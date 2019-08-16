
/****************************** Module Header ******************************\
*Module Name:  Tables.aspx.cs
*Project:      TherapistPortal
*Copyright (c) Microsoft Corporation.
* 
*The Azure Table storage service stores large amounts of structured data. 
*The service is a NoSQL datastore which accepts authenticated calls from inside and outside the Azure cloud
*You can use the Table service to store and query huge sets of structured
*
*This project demonstrates How to bulk import/export data with Excel to/from Azure table storage.
*Users can bulk import data with Excel to Table storage 
*Users can bulk export data with Excel from Table storage 
* 
*This source is subject to the Microsoft Public License.
*See http://www.microsoft.com/en-us/openness/licenses.aspx#MPL
*All other rights reserved.
* 
*THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
*EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED 
*WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
\***************************************************************************/
using Excel;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.Protocol;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace TherapistPortal
{
    public partial class Notifier : System.Web.UI.Page
    {
        string strNotifierLogTable = "NotifierStatus";
        string strPIITable = "PIINotifier";

        private static string StorageConnectionString = Environment.GetEnvironmentVariable("StorageAccount");

        CloudStorageAccount storageAccount;

        protected void Page_Load(object sender, EventArgs e)
        {
            string userID = null;
            if (!Request.IsLocal)
            {
                userID = Global.GetIDFromWindowsLive(Request);
                if (!Environment.GetEnvironmentVariable("TherapistPortalUserIDs").Contains(userID))
                {
                    throw new UnauthorizedAccessException("Your User ID (" + userID + ") is not in TherapistPortalUserIDs and therefore cannot access this page.");
                }
            }

            storageAccount = CloudStorageAccount.Parse(StorageConnectionString);

            RefreshStatus();
        }

        /// <summary>
        /// Imports selected excel files to table storage
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void btn_Import_Click(object sender, EventArgs e)
        {
            FileInfo file = null;
            FileInfo copyfile = null;
            try
            {
                bool blnFlag = false;
                HttpFileCollection Files = Request.Files;
                for (int i = 0; i < Files.Count; i++)
                {
                    string strFileName = string.Empty;
                    string strFilePath = Files[i].FileName;
                    string[] aryFileName = strFilePath.Split('\\');
                    if (aryFileName.Length > 0)
                    {
                        strFileName = aryFileName[aryFileName.Length - 1];
                    }
                    if (!string.IsNullOrEmpty(strFileName))
                    {
                        string strCopyFilePath = Server.MapPath("Spreadsheets");
                        if (Directory.Exists(strCopyFilePath) == false)
                        {
                            Directory.CreateDirectory(strCopyFilePath);
                        }
                        ful_FileUpLoad.SaveAs(strCopyFilePath + "\\" + strFileName);
                        file = new FileInfo(strCopyFilePath + "\\" + strFileName);
                        copyfile = new FileInfo(strCopyFilePath + "\\" + "Copy" + strFileName);
                        if (copyfile.Exists)
                        {
                            copyfile.Delete();
                        }
                        file.CopyTo(strCopyFilePath + "\\" + "Copy" + strFileName);
                        string extension = file.Extension;
                        if (extension == ".xls" || extension == ".xlsx")
                        {
                            ReadExcelInfo(strCopyFilePath + "\\" + "Copy" + strFileName);
                        }
                        else
                        {
                            Response.Write("<script>alert('" + strFilePath + " is not an excel file.');</script>");
                            file.Delete();
                            copyfile.Delete();
                            //Lists all tables of the specified storageAccount 
                            RefreshStatus();
                            return;
                        }
                        blnFlag = true;
                        file.Delete();
                        copyfile.Delete();
                    }
                }
                if (blnFlag)
                {
                    Response.Write("<script>alert('Successfully imported excel files.');</script>");
                }
                else
                {
                    Response.Write("<script>alert('Select the excel files you want to import.');</script>");
                }
            }
            catch (Exception ex)
            {
                if (file != null)
                {
                    file.Delete();
                }
                if (copyfile != null)
                {
                    copyfile.Delete();
                }
                string strError = "<br/>Importing failed! Error message is <blockquote><pre>" + HttpUtility.HtmlEncode(ex) + "</pre></blockquote>";
                Response.Write(strError);
            }
            //Lists all tables of the specified storageAccount 
            RefreshStatus();
        }

        /// <summary>
        /// refresh all table of the specified storageAccount 
        /// </summary>
        private void RefreshStatus()
        {
            var client = storageAccount.CreateCloudTableClient();
            CloudTable table = client.GetTableReference(strPIITable);
            string strFilter = TableQuery.GenerateFilterCondition("InvitationCode", QueryComparisons.NotEqual, "");
            TableQuery<ExcelTableEntity> query = new TableQuery<ExcelTableEntity>().Where(strFilter);
            int count = 0;

            try
            {
                count = table.ExecuteQuery(query).ToList().Select(row => row.properties["InvitationCode"]).Distinct().Count();
            }
            catch (StorageException)
            {
            }

            lbl_Contacts.Text = count + " Contacts in Azure";

            ShowContactList();
            ShowNotifierStatus();
        }

        /// <summary>
        /// Reads content of excel files that are selected
        /// </summary>
        /// <param name="strFilePath"></param>
        private void ReadExcelInfo(string strFilePath)
        {
            string strConn = string.Empty;
            FileInfo file = new FileInfo(strFilePath);
            if (!file.Exists) { throw new Exception(strFilePath + " does not exist"); }

            FileStream stream = File.Open(strFilePath, FileMode.Open, FileAccess.Read);

            //2. Reading from a OpenXml Excel file (2007 format; *.xlsx)
            IExcelDataReader excelReader = ExcelReaderFactory.CreateOpenXmlReader(stream);

            //4. DataSet - Create column names from first row
            excelReader.IsFirstRowAsColumnNames = true;
            DataSet result = excelReader.AsDataSet();

            foreach (System.Data.DataTable table in result.Tables)
            {
                if (String.IsNullOrWhiteSpace(txt_SheetNames.Text) || txt_SheetNames.Text.ToLower().Split(new string[] { " , ", ", ", "," }, StringSplitOptions.RemoveEmptyEntries).Contains(table.TableName.ToLower().Replace("$", "")))
                {
                    ImportDataToTable(table, table.TableName);
                }
            }

            //6. Free resources (IExcelDataReader is IDisposable)
            excelReader.Close();
        }

        /// <summary>
        /// Imports data of DataTable to table storage
        /// </summary>
        /// <param name="dtSheetInfo"></param>
        /// <param name="strSheetName"></param>
        private void ImportDataToTable(System.Data.DataTable dtSheetInfo, string strSheetName)
        {
            var client = storageAccount.CreateCloudTableClient();
            CloudTable table = client.GetTableReference(strPIITable);

            Response.Write(new string(' ', 1024));
            Response.Write(String.Format("<div>Deleting existing data"));
            Response.Flush();

            table.DeleteIfExists();

            create:
            try
            {
                Response.Write(".");
                Response.Flush();
                table.Create();
            }
            catch (StorageException ex) when (ex.RequestInformation.ExtendedErrorInformation.ErrorCode.Equals(TableErrorCodeStrings.TableBeingDeleted))
            {
                Thread.Sleep(1000);
                goto create;
            }

            Response.Write(String.Format("</div><div>Uploading {0} rows for sheet {1}", dtSheetInfo.Rows.Count, strSheetName.Replace("$", "")));
            Response.Flush();

            // Create a new partition key for this data instead of overwriting old data.
            var partitionKey = strSheetName;

            var batch = new TableBatchOperation();

            for (int j = 0; j < dtSheetInfo.Rows.Count; j++)
            {
                ExcelTableEntity entity = new ExcelTableEntity(partitionKey, (j + 2).ToString("D5"));
                var hasContent = false;
                for (int i = 0; i < dtSheetInfo.Columns.Count; i++)
                {
                    string strCloName = dtSheetInfo.Columns[i].ColumnName;
                    if (!(dtSheetInfo.Rows[j][i] is DBNull) && (dtSheetInfo.Rows[j][i] != null))
                    {
                        hasContent = true;
                        string strValue = dtSheetInfo.Rows[j][i].ToString().Trim();
                        if (!CheckPropertyExist(strCloName, strValue, entity))
                        {
                            EntityProperty property = entity.ConvertToEntityProperty(strCloName, dtSheetInfo.Rows[j][i]);
                            if (!entity.properties.ContainsKey(strCloName))
                            {
                                entity.properties.Add(strCloName, property);
                            }
                            else
                            {
                                entity.properties[strCloName] = property;
                            }
                        }
                    }
                }

                if (hasContent)
                {
                    batch.Add(TableOperation.InsertOrReplace(entity));
                }

                if (batch.Count >= 100)
                {
                    table.ExecuteBatch(batch);
                    Response.Write(".");
                    Response.Flush();
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                table.ExecuteBatch(batch);
                Response.Write(".");
                Response.Flush();
            }

            Response.Write("</div><hr/>");
            Response.Flush();
        }

        /// <summary>
        /// Sets title of column of DataTable using property of ExcelTableEntity
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="dtEntityInfo"></param>
        private void SetColumnTitle(object obj, System.Data.DataTable dtEntityInfo)
        {
            try
            {
                //Lists all Properties of ExcelTableEntity
                Type entityType = typeof(ExcelTableEntity);
                PropertyInfo[] ProList = entityType.GetProperties();
                foreach (PropertyInfo Pro in ProList)
                {
                    if (Pro.PropertyType.Name.Contains("IDictionary"))
                    {
                        Dictionary<string, EntityProperty> dicEntity = (Dictionary<string, EntityProperty>)Pro.GetValue(obj, null);

                        foreach (string key in dicEntity.Keys)
                        {
                            DataColumn col = new DataColumn(key);
                            dtEntityInfo.Columns.Add(col);
                        }
                    }
                    else if (Pro.Name != "ETag")
                    {
                        DataColumn col = new DataColumn(Pro.Name);
                        dtEntityInfo.Columns.Add(col);
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Inserts values of all ExcelTableEntity properties to DataTable
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="dtEntityInfo"></param>
        private void InsertEntityDataToTable(object obj, System.Data.DataTable dtEntityInfo, params string[] filter)
        {
            try
            {
                DataRow row = dtEntityInfo.Rows.Add();

                //Lists all Properties of ExcelTableEntity
                Type entityType = typeof(ExcelTableEntity);
                PropertyInfo[] ProList = entityType.GetProperties();
                foreach (PropertyInfo Pro in ProList)
                {
                    if (Pro.PropertyType.Name.Contains("IDictionary"))
                    {
                        Dictionary<string, EntityProperty> dicEntity = (Dictionary<string, EntityProperty>)Pro.GetValue(obj, null);

                        foreach (string key in dicEntity.Keys)
                        {
                            if (!dtEntityInfo.Columns.Contains(key))
                            {
                                DataColumn col = new DataColumn(key);
                                dtEntityInfo.Columns.Add(col);
                            }
                            if (filter == null || filter.Length == 0 || filter.Contains(key))
                            {
                                row[key] = dicEntity[key].PropertyAsObject.ToString();
                            }
                            else
                            {
                                row[key] = "&lt;hidden&gt;";
                            }
                        }
                    }
                    else if (Pro.Name != "ETag")
                    {
                        row[Pro.Name] = Pro.GetValue(obj, null).ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Checks the property is exist or not in ExcelTableEntity
        /// </summary>
        /// <param name="strProperName"></param>
        /// <param name="strValue"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        private bool CheckPropertyExist(string strProperName, string strValue, ExcelTableEntity entity)
        {
            bool bln_Result = false;
            try
            {
                Type entityType = typeof(ExcelTableEntity);
                PropertyInfo[] ProList = entityType.GetProperties();
                for (int i = 0; i < ProList.Length; i++)
                {
                    if (ProList[i].Name == strProperName)
                    {
                        if (ProList[i].PropertyType.Name == "DateTimeOffset")
                        {
                            DateTime dtime = Convert.ToDateTime(strValue);
                            dtime = DateTime.SpecifyKind(dtime, DateTimeKind.Utc);
                            DateTimeOffset utcTime2 = dtime;
                            ProList[i].SetValue(entity, utcTime2);
                        }
                        else
                        {
                            ProList[i].SetValue(entity, strValue);
                        }
                        bln_Result = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return bln_Result;
        }

        /// <summary>
        ///Shows the notifier table
        /// </summary>
        private void ShowContactList()
        {
            //Sets text content of cells using table name
            TableRow tableTitleRow = new TableRow();
            tableTitleRow.ID = "tblTitleRow_" + strPIITable;
            TableCell celltitle = new TableCell();
            celltitle.Text = strPIITable;
            celltitle.HorizontalAlign = HorizontalAlign.Left;
            celltitle.Style.Add("font-size", "16pt");
            celltitle.Style.Add("Font-Bold", "true");
            tableTitleRow.Cells.Add(celltitle);
            tbl_TableContactList.Rows.Add(tableTitleRow);

            //Binds DataGrid with data of Querying table storage
            TableCell cell = new TableCell();
            DataGrid dgDynamicTableInfo = new DataGrid();
            dgDynamicTableInfo.ID = "dg_" + strPIITable;
            var client = storageAccount.CreateCloudTableClient();
            CloudTable table = client.GetTableReference(strPIITable);
            string strPartitionKey = "";
            string strFilter = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.NotEqual, strPartitionKey);
            TableQuery<ExcelTableEntity> query = new TableQuery<ExcelTableEntity>().Where(strFilter);
            List<ExcelTableEntity> results = table.ExecuteQuery(query).ToList();

            TableRow tableDataRow = new TableRow();
            tableDataRow.ID = "tblDataRow_" + strPIITable;
            System.Data.DataTable dtInfo = new System.Data.DataTable();
            int i = 0;
            int limit = 100;
            foreach (object entity in results)
            {
                if (i == 0)
                {
                    SetColumnTitle(entity, dtInfo);
                }
                if (i < limit)
                {
                    InsertEntityDataToTable(entity, dtInfo, "InvitationCode", "StartTime", "EndTime", "TimeZone");
                }
                i++;
                if (i > limit)
                {
                    break;
                }
            }
            if (i > 0)
            {
                dgDynamicTableInfo.EnableViewState = true;
                dgDynamicTableInfo.DataSource = dtInfo;
                dgDynamicTableInfo.DataBind();
                cell.Controls.Add(dgDynamicTableInfo);
            }
            tableDataRow.Cells.Add(cell);
            tbl_TableContactList.Rows.Add(tableDataRow);
            celltitle.Text = strPIITable + " (" + (i > limit ? "over " + limit : i.ToString()) + " rows)";
        }

        /// <summary>
        ///Shows the notifier table
        /// </summary>
        private void ShowNotifierStatus()
        {
            //Sets text content of cells using table name
            TableRow tableTitleRow = new TableRow();
            tableTitleRow.ID = "tblTitleRow_" + strNotifierLogTable;
            TableCell celltitle = new TableCell();
            celltitle.Text = strNotifierLogTable;
            celltitle.HorizontalAlign = HorizontalAlign.Left;
            celltitle.Style.Add("font-size", "16pt");
            celltitle.Style.Add("Font-Bold", "true");
            tableTitleRow.Cells.Add(celltitle);
            tbl_TableNotifierStatus.Rows.Add(tableTitleRow);

            //Binds DataGrid with data of Querying table storage
            TableCell cell = new TableCell();
            DataGrid dgDynamicTableInfo = new DataGrid();
            dgDynamicTableInfo.ID = "dg_" + strNotifierLogTable;
            var client = storageAccount.CreateCloudTableClient();
            CloudTable table = client.GetTableReference(strNotifierLogTable);
            string strPartitionKey = "";
            string strFilter = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.NotEqual, strPartitionKey);
            TableQuery<ExcelTableEntity> query = new TableQuery<ExcelTableEntity>().Where(strFilter);
            List<ExcelTableEntity> results = table.ExecuteQuery(query).OrderByDescending(r => r.Timestamp).ToList();

            TableRow tableDataRow = new TableRow();
            tableDataRow.ID = "tblDataRow_" + strNotifierLogTable;
            System.Data.DataTable dtInfo = new System.Data.DataTable();
            int i = 0;
            int limit = 100;
            foreach (object entity in results)
            {
                if (i == 0)
                {
                    SetColumnTitle(entity, dtInfo);
                }
                if (i < limit)
                {
                    InsertEntityDataToTable(entity, dtInfo);
                }
                i++;
                if (i > limit)
                {
                    break;
                }
            }
            if (i > 0)
            {
                dgDynamicTableInfo.EnableViewState = true;
                dgDynamicTableInfo.DataSource = dtInfo;
                dgDynamicTableInfo.DataBind();
                cell.Controls.Add(dgDynamicTableInfo);
            }
            tableDataRow.Cells.Add(cell);
            tbl_TableNotifierStatus.Rows.Add(tableDataRow);
            celltitle.Text = strNotifierLogTable + " (" + (i > limit ? "over " + limit : i.ToString()) + " rows)";
        }
    }
}