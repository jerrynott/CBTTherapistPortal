<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Tables.aspx.cs" Inherits="TherapistPortal.Tables" MasterPageFile="~/Site.Master" %>

<asp:Content ContentPlaceHolderID="HeadContent" runat="server">
    <script type="text/javascript">
        $(document).ready(function () {
            $("#btn_Import").click(function () {
                var containerName = $("#txt_TableName").val();
                var patrn = /^[a-z]([a-z0-9])*$/;
                var result = patrn.test(containerName);
                if (!result) {
                    $("#lbl_NameError").css("visibility", "visible");
                    return false;
                } else {
                    if (containerName.length >= 3 && containerName.length <= 63) {
                        $("#lbl_NameError").css("visibility", "hidden");
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
</asp:Content>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <form id="form1" runat="server">

        <%-- Database mode toggle --%>
        <div class="db-mode-bar db-mode-<%= CurrentDbMode %>">
            <span class="db-mode-label">Target Database:</span>
            <asp:Button ID="btn_Dev"  runat="server" Text="Dev"  OnClick="btn_Dev_Click" />
            <asp:Button ID="btn_Prod" runat="server" Text="Prod" OnClick="btn_Prod_Click" />
        </div>

        <%-- Export section --%>
        <div class="section">
            <h2 class="section-title">Download Azure Tables to Excel</h2>
            <ol class="section-instructions">
                <li>Select the storage table(s) you want to export.</li>
                <li>Optionally enter an OData query string to filter results.</li>
                <li>Optionally check "Preview table details" to view 10 records per table.</li>
                <li>Click <strong>Download Excel</strong>.</li>
            </ol>

            <div class="field-group">
                <asp:CheckBoxList ID="ckb_TableName" runat="server" AutoPostBack="true"
                    Font-Size="15px" CssClass="checkbox-list">
                </asp:CheckBoxList>
            </div>

            <div class="field-group">
                <label for="txt_FilterString">Filter:</label>
                <asp:TextBox ID="txt_FilterString" runat="server" Width="100%"
                    AutoPostBack="true" CssClass="filter-input"></asp:TextBox>
                <div></div>
                <span class="field-note">
                    Example: <code><asp:Label ID="lbl_Example" runat="server">Timestamp ge datetime'2017-05-31T23:59:59Z'</asp:Label></code>
                </span>
            </div>

            <div class="field-group">
                <asp:CheckBox ID="chk_ShowDetails" runat="server" Checked="false"
                    Text="Preview table details" Font-Size="15px" AutoPostBack="true"
                    EnableViewState="true" />
                <asp:Table ID="tbl_TableDetailList" runat="server" EnableViewState="true"
                    CssClass="data-table" style="margin-top: 12px">
                    <asp:TableRow></asp:TableRow>
                </asp:Table>
            </div>

            <asp:Button ID="btn_ExportData" runat="server" Text="Download Excel"
                OnClick="btn_ExportData_Click" CssClass="btn" />
        </div>

        <hr class="section-divider" />

        <%-- Import section --%>
        <div class="section db-mode-<%= CurrentDbMode %>">
            <h2 class="section-title">Upload Excel Files to an Azure</h2>
            <ol class="section-instructions">
                <li>Enter the name of the destination storage table.</li>
                <li>Table names must be 3-63 characters, start with a letter, and contain only alphanumeric characters.</li>
                <li>Table names are case-sensitive.</li>
            </ol>

            <div class="field-group">
                <label for="txt_TableName">Table name:</label>
                <asp:TextBox ID="txt_TableName" ReadOnly="true" runat="server"
                    CssClass="readonly">content</asp:TextBox>
                <span id="lbl_NameError" class="error-label">Table name is invalid, please check it.</span>
            </div>

            <div class="field-group">
                <label for="ful_FileUpLoad">Select Excel file(s) to import:</label>
                <asp:FileUpload runat="server" AllowMultiple="true" ID="ful_FileUpLoad" />
            </div>

            <div class="field-group">
                <label for="txt_SheetNames">Worksheet names (optional, comma-separated - leave blank to import all):</label>
                <asp:TextBox ID="txt_SheetNames" runat="server" Width="100%"></asp:TextBox>
            </div>

            <asp:Button ID="btn_Import" runat="server" Text="Upload to Azure"
                OnClick="btn_Import_Click" CssClass="btn" />
        </div>

    </form>
</asp:Content>
