using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
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

    private Session Session1 { get; set; } = null!;
    private IEnumerable<Rig>? Rigs { get; set; }
    private XPCollection<Report>? ReportList { get; set; }
    private XPCollection<HSEForm>? FormCollection { get; set; }
    private SfGrid<Report>? ReportGrid { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
        if (SamcoSoftShared.CurrentUserRole == SamcoSoftShared.SiteRoles.Admin)
        {
            var loggedUser =
                await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
            Rigs = loggedUser.Rigs;
            ReportList = loggedUser.Reports;
        }
        else
        {
            //Owner
            FormCollection = new XPCollection<HSEForm>(Session1);
            ReportList = new XPCollection<Report>(Session1);
            Rigs = await Session1.Query<Rig>().ToListAsync();
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


}