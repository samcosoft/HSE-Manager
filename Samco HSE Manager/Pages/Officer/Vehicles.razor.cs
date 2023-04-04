using BootstrapBlazor.Components;
using DevExpress.Blazor;
using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using System.Diagnostics.CodeAnalysis;

namespace Samco_HSE_Manager.Pages.Officer;

public partial class Vehicles : IDisposable
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;
    [Inject]
    [NotNull]
    private ToastService? ToastService { get; set; }

    private Session Session1 { get; set; } = null!;
    private IEnumerable<Vehicle>? VehiclesList { get; set; }
    private IEnumerable<Samco_HSE.HSEData.Personnel>? DriverList { get; set; }
    private IEnumerable<Rig>? Rigs { get; set; }

    private IEnumerable<string> _category = null!;
    private DxGrid? VehicleGrid { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
        _category = await File.ReadAllLinesAsync(Path.Combine(HostEnvironment.WebRootPath, "content", "VehicleTypes.txt"));
        await LoadInformation();
    }

    public void Dispose()
    {
        Session1.Dispose();
    }

    private async Task LoadInformation()
    {
        if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
        {
            var loggedUser =
                await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));

            VehiclesList = Session1.Query<Vehicle>().Where(x => loggedUser.Rigs.Contains(x.RigNo));
            DriverList = await Session1.Query<Samco_HSE.HSEData.Personnel>().Where(x => loggedUser.Rigs.Contains(x.ActiveRig) &&
                x.CurrentRole.Contains("راننده") || x.CurrentRole.Contains("driver")).ToListAsync();
            Rigs = loggedUser.Rigs;
        }
        else
        {
            //Owner
            VehiclesList = Session1.Query<Vehicle>();
            DriverList = await Session1.Query<Samco_HSE.HSEData.Personnel>()
                .Where(x => x.CurrentRole.Contains("راننده") || x.CurrentRole.Contains("driver")).ToListAsync();
            Rigs = await Session1.Query<Rig>().ToListAsync();
        }
    }

    private void VehicleGrid_CustomizeElement(GridCustomizeElementEventArgs e)
    {
        if (e.ElementType == GridElementType.DataRow)
        {
            var lastCheck = (DateTime?)e.Grid.GetRowValue(e.VisibleIndex, nameof(Vehicle.LastCheckDate));
            var insurance = (DateTime?)e.Grid.GetRowValue(e.VisibleIndex, nameof(Vehicle.InsuranceDate));
            if (lastCheck.HasValue && lastCheck.Value.AddMonths(-3) < DateTime.Today || insurance.HasValue && insurance.Value.AddMonths(-3) < DateTime.Today)
            {
                e.CssClass = "warning-item";
            }
            if (lastCheck.HasValue && lastCheck.Value < DateTime.Today || insurance.HasValue && insurance.Value < DateTime.Today)
            {
                e.CssClass = "danger-item";
            }
        }
    }

    #region VehicleGrid

    private void VehicleEditModel(GridCustomizeEditModelEventArgs e)
    {
        var dataItem = (Vehicle?)e.DataItem ?? new Vehicle(Session1);
        e.EditModel = dataItem;
    }
    private async Task OnEditModelSaving(GridEditModelSavingEventArgs e)
    {
        var editModel = (Vehicle)e.EditModel;
        //Validation
        if (string.IsNullOrEmpty(editModel.Name) || string.IsNullOrEmpty(editModel.PlateNo) || editModel.RigNo == null)
        {
            await ToastService.Error("خطا در افزودن ماشین", "لطفاً موارد الزامی را تکمیل کنید.");
            e.Cancel = true;
            return;
        }
        //Check equipment not existed before
        var selVehicle = Session1.FindObject<Vehicle>(new BinaryOperator(nameof(Vehicle.PlateNo), editModel.PlateNo));
        if (e.IsNew)
        {
            if (selVehicle != null)
            {
                //Vehicle existed
                await ToastService.Error("خطا در ثبت اطلاعات",
                    "ماشین با همین پلاک در سیستم وجود دارد. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.");
                e.Cancel = true;
                return;
            }
        }
        else
        {
            if (selVehicle != null && selVehicle.Oid != editModel.Oid)
            {
                //Equipment existed
                await ToastService.Error("خطا در ثبت اطلاعات",
                    "ماشین با همین پلاک در سیستم وجود دارد. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.");
                e.Cancel = true;
                return;
            }
        }
        editModel.Save();
        await LoadInformation();
    }
    private async Task OnDataItemDeleting(GridDataItemDeletingEventArgs e)
    {
        var dataItem =
            await Session1.FindObjectAsync<Vehicle>(new BinaryOperator("Oid", (e.DataItem as Vehicle)!.Oid));
        dataItem?.Delete();
        await LoadInformation();
    }

    private async Task OnPrintBtnClick()
    {
        //await ToastService.Information("دریافت گزارش", "سیستم در حال ایجاد فایل است. لطفاً شکیبا باشید...");
        await ToastService.Show(new ToastOption
        {
            Content = "سیستم در حال ایجاد فایل است. لطفاً تا دانلود گزارش شکیبا باشید...",
            Title = "دریافت گزارش",
            Category = ToastCategory.Information,
            Delay = 6000,
            ForceDelay = true
        });

        //var getReport = PersonnelGrid?.ExportToXlsxAsync("Personnel", new GridXlExportOptions
        await VehicleGrid?.ExportToXlsxAsync("Vehicles", new GridXlExportOptions
        {
            CustomizeSheet = SamcoSoftShared.CustomizeSheet,
            CustomizeCell = SamcoSoftShared.CustomizeCell,
            CustomizeSheetFooter = SamcoSoftShared.CustomizeFooter
        })!;
    }
    #endregion
}