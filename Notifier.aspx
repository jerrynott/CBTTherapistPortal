<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Notifier.aspx.cs" Inherits="TherapistPortal.Notifier" MasterPageFile="~/Site.Master" %>

<asp:Content ContentPlaceHolderID="HeadContent" runat="server">
    <script type="text/javascript">
        $(document).ready(function () {
            $("#btn_Import").click(function () {
                var filename = $("#ful_FileUpLoad").val();
                if (filename.length > 0) {
                    return true;
                } else {
                    alert("Select the files you want to upload first.");
                    return false;
                }
            });
        });
    </script>
</asp:Content>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <form id="form1" runat="server">

        <%-- Status section --%>
        <div class="section">
            <h2 class="section-title">Notifier Status</h2>
            <asp:Label ID="lbl_Contacts" runat="server" CssClass="field-note"></asp:Label>

            <asp:Table ID="tbl_TableContactList" runat="server" EnableViewState="false"
                CssClass="data-table" style="margin-bottom: 16px">
                <asp:TableRow></asp:TableRow>
            </asp:Table>

            <asp:Table ID="tbl_TableNotifierStatus" runat="server" EnableViewState="false"
                CssClass="data-table">
                <asp:TableRow></asp:TableRow>
            </asp:Table>
        </div>

        <hr class="section-divider" />

        <%-- Import section --%>
        <div class="section">
            <h2 class="section-title">Import Excel Files to Notifier</h2>

            <div class="field-group">
                <label for="ful_FileUpLoad">Select Excel file(s) to import:</label>
                <asp:FileUpload runat="server" AllowMultiple="true" ID="ful_FileUpLoad" />
            </div>

            <div class="field-group">
                <label for="txt_SheetNames">Worksheet names (optional, comma-separated — leave blank to import all):</label>
                <asp:TextBox ID="txt_SheetNames" runat="server" Width="100%"></asp:TextBox>
            </div>

            <asp:Button ID="btn_Import" runat="server" Text="Import"
                OnClick="btn_Import_Click" CssClass="btn" />
        </div>

    </form>
</asp:Content>
