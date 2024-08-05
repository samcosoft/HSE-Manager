using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Samco_HSE.HSEData;
using Syncfusion.Blazor.Grids;
using Action = Syncfusion.Blazor.Grids.Action;

namespace Samco_HSE_Manager.Pages.Shared.Reports;

public partial class ReportsView
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;

    private Session Session1 { get; set; } = null!;
    private XPCollection<Report>? ReportList { get; set; }
    private SfGrid<Report>? ReportGrid { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
        if (SamcoSoftShared.CurrentUserRole > SamcoSoftShared.SiteRoles.Admin)
        {
            var loggedUser =
                await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
            ReportList = loggedUser.Reports;
        }
        else
        {
            //Owner
            ReportList = new XPCollection<Report>(Session1);
        }
    }

    private void ReportGrid_Action(ActionEventArgs<Report> e)
    {
        switch (e.RequestType)
        {
            case Action.Delete:
                {
                    var dataItem = e.RowData;
                    dataItem.Delete();
                    break;
                }
        }
    }

    private async Task ToolbarClickHandler(Syncfusion.Blazor.Navigations.ClickEventArgs args)
    {
        if (args.Item.Id == "reportGrid_Excel Export")
        {
            Snackbar.Add("سیستم در حال ایجاد فایل است. لطفاً تا دانلود گزارش شکیبا باشید...", Severity.Info);
            var exportProperties = new ExcelExportProperties
            {
                FileName = "ReportList.xlsx"
            };
            await ReportGrid!.ExportToExcelAsync(exportProperties);
        }
    }

    private async Task OnAddBtnClick(MouseEventArgs obj)
    {
        await DialogService.ShowAsync<NewReportModal>("افزودن گزارش جدید", new DialogOptions { FullWidth = true });
    }

    private async Task OnEditBtnClick(MouseEventArgs obj)
    {
        if (ReportGrid!.SelectedRecords.Count == 0)
        {
            Snackbar.Add("لطفاً یک گزارش را انتخاب کنید.", Severity.Error);
            return;
        }

        var newReport = ReportGrid!.SelectedRecords[0];
        var destPath = Path.Combine(HostEnvironment.WebRootPath, "upload", "UserReports", SamcoSoftShared.CurrentUserId.ToString(), $"{newReport.Oid}.{newReport.Form.FormType}");
        if (!File.Exists(destPath))
        {
            Snackbar.Add("فایل فرم یافت نشد.", Severity.Error);
            return;
        }
        //Open report for editing
        switch (newReport.Form.FormType.ToLower())
        {
            case "pdf":
                var parameter1 = new DialogParameters<PDFViewer>
                {
                    { x => x.DocumentPath, destPath },
                    { x => x.ReportId, newReport.Oid }
                };
                await DialogService.ShowAsync<PDFViewer>($"گزارش {newReport.Form.Title}", parameter1, new DialogOptions { FullScreen = true });
                break;
            case "doc":
            case "docx":
                var parameter2 = new DialogParameters<WordViewer>
                {
                    { x => x.DocumentPath, destPath },
                    { x => x.ReportId, newReport.Oid }
                };
                await DialogService.ShowAsync<WordViewer>($"گزارش {newReport.Form.Title}", parameter2, new DialogOptions { FullScreen = true });
                break;
        }
    }
}