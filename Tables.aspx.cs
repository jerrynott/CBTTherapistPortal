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
using System.Web.Services.Description;
using System.Web.UI;
using System.Web.UI.WebControls;
using Excel;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;

namespace TherapistPortal
{
    public partial class Tables : System.Web.UI.Page
    {
        /// <summary>
        /// Stores the connection string to Azure storage, which is retrieved in the static constructor below.
        /// </summary>
        private static string StorageConnectionString = Environment.GetEnvironmentVariable("StorageAccount");
        private static string DevStorageConnectionString = Environment.GetEnvironmentVariable("StorageAccountDev");

        /// <summary>
        /// Stores the prod and dev storage account IDs. The current storage account in use is stored into storageAccount. The user can switch between targetting prod and dev storage accounts by clicking buttons on the page, which will trigger the change of storageAccount and refresh of table list.
        /// </summary>
        CloudStorageAccount prodStorageAccount, devStorageAccount;
        CloudStorageAccount storageAccount;

        protected void Page_Load(object sender, EventArgs e)
        {
            prodStorageAccount = !string.IsNullOrEmpty(StorageConnectionString) ? CloudStorageAccount.Parse(StorageConnectionString) : null;
            devStorageAccount = !string.IsNullOrEmpty(DevStorageConnectionString) ? CloudStorageAccount.Parse(DevStorageConnectionString) : null;
            ChangeTargetDatabase(CurrentDbMode);
            UpdateDbModeButtons();
            string userID = null;
            if (!Request.IsLocal)
            {
                userID = Global.GetIDFromAzure(Request);
                if (userID == null)
                {
                    Global.RedirectToLogin(Request, Response);
                    return;
                }
                if (!Environment.GetEnvironmentVariable("TherapistPortalUserIDs").Contains(userID))
                {
                    throw new UnauthorizedAccessException("Your User ID (" + userID + ") is not in TherapistPortalUserIDs and therefore cannot access this page.");
                }
            }

            if (lbl_Example.Text == "Timestamp ge datetime'2017-05-31T23:59:59Z'")
            {
                var now = DateTime.UtcNow;
                now = now.AddDays(-7).AddTicks(-now.Ticks % TimeSpan.TicksPerSecond);
                lbl_Example.Text = "Timestamp ge datetime'" + DateTime.UtcNow.AddDays(-7).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ssK") + "'";
                if (userID != null)
                {
                    lbl_Example.Text += " and PartitionKey eq '" + userID + "'";
                }
            }

            if (!IsPostBack)
            {
                try
                {
                    GetAllTableName();
                }
                catch (Exception ex)
                {
                    string strError = "<br/>Getting table list failed! Error message is <blockquote><pre>" + HttpUtility.HtmlEncode(ex) + "</pre></blockquote>";
                    Response.Write(strError);
                }
            }

            if (chk_ShowDetails.Checked)
            {
                ShowTableContent();
            }
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
                tbl_TableDetailList.Rows.Clear();
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
                            RefreshAllTableName();
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
            RefreshAllTableName();
        }

        /// <summary>
        /// Uploads selected media files to blob storage of prod storage account. The supported media types are mp4, mov, avi, mkv, webm, wmv, m4v for video files; mp3, wav, aac, ogg, m4a, flac for audio files; jpg, jpeg, png, gif, webp, bmp, svg for image files.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void btn_Media_Upload(object sender, EventArgs e)
        {
            var allowedTypes = new Dictionary<string, string>
            {
                { ".mp4",  "video/mp4" },
                { ".mov",  "video/quicktime" },
                { ".avi",  "video/x-msvideo" },
                { ".mkv",  "video/x-matroska" },
                { ".webm", "video/webm" },
                { ".wmv",  "video/x-ms-wmv" },
                { ".m4v",  "video/x-m4v" },
                { ".mp3",  "audio/mpeg" },
                { ".wav",  "audio/wav" },
                { ".aac",  "audio/aac" },
                { ".ogg",  "audio/ogg" },
                { ".m4a",  "audio/mp4" },
                { ".flac", "audio/flac" },
                { ".jpg",  "image/jpeg" },
                { ".jpeg", "image/jpeg" },
                { ".png",  "image/png" },
                { ".gif",  "image/gif" },
                { ".webp", "image/webp" },
                { ".bmp",  "image/bmp" },
                { ".svg",  "image/svg+xml" }
            };

            try
            {
                HttpFileCollection files = Request.Files;

                if (files.Count == 0 || (files.Count == 1 && string.IsNullOrEmpty(files[0].FileName)))
                {
                    Response.Write("<script>alert('Select the media files you want to upload first.');</script>");
                    return;
                }

                CloudBlobClient blobClient = prodStorageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = blobClient.GetContainerReference("media");
                container.CreateIfNotExists(BlobContainerPublicAccessType.Off);

                /// Instead of uploading single file, allows for uploading of multiple files at once. Also displays the currently uploaded files before the upload process.
                List<string> uploaded = new List<string>();

                for (int i = 0; i < files.Count; i++)
                {
                    HttpPostedFile file = files[i];

                    if (string.IsNullOrEmpty(file.FileName) || file.ContentLength == 0)
                        continue;

                    string originalName = Path.GetFileName(file.FileName);
                    string extension = Path.GetExtension(originalName).ToLowerInvariant();

                    string contentType;
                    if (!allowedTypes.TryGetValue(extension, out contentType))
                    {
                        Response.Write("<script>alert('" +
                            HttpUtility.JavaScriptStringEncode(originalName) +
                            " is not a supported media file.');</script>");
                        return;
                    }

                    CloudBlockBlob blob = container.GetBlockBlobReference(originalName);
                    blob.Properties.ContentType = contentType;
                    blob.UploadFromStream(file.InputStream);
                    uploaded.Add(originalName);
                }

                if (uploaded.Count > 0)
                {
                    string names = string.Join(", ", uploaded);
                    Response.Write("<script>alert('Successfully uploaded: " +
                        HttpUtility.JavaScriptStringEncode(names) + "');</script>");
                }
                else
                {
                    Response.Write("<script>alert('No valid media files were uploaded.');</script>");
                }
            }
            catch (Exception ex)
            {
                string strError = "<br/>Media upload failed! Error message is <blockquote><pre>" +
                    HttpUtility.HtmlEncode(ex) + "</pre></blockquote>";
                Response.Write(strError);
            }
        }

        /// <summary>
        /// refresh all table of the specified storageAccount
        /// </summary>
        private void RefreshAllTableName()
        {
            List<string> lstSelectedTableName = new List<string>();
            foreach (ListItem item in ckb_TableName.Items)
            {
                if (item.Selected)
                {
                    if (!lstSelectedTableName.Contains(item.Text))
                    {
                        lstSelectedTableName.Add(item.Text);
                    }
                }
            }
            //Lists all tables of the specified storageAccount 
            ViewState.Add("SelectedTableName", lstSelectedTableName);
            GetAllTableName();
        }

        /// <summary>
        /// Exports selected storage tables to excel
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void btn_ExportData_Click(object sender, EventArgs e)
        {
            List<string> lstSelectedTableName = new List<string>();
            if (ckb_TableName.Items.Count <= 0)
            {
                return;
            }
            try
            {
                List<string> exportedFiles = new List<string>();
                foreach (ListItem item in ckb_TableName.Items)
                {
                    if (item.Selected)
                    {
                        string path = ExportDataToExcel(item.Text, txt_FilterString.Text);
                        exportedFiles.Add(path);
                        if (!lstSelectedTableName.Contains(item.Text))
                        {
                            lstSelectedTableName.Add(item.Text);
                        }
                    }
                }
                if (exportedFiles.Count > 0)
                {
                    if (exportedFiles.Count == 1)
                    {
                        //Response.ContentType = "application/vnd.ms-excel";
                        Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        Response.AddHeader("Content-Disposition", "attachment; filename=" + Path.GetFileName(exportedFiles[0]));
                        Response.TransmitFile(exportedFiles[0]);
                    }
                    else
                    {
                        ZipFile zip = ZipFile.Create(Path.GetTempFileName());
                        zip.BeginUpdate();
                        foreach (string file in exportedFiles)
                        {
                            zip.Add(file, Path.GetFileName(file));
                        }
                        zip.CommitUpdate();
                        zip.Close();
                        Response.ContentType = "application/zip";
                        Response.AddHeader("Content-Disposition", "attachment; filename=Tables.zip");
                        Response.TransmitFile(zip.Name);
                    }
                    Response.Flush(); // Sends all currently buffered output to the client.
                    Response.SuppressContent = true;  // Gets or sets a value indicating whether to send HTTP content to the client.
                    HttpContext.Current.ApplicationInstance.CompleteRequest(); // Causes ASP.NET to bypass all events and filtering in the HTTP pipeline chain of execution and directly execute the EndRequest event.
                }
                else
                {
                    Response.Write("<script>alert('Select the storage tables you want to export.');</script>");
                }
            }
            catch (Exception ex)
            {
                string strError = "<br/>Exporting failed! Error message is <blockquote><pre>" + HttpUtility.HtmlEncode(ex) + "</pre></blockquote>";
                Response.Write(strError);
            }

            //Lists all tables of the specified storageAccount 
            ViewState.Add("SelectedTableName", lstSelectedTableName);
            GetAllTableName();
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

            IExcelDataReader excelReader = file.Extension.ToLower() == ".xls"
                ? ExcelReaderFactory.CreateBinaryReader(stream)
                : ExcelReaderFactory.CreateOpenXmlReader(stream);

            if (excelReader == null)
                throw new Exception($"Could not read '{file.Name}'. The file may be corrupt, password-protected, or created by a newer version of Excel than this library supports.");

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
            string strTableName = txt_TableName.Text;
            if (!string.IsNullOrEmpty(strTableName))
            {
                Response.Write(new string(' ', 1024));
                Response.Write(String.Format("<div>Uploading {0} rows for sheet {1}", dtSheetInfo.Rows.Count, strSheetName.Replace("$", "")));
                Response.Flush();

                CloudTable table = client.GetTableReference(strTableName);
                table.CreateIfNotExists();

                // Create a new partition key for this data instead of overwriting old data.
                var partitionKey = strSheetName + DateTime.UtcNow.ToString("o");

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

                var pointer = new ExcelTableEntity(strSheetName.Replace("$", ""), "Latest");
                pointer.properties.Add("ID", new EntityProperty(partitionKey));
                table.Execute(TableOperation.InsertOrReplace(pointer));

                Response.Write(String.Format("\n PartitionKey: <code>{0}</code></div><hr/>", partitionKey));
                Response.Flush();
            }
        }

        /// <summary>
        /// Exports data of selected storage tables to excel
        /// </summary>
        /// <param name="strTableName"></param>
        /// <param name="strFilter"></param>
        private string ExportDataToExcel(string strTableName, string strFilter)
        {
            var client = storageAccount.CreateCloudTableClient();
            if (!string.IsNullOrEmpty(strTableName))
            {
                CloudTable table = client.GetTableReference(strTableName);
                TableQuery<ExcelTableEntity> query = new TableQuery<ExcelTableEntity>().Where(strFilter);
                IEnumerable<ExcelTableEntity> results = table.ExecuteQuery(query);

                System.Data.DataTable dtInfo = new System.Data.DataTable();
                int i = 0;
                foreach (ExcelTableEntity entity in results)
                {
                    if (i == 0)
                    {
                        SetColumnTitle(entity, dtInfo);
                    }
                    InsertEntityDataToTable(entity, dtInfo);
                    i++;
                }
                string strPath = Server.MapPath("Spreadsheets");
                if (Directory.Exists(strPath) == false)
                {
                    Directory.CreateDirectory(strPath);
                }
                strPath = strPath + "\\" + strTableName + ".xlsx";
                ExportToExcel.CreateExcelFile.CreateExcelDocument(dtInfo, strPath);
                return strPath;
            }
            else
            {
                throw new ArgumentNullException(strTableName);
            }
        }

        /// <summary>
        /// Sets title of column of DataTable using property of ExcelTableEntity
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="dtEntityInfo"></param>
        private void SetColumnTitle(object obj, System.Data.DataTable dtEntityInfo)
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

        /// <summary>
        /// Inserts values of all ExcelTableEntity properties to DataTable
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="dtEntityInfo"></param>
        private void InsertEntityDataToTable(object obj, System.Data.DataTable dtEntityInfo)
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
                        row[key] = dicEntity[key].PropertyAsObject.ToString();
                    }
                }
                else if (Pro.Name != "ETag")
                {
                    row[Pro.Name] = Pro.GetValue(obj, null).ToString();
                }
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
            return bln_Result;
        }

        /// <summary>
        /// Lists all tables of the specified storageAccount 
        /// </summary>
        private void GetAllTableName()
        {
            CloudTableClient client = storageAccount.CreateCloudTableClient();
            ckb_TableName.Items.Clear();
            List<string> lstSelectedTableName = new List<string>();
            if (ViewState["SelectedTableName"] != null)
            {
                lstSelectedTableName = (List<string>)ViewState["SelectedTableName"];
            }
            foreach (CloudTable table in client.ListTables())
            {
                if (!table.Name.StartsWith("PII"))
                {
                    ListItem item = new ListItem();
                    item.Text = table.Name;
                    item.Value = table.Name;
                    ckb_TableName.Items.Add(item);

                    if (lstSelectedTableName.Contains(item.Text))
                    {
                        item.Selected = true;
                    }
                }
            }
        }

        /// <summary>
        ///Shows 10 records of each selected table under table storage
        /// </summary>
        private void ShowTableContent()
        {
            foreach (ListItem item in ckb_TableName.Items)
            {
                if (item.Selected && !item.Text.StartsWith("PII"))
                {
                    //Sets text content of cells using table name
                    TableRow tableTitleRow = new TableRow();
                    tableTitleRow.ID = "tblTitleRow_" + item.Text;
                    TableCell celltitle = new TableCell();
                    celltitle.HorizontalAlign = HorizontalAlign.Left;
                    celltitle.Style.Add("font-size", "16pt");
                    celltitle.Style.Add("Font-Bold", "true");
                    tableTitleRow.Cells.Add(celltitle);
                    tbl_TableDetailList.Rows.Add(tableTitleRow);

                    //Binds DataGrid with data of Querying table storage
                    TableCell cell = new TableCell();
                    DataGrid dgDynamicTableInfo = new DataGrid();
                    dgDynamicTableInfo.ID = "dg_" + item.Text;
                    var client = storageAccount.CreateCloudTableClient();
                    CloudTable table = client.GetTableReference(item.Text);
                    string strFilter = txt_FilterString.Text;
                    TableQuery<ExcelTableEntity> query = new TableQuery<ExcelTableEntity>().Where(strFilter);
                    IEnumerable<ExcelTableEntity> results = table.ExecuteQuery(query);

                    TableRow tableDataRow = new TableRow();
                    tableDataRow.ID = "tblDataRow_" + item.Text;
                    System.Data.DataTable dtInfo = new System.Data.DataTable();
                    int i = 0;
                    int limit = 10;
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
                    tbl_TableDetailList.Rows.Add(tableDataRow);
                    celltitle.Text = item.Text + " (" + (i > limit ? "over " + limit : i.ToString()) + " rows)";
                }
            }
        }

        /// <summary>
        /// Gets the current database mode (prod or dev) that user is targeting. The default mode is dev.
        /// </summary>
        public string CurrentDbMode => (ViewState["DbTarget"] as string) ?? "dev";

        /// <summary>
        /// Updates the visibility, enabled status and CSS class of prod/dev mode buttons based on the current database mode. This provides visual feedback to users about which database they are currently targeting and prevents them from clicking the button of the mode they are already in.
        /// </summary>
        private void UpdateDbModeButtons()
        {
            bool isDev = CurrentDbMode == "dev";
            btn_Dev.Visible = devStorageAccount != null;
            btn_Dev.CssClass = "btn btn-mode" + (isDev ? " btn-mode-active" : "");
            btn_Prod.CssClass = "btn btn-mode" + (!isDev ? " btn-mode-active" : "");
            btn_Dev.Enabled = !isDev;
            btn_Prod.Enabled = isDev;
            hf_DbMode.Value = CurrentDbMode;
        }

        /// <summary>
        /// Handles the click event of the Dev button. When clicked, it sets the target database to dev, clears the selected table name in view state, changes the storage account to dev storage account, updates the button states, and refreshes the table list.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void btn_Dev_Click(object sender, EventArgs e)
        {
            ViewState["DbTarget"] = "dev";
            ViewState.Remove("SelectedTableName");
            ChangeTargetDatabase("dev");
            UpdateDbModeButtons();
            GetAllTableName();
        }

        /// <summary>
        /// Handles the click event of the Prod button. When clicked, it sets the target database to prod, clears the selected table name in view state, changes the storage account to prod storage account, updates the button states, and refreshes the table list.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void btn_Prod_Click(object sender, EventArgs e)
        {
            ViewState["DbTarget"] = "prod";
            ViewState.Remove("SelectedTableName");
            ChangeTargetDatabase("prod");
            UpdateDbModeButtons();
            GetAllTableName();
        }

        /// <summary>
        /// Changes the storage account that the page is targeting based on the input target ("dev" or "prod"). This method is called when user clicks the prod or dev button to switch between prod and dev storage accounts.
        /// </summary>
        /// <param name="target"></param>
        private void ChangeTargetDatabase(string target)
        {
            switch (target)
            {
                case "dev":
                    storageAccount = devStorageAccount;
                    break;
                case "prod":
                    storageAccount = prodStorageAccount;
                    break;
                default:
                    break;
            }
        }
    }
}
