<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Notifier.aspx.cs" Inherits="TherapistPortal.Notifier" %>

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
<body>
    <form id="form1" runat="server">
        <div>
            <div style="font-weight: 600">Notifier Status</div>
            <asp:Label ID="lbl_Contacts" runat="server"></asp:Label>
            <br />
            <br />
            <asp:Table ID="tbl_TableContactList" runat="server" EnableViewState="false" Font-Size="15px" style="white-space: nowrap">
                <asp:TableRow></asp:TableRow>
            </asp:Table>
            <br />
            <asp:Table ID="tbl_TableNotifierStatus" runat="server" EnableViewState="false" Font-Size="15px" style="white-space: nowrap">
                <asp:TableRow></asp:TableRow>
            </asp:Table>
            <br />
            <hr />
        </div>
        <br />
        <div>
            <div>
                <div style="font-weight: 600">Import Excel files to Notifier</div>
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
            <asp:Button ID="btn_Import" runat="server" Text="Import" OnClick="btn_Import_Click" />
        </div>
    </form>
</body>
</html>
