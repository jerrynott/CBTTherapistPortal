<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Tables.aspx.cs" Inherits="TherapistPortal.Tables" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title></title>
    <link rel="stylesheet" type="text/css" href="normalize.css" />
    <script src="Scripts/jquery-1.10.2.min.js">   
    </script>
    <script type="text/javascript">
        $(document).ready(function () {

            $("#btn_Import").click(function () {
                var containerName = $("#txt_TableName").val();
                var patrn = /^[a-z]([a-z0-9])*$/;
                var result = patrn.test(containerName);
                if (!result) {
                    $("#lbl_NameError").css("visibility", "visible");
                    return false;
                }
                else {
                    if (containerName.length >= 3 && containerName.length <= 63) {
                        $("#lbl_NameError").css("visibility", "hidden");
                        //check files name
                        var filename = $("#ful_FileUpLoad").val();
                        if (filename.length > 0) {
                            return true;
                        } else {
                            alert("Select the files you want to upload first.");
                            return false;
                        }
                    } else {
                        $("#lbl_NameError").css("visibility", "visible");
                        return false;
                    }
                }
            });
        });
    </script>
</head>
<body style="font-family:'Segoe UI', Tahoma, Geneva, Verdana, sans-serif">
    <form id="form1" runat="server">
        <div>
            <div style="font-weight: 600">Export Azure tables to Excel files</div>
            <div style="margin-left: 20px">(1) Select the storage table you want to export to excel.</div>
            <div style="margin-left: 20px">(3) Optionally enter an Azure/OData query string to filter the exported results.  </div>
            <div style="margin-left: 20px">(2) Optionally check "Preview table details" to view 10 records of each selected table under table storage.  </div>
            <div style="margin-left: 20px">(4) Click “Export To Excel”.  </div>
            <br />
            <asp:CheckBoxList ID="ckb_TableName" runat="server" AutoPostBack="true" Font-Size="16px">
            </asp:CheckBoxList>
            <br />
            <label id="lbl_FilterString" style="font-weight: 600">Filter:</label>
            <br />
            <asp:TextBox ID="txt_FilterString" runat="server" Width="50%" AutoPostBack="true"></asp:TextBox>
            <br />
            Example: <code><asp:Label ID="lbl_Example" runat="server">Timestamp ge datetime'2017-05-31T23:59:59Z'</asp:Label></code>
            <br />
            <br />
            <asp:CheckBox ID="chk_ShowDetails" runat="server" Checked="false" Text="Preview table details" Font-Size="16px" AutoPostBack="true" EnableViewState="true" />
            <asp:Table ID="tbl_TableDetailList" runat="server" EnableViewState="true" Font-Size="15px" style="white-space: nowrap">
                <asp:TableRow></asp:TableRow>
            </asp:Table>
            <br />
            <br />
            <asp:Button ID="btn_ExportData" runat="server" Text="Export To Excel" OnClick="btn_ExportData_Click" />
            <br />
            <br />
            <hr />
        </div>
        <br />
        <div>
            <div>
                <div style="font-weight: 600">Import Excel files to an Azure table</div>
                <div style="margin-left: 20px">
                    (1) Input the name of the storage table to which you want to import excel files<br />
                    (2) Table names must be valid DNS names, 3-63 characters in length.<br />
                    (3) Beginning with a letter and containing only alphanumeric characters.<br />
                    (4) Table names are case-sensitive<br />
                </div>
                <br />
                <label id="lbl_NewTableName">Table name:</label>
                <asp:TextBox ID="txt_TableName" ReadOnly="true" BackColor="LightGray" runat="server">content</asp:TextBox>
                <label id="lbl_NameError" style="color: red; visibility: hidden">Table name is invalid, please check it.</label>
                <br />
            </div>
            <br />
            <label id="lbl_FileInfo" style="font-weight: 600">Select the excel files you want to import</label>
            <br />
            <asp:FileUpload runat="server" AllowMultiple="true" ID="ful_FileUpLoad" />
            <br />
            <br />
            <label id="lbl_SheetNames" style="font-weight: 600">[Optional] Enter a comma-separated list of worksheet names to import (or leave blank to import all worksheets)</label><br />
            <asp:TextBox ID="txt_SheetNames" runat="server" Width="50%"></asp:TextBox>
            <br />
            <br />
            <asp:Button ID="btn_Import" runat="server" Text="Import to Azure" OnClick="btn_Import_Click" />
        </div>
    </form>
</body>
</html>
