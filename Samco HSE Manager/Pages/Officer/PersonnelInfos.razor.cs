using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Samco_HSE.HSEData;
using Syncfusion.Blazor.Grids;
using Action = Syncfusion.Blazor.Grids.Action;

namespace Samco_HSE_Manager.Pages.Officer
{
    public partial class PersonnelInfos : IDisposable
    {
        [Parameter] public int UserOid { get; set; }
        [Inject] private IDataLayer DataLayer { get; set; } = null!;
        [Inject] private ISnackbar Snackbar { get; set; } = null!;
        private SfGrid<Incentive>? IncentiveGrid { get; set; }
        private SfGrid<Warning>? WarningGrid { get; set; }
        private Session Session1 { get; set; } = null!;
        private Samco_HSE.HSEData.Personnel? SelUser { get; set; }
        
        protected override async Task OnInitializedAsync()
        {
            Session1 = new Session(DataLayer);
            SelUser = await Session1.GetObjectByKeyAsync<Samco_HSE.HSEData.Personnel>(UserOid);
        }

        #region Incentives
        private async Task IncentiveGrid_Action(ActionEventArgs<Incentive> e)
        {
            switch (e.RequestType)
            {
                case Action.Add:
                    e.Data ??= new Incentive(Session1);
                    break;
                case Action.BeginEdit:
                    e.Data = await Session1.GetObjectByKeyAsync<Incentive>(e.RowData.Oid);
                    break;
                case Action.Save:
                    var editModel = e.Data;
                    //Validation
                    if (string.IsNullOrEmpty(editModel.Reason) || editModel.IssueDate.Year < 1900)
                    {
                        Snackbar.Add("لطفاً موارد الزامی را تکمیل کنید.", Severity.Error);
                        e.Cancel = true;
                        return;
                    }
                    
                    var wellWork = Session1.Query<WellWork>().FirstOrDefault(x => x.RigNo.Oid == SelUser!.ActiveRig.Oid &&
                        x.IsActive);

                    if (wellWork == null)
                    {
                        Snackbar.Add("دکل این فرد در هیچ پروژه‌ای فعال نیست.", Severity.Error);
                        e.Cancel = true;
                        return;
                    }

                    editModel.Save();
                    break;
                case Action.Delete:
                    e.RowData.Delete();
                    break;
            }
        }

        private async Task IncentiveGridToolbarClickHandler(Syncfusion.Blazor.Navigations.ClickEventArgs args)
        {
            if (args.Item.Id == "incentiveGrid_Excel Export")
            {
                Snackbar.Add("سیستم در حال ایجاد فایل است. لطفاً تا دانلود گزارش شکیبا باشید...", Severity.Info);
                var exportProperties = new ExcelExportProperties
                {
                    FileName = "IncentiveList.xlsx"
                };
                await IncentiveGrid!.ExportToExcelAsync(exportProperties);
            }
        }

        #endregion

        #region Warnings

           private async Task WarningGrid_Action(ActionEventArgs<Warning> e)
        {
            switch (e.RequestType)
            {
                case Action.Add:
                    e.Data ??= new Warning(Session1);
                    break;
                case Action.BeginEdit:
                    e.Data = await Session1.GetObjectByKeyAsync<Warning>(e.RowData.Oid);
                    break;
                case Action.Save:
                    var editModel = e.Data;
                    //Validation
                    if (string.IsNullOrEmpty(editModel.Reason) || editModel.IssueDate.Year < 1900)
                    {
                        Snackbar.Add("لطفاً موارد الزامی را تکمیل کنید.", Severity.Error);
                        e.Cancel = true;
                        return;
                    }
                    
                    var wellWork = Session1.Query<WellWork>().FirstOrDefault(x => x.RigNo.Oid == SelUser!.ActiveRig.Oid &&
                        x.IsActive);

                    if (wellWork == null)
                    {
                        Snackbar.Add("دکل این فرد در هیچ پروژه‌ای فعال نیست.", Severity.Error);
                        e.Cancel = true;
                        return;
                    }

                    editModel.Save();
                    break;
                case Action.Delete:
                    e.RowData.Delete();
                    break;
            }
        }

        private async Task WarningGridToolbarClickHandler(Syncfusion.Blazor.Navigations.ClickEventArgs args)
        {
            if (args.Item.Id == "warningGrid_Excel Export")
            {
                Snackbar.Add("سیستم در حال ایجاد فایل است. لطفاً تا دانلود گزارش شکیبا باشید...", Severity.Info);
                var exportProperties = new ExcelExportProperties
                {
                    FileName = "WarningList.xlsx"
                };
                await WarningGrid!.ExportToExcelAsync(exportProperties);
            }
        }

        #endregion

        public void Dispose()
        {
            DataLayer.Dispose();
            Snackbar.Dispose();
            Session1.Dispose();
        }
    }
}