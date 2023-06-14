using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using MudBlazor;
using Syncfusion.Blazor.Grids;
using Syncfusion.Blazor.Popups;
using Action = Syncfusion.Blazor.Grids.Action;

namespace Samco_HSE_Manager.Pages.Medic;

public partial class Drugs
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private IConfiguration Configuration { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private Session Session1 { get; set; } = null!;
    private IEnumerable<Rig> Rigs { get; set; } = null!;

    private IEnumerable<Medication>? MedicineList { get; set; }

    private readonly IEnumerable<string> _category = new List<string>
    {
        "دارو", "تجهیز"
    };

    private readonly IEnumerable<string> _drugFormList = new List<string>
    {
        "Ampule", "Syrup", "Tablet", "Otic Drop", "Ophthalmic Ointment",
        "Ophthalmic Drop", "Bottle", "Suppository", "Vial", "Spray", "Nasal Spray",
        "Inhaler", "Topical Gel", "Suspension", "Sub-lingual Tablet", "Cream",
        "Ointment", "Oral Drop", "Pearl", "Sachet", "Ophthalmic Solution", "IV Fluid",
        "Effervescent Tablet",
        "Pack", "Pads", "Support", "Splint", "Roll", "Tools", "Bandage", "Tube", "Plaster",
        "Catheter", "Airway", "Syringe", "Suture", "Gauze", "Dressing"
    };

    private SfGrid<Medication>? MedicineGrid { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
        if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
        {
            var loggedUser =
                Session1.FindObject<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
            Rigs = loggedUser.Rigs;
        }
        else
        {
            //Owner
            Rigs = Session1.Query<Rig>().ToList();
        }

        MedicineList = await Session1.Query<Medication>().ToListAsync();
    }

    #region MedicationGrid

    private string MedicGridUnbound(int rigOid, Medication medic)
    {
        using var tempSession = new Session(DataLayer);
        var currentMedication =
            tempSession.FindObject<Medication>(new BinaryOperator("Oid", medic.Oid));
        var medStock = tempSession.Query<MedicationStock>()
            .FirstOrDefault(itm => itm.RigNo.Oid == rigOid && itm.MedicName.Oid == currentMedication.Oid);
        return medStock?.AvailCount.ToString() ?? "0";
    }

    private async Task MedicGrid_Action(ActionEventArgs<Medication> e)
    {
        switch (e.RequestType)
        {
            case Action.Add:
                if (SamcoSoftShared.CurrentUserRole > SamcoSoftShared.SiteRoles.Supervisor)
                {
                    Snackbar.Add("شما اجازه ویرایش دارو و تجهیزات را ندارید.", Severity.Warning);
                    e.Cancel = true;
                }

                e.Data ??= new Medication(Session1);
                break;
            case Action.BeginEdit:
                if (SamcoSoftShared.CurrentUserRole > SamcoSoftShared.SiteRoles.Supervisor)
                {
                    Snackbar.Add("شما اجازه ویرایش دارو و تجهیزات را ندارید.", Severity.Warning);
                    e.Cancel = true;
                }

                e.Data = await Session1.GetObjectByKeyAsync<Medication>(e.RowData.Oid);
                break;
            case Action.Save:
                var editModel = e.Data;
                //Validation
                if (string.IsNullOrEmpty(editModel.Name) || string.IsNullOrEmpty(editModel.Dose) ||
                    string.IsNullOrEmpty(editModel.DrugForm) || string.IsNullOrEmpty(editModel.Category))
                {
                    Snackbar.Add("لطفاً موارد الزامی را تکمیل کنید.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                //Check drug not existed before
                var selMedication = await Session1.Query<Medication>().FirstOrDefaultAsync(x =>
                    x.Name == editModel.Name &&
                    x.Dose == editModel.Dose &&
                    x.DrugForm == editModel.DrugForm);

                if (selMedication != null && selMedication.Oid != editModel.Oid)
                {
                    //Medication existed
                    Snackbar.Add(
                        "دارو / تجهیز با همین مشخصات در سیستم موجود است. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.",
                        Severity.Error);
                    e.Cancel = true;
                    return;
                }

                editModel.Save();
                break;
            case Action.Delete:
                if (SamcoSoftShared.CurrentUserRole > SamcoSoftShared.SiteRoles.Supervisor)
                {
                    Snackbar.Add("شما اجازه ویرایش دارو و تجهیزات را ندارید.", Severity.Warning);
                    e.Cancel = true;
                    return;
                }

                e.RowData.Delete();
                break;
        }
    }

    private async Task MedicToolbarClickHandler(Syncfusion.Blazor.Navigations.ClickEventArgs args)
    {
        if (args.Item.Id == "medicGrid_Excel Export")
        {
            Snackbar.Add("سیستم در حال ایجاد فایل است. لطفاً تا دانلود گزارش شکیبا باشید...", Severity.Info);
            var exportProperties = new ExcelExportProperties
            {
                FileName = "MedicationList.xlsx"
            };
            await MedicineGrid!.ExportToExcelAsync(exportProperties);
        }
    }
    #endregion

    #region MedicationCount

    private SfDialog? SetNumberModal { get; set; }
    private MedicationStock? _selMedicationStock;

    private async Task OnSetNumberBtnClick()
    {
        if (MedicineGrid!.SelectedRecords.Any() == false)
        {
            Snackbar.Add("لطفاً یک دارو / تجهیز را از لیست زیر انتخاب کنید.", Severity.Warning);
            return;
        }

        _selMedicationStock = new MedicationStock(Session1) { MedicName = MedicineGrid!.SelectedRecords.First() };
        //auto select rig
        await SetNumberModal!.ShowAsync();
    }

    private void RigSelectionChanged(Rig itm)
    {
        //Change data source if needed
        var selMedication = MedicineGrid!.SelectedRecords.FirstOrDefault();
        if (selMedication == null) return;
        //Get stock items
        var stockItm = Session1.Query<MedicationStock>().Where(x => x.RigNo.Oid == (itm).Oid &&
                                                                    x.MedicName.Oid == selMedication.Oid);
        if (stockItm.Any())
        {
            _selMedicationStock = stockItm.First();
        }
        else
        {
            _selMedicationStock = new MedicationStock(Session1)
            {
                MedicName = MedicineGrid!.SelectedRecords.First(),
                RigNo = itm
            };
        }
    }

    private async Task SetCountOkBtnClick()
    {
        //Validation
        if (_selMedicationStock?.RigNo == null)
        {
            Snackbar.Add("لطفاً یک دکل را انتخاب کنید.", Severity.Error);
            return;
        }

        _selMedicationStock?.Save();
        Snackbar.Add("اطلاعات با موفقیت ثبت شد.", Severity.Success);
        await MedicineGrid!.Refresh();
        await SetNumberModal!.HideAsync();
    }

    #endregion

    #region DrugRequest
    private SfDialog? DrugRequestModal { get; set; }
    private Rig? _selRig;

    private async Task OnDrugRequestBtnClick()
    {
        await DrugRequestModal!.ShowAsync();
    }

    private void RequestBtnClick()
    {
        if (_selRig == null)
        {
            Snackbar.Add("لطفاً یک دکل را انتخاب کنید.", Severity.Error);
            return;
        }

        var parameter =
            $"ReportName=MedicineRequest&Parameters=RigId--{_selRig.Oid}|Title--شرکت {Configuration["CompanyInfo:Name"]} - دکل {_selRig.Name}";
        NavigationManager.NavigateTo("report?" + parameter);
        DrugRequestModal?.HideAsync();
    }
    #endregion
}