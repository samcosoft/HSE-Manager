using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using MudBlazor;
using Syncfusion.Blazor.Grids;
using Action = Syncfusion.Blazor.Grids.Action;

namespace Samco_HSE_Manager.Pages.Officer;

public partial class Vehicles : IDisposable
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private Session Session1 { get; set; } = null!;
    private IEnumerable<Vehicle>? VehiclesList { get; set; }
    private IEnumerable<Samco_HSE.HSEData.Personnel>? DriverList { get; set; }
    private IEnumerable<Rig>? Rigs { get; set; }

    private IEnumerable<string> _category = null!;
    private SfGrid<Vehicle>? VehicleGrid { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
        _category = await File.ReadAllLinesAsync(Path.Combine(HostEnvironment.WebRootPath, "content",
            "VehicleTypes.txt"));
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
            DriverList = await Session1.Query<Samco_HSE.HSEData.Personnel>().Where(x =>
                loggedUser.Rigs.Contains(x.ActiveRig) &&
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

    private void Customize_Row(RowDataBoundEventArgs<Vehicle> args)
    {
        var lastCheck = args.Data.LastCheckDate;
        var insurance = args.Data.InsuranceDate;
        if (lastCheck.HasValue && lastCheck.Value.AddMonths(-3) < DateTime.Today ||
            insurance.HasValue && insurance.Value.AddMonths(-3) < DateTime.Today)
        {
            args.Row.AddClass(new[] { "warning-item" });
        }

        if (lastCheck.HasValue && lastCheck.Value < DateTime.Today ||
            insurance.HasValue && insurance.Value < DateTime.Today)
        {
            args.Row.AddClass(new[] { "danger-item" });
        }
    }

    #region VehicleGrid

    private void VehicleGrid_Action(ActionEventArgs<Vehicle> e)
    {
        switch (e.RequestType)
        {
            case Action.Add:
                e.Data = new Vehicle(Session1);
                break;
            case Action.BeginEdit:
                e.Data = Session1.GetObjectByKey<Vehicle>(e.RowData.Oid);
                break;
            case Action.Save:
                var editModel = e.Data;
                //Validation
                if (string.IsNullOrEmpty(editModel.Name) || string.IsNullOrEmpty(editModel.PlateNo) ||
                    editModel.RigNo == null)
                {
                    Snackbar.Add("لطفاً موارد الزامی را تکمیل کنید.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                //Check equipment not existed before
                var selVehicle =
                    Session1.FindObject<Vehicle>(new BinaryOperator(nameof(Vehicle.PlateNo), editModel.PlateNo));

                if (selVehicle != null && selVehicle.Oid != editModel.Oid)
                {
                    //Equipment existed
                    Snackbar.Add(
                        "ماشین با همین پلاک در سیستم وجود دارد. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.",
                        Severity.Error);
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

    private async Task VehicleToolbarClickHandler(Syncfusion.Blazor.Navigations.ClickEventArgs args)
    {
        if (args.Item.Id == "vehicleGrid_Excel Export")
        {
            Snackbar.Add("سیستم در حال ایجاد فایل است. لطفاً تا دانلود گزارش شکیبا باشید...", Severity.Info);
            var exportProperties = new ExcelExportProperties
            {
                FileName = "VehicleList.xlsx"
            };
            await VehicleGrid!.ExportToExcelAsync(exportProperties);
        }
    }

    #endregion
}