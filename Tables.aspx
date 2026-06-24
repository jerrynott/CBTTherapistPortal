<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Tables.aspx.cs" Inherits="TherapistPortal.Tables"
    MasterPageFile="~/Site.Master" %>

    <asp:Content ContentPlaceHolderID="HeadContent" runat="server">
        <script type="text/javascript">
            $(document).ready(function () {
                var mediaFiles = new DataTransfer();

                function formatSize(bytes) {
                    if (bytes < 1048576) return (bytes / 1024).toFixed(1) + " KB";
                    return (bytes / 1048576).toFixed(1) + " MB";
                }

                /// <summary>
                /// Removes a file from the selected media files list based on the given index. This function creates a new DataTransfer object, adds all files except the one to be removed, updates the file input's files property, and re-renders the file list to reflect the change.
                /// </summary>
                function removeFile(index) {
                    var dt = new DataTransfer();
                    $.each(mediaFiles.files, function (i, f) {
                        if (i !== index) dt.items.add(f);
                    });
                    mediaFiles = dt;
                    document.getElementById("<%= media_Upload.ClientID %>").files = dt.files;
                    renderFileList();
                }
                /// <summary>
                /// Renders the list of selected media files with their names, sizes and a remove button for each file. This function is called after selecting files and after removing a file to update the displayed file list.
                /// </summary>
                function renderFileList() {
                    var list = $("#media_file_list").empty();
                    $.each(mediaFiles.files, function (i, f) {
                        var li = $("<li>").css({
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "space-between",
                            padding: "6px 10px",
                            border: "1px solid #ccc",
                            borderRadius: "6px",
                            listStyle: "none",
                            fontSize: "13px"
                        }).text(f.name + " (" + formatSize(f.size) + ")");
                        $("<button>").attr("type", "button").html("&times;").css({
                            background: "none",
                            border: "none",
                            color: "#888",
                            fontSize: "16px",
                            cursor: "pointer",
                            lineHeight: "1",
                            padding: "0 2px",
                            flexShrink: "0"
                        }).on("click", (function (idx) { return function () { removeFile(idx); }; })(i))
                            .appendTo(li);
                        list.append(li);
                    });
                }

                /// <summary>
                /// Handles the change event of the media file input. When files are selected, it adds the new files to the mediaFiles DataTransfer object while preventing duplicates, updates the file input's files property to reflect the current list of selected files, and calls renderFileList to update the displayed file list.
                /// </summary>
                $("#<%= media_Upload.ClientID %>").on("change", function () {
                    var existing = {};
                    $.each(mediaFiles.files, function (_, f) { existing[f.name] = true; });
                    $.each(this.files, function (_, f) {
                        if (!existing[f.name]) mediaFiles.items.add(f);
                    });
                    this.files = mediaFiles.files;
                    renderFileList();
                });

                /// <summary>
                /// Handles the click event of the Import button. It validates the table name input against Azure Table storage naming rules, checks if files are selected for upload, prompts for confirmation if uploading to prod, and returns true to allow form submission if all validations pass. Otherwise, it shows appropriate error messages and returns false to prevent form submission.
                /// </summary>
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
                                var mode = $('#<%= hf_DbMode.ClientID %>').val();
                                console.log(`Mode: ${mode}`);
                                if (mode === 'prod') {
                                    if (!confirm('You are about to import into PRODUCTION. Are you sure?')) {
                                        return false;
                                    }
                                }
                                return true;
                            } else {
                                var mode = $('#<%= hf_DbMode.ClientID %>').val();
                                console.log(`Mode: ${mode}`);
                                debugger;
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

            <asp:HiddenField ID="hf_DbMode" runat="server" />

            <%-- Database mode toggle --%>
                <div class="db-mode-bar db-mode-<%= CurrentDbMode %>">
                    <span class="db-mode-label">Target Database:</span>
                    <asp:Button ID="btn_Dev" runat="server" Text="Dev" OnClick="btn_Dev_Click" />
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
                            <asp:CheckBoxList ID="ckb_TableName" runat="server" AutoPostBack="true" Font-Size="15px"
                                CssClass="checkbox-list">
                            </asp:CheckBoxList>
                        </div>

                        <div class="field-group">
                            <label for="txt_FilterString">Filter:</label>
                            <asp:TextBox ID="txt_FilterString" runat="server" Width="100%" AutoPostBack="true"
                                CssClass="filter-input"></asp:TextBox>
                            <div></div>
                            <span class="field-note">
                                Example:
                                <code><asp:Label ID="lbl_Example" runat="server">Timestamp ge datetime'2017-05-31T23:59:59Z'</asp:Label></code>
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
                            <h2 class="section-title">Upload Excel Files to Azure <u>
                                    <%= CurrentDbMode=="prod" ? "Prod" : "Dev" %>
                                </u> Database</h2>
                            <ol class="section-instructions">
                                <li>Enter the name of the destination storage table.</li>
                                <li>Table names must be 3-63 characters, start with a letter, and contain only
                                    alphanumeric characters.</li>
                                <li>Table names are case-sensitive.</li>
                            </ol>

                            <div class="field-group">
                                <label for="txt_TableName">Table name:</label>
                                <asp:TextBox ID="txt_TableName" ReadOnly="true" runat="server" CssClass="readonly">content</asp:TextBox>
                                <span id="lbl_NameError" class="error-label">Table name is invalid, please check
                                    it.</span>
                            </div>

                            <div class="field-group">
                                <label for="ful_FileUpLoad">Select Excel file(s) to import:</label>
                                <asp:FileUpload runat="server" AllowMultiple="true" ID="ful_FileUpLoad" />
                            </div>

                            <div class="field-group">
                                <label for="txt_SheetNames">Worksheet names (optional, comma-separated - leave blank to
                                    import all):</label>
                                <asp:TextBox ID="txt_SheetNames" runat="server" Width="100%"></asp:TextBox>
                            </div>

                            <asp:Button ID="btn_Import" runat="server" Text="Upload to Azure" OnClick="btn_Import_Click"
                                CssClass="btn" />
                        </div>

                        <%-- Import media --%>
                            <div class="section">
                                <h2 class="section-title">Upload Media Files to Azure Database</h2>
                                <ol class="section-instructions">
                                    <li>Select the media files to Upload to the server after pressing the "Choose files"
                                        button</li>
                                    <li>Make sure the uploaded file list is correct</li>
                                    <li>Click <b>Upload to Azure</b></li>
                                </ol>

                                <div class="field-group">
                                    <label for="media_Upload">Select media files to upload:</label>
                                    <asp:FileUpload runat="server" AllowMultiple="true" ID="media_Upload" />
                                    <ul id="media_file_list"
                                        style="margin-top: 6px; padding: 0; list-style: none; display: flex; flex-direction: column; gap: 6px;">
                                    </ul>
                                </div>

                                <asp:Button ID="btn_Upload" runat="server" Text="Upload to Azure"
                                    OnClick="btn_Media_Upload" CssClass="btn" />
                            </div>

        </form>
    </asp:Content>