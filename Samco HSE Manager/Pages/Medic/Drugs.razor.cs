using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using MudBlazor;
using Samco_HSE_Manager.Pages.Medic.MedicationModals;
using Syncfusion.Blazor.Grids;
using Action = Syncfusion.Blazor.Grids.Action;

namespace Samco_HSE_Manager.Pages.Medic;

public partial class Drugs : IDisposable
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private IConfiguration Configuration { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;

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
        MedicineList = await Session1.Query<Medication>().ToListAsync();
        if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
        {
            var loggedUser =
                Session1.FindObject<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
            Rigs = loggedUser.Rigs;
            //DiscardList = await Session1.Query<DisposedMedicine>().Where(x => loggedUser.Rigs.Contains(x.RigNo)).ToListAsync();
            var _rigOids = loggedUser.Rigs.Select(x => x.Oid).ToList();
            DiscardList = new XPCollection<DisposedMedicine>(Session1, CriteriaOperator.FromLambda<DisposedMedicine, bool>(x => _rigOids.Contains(x.RigNo.Oid)));

        }
        else
        {
            //Owner
            Rigs = await Session1.Query<Rig>().ToListAsync();
            //DiscardList = await Session1.Query<DisposedMedicine>().ToListAsync();
            DiscardList = new XPCollection<DisposedMedicine>(Session1, true);
        }
    }

    #region MedicationGrid

    private string MedicGridUnbound(int rigOid, Medication? medic)
    {
        if (medic == null) return "0";
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

                e.Data = new Medication(Session1) { AvailForOrder = true };
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

    private async Task OnSetNumberBtnClick()
    {
        if (MedicineGrid!.SelectedRecords.Any() == false)
        {
            Snackbar.Add("لطفاً یک دارو / تجهیز را از لیست زیر انتخاب کنید.", Severity.Warning);
            return;
        }

        var selMedicine = MedicineGrid!.SelectedRecords.First();

        var dialog = await DialogService.ShowAsync<NumberModal>($"ثبت موجودی برای {selMedicine.Name}",
            new DialogParameters { { "SelMedicationId", selMedicine.Oid } });
        var result = await dialog.Result;
        if (!result!.Canceled)
            StateHasChanged();
    }

    #endregion

    #region DrugRequest

    private async Task OnDrugRequestBtnClick()
    {
        await DialogService.ShowAsync<DrugRequestModal>("درخواست دارو");
    }

    #endregion

    public void Dispose()
    {
        Session1.Dispose();
        ((IDisposable)MedicineGrid!).Dispose();
    }

    private async Task OnDrugDiscardBtnClick()
    {
        var dialog = await DialogService.ShowAsync<MedicationDiscardModal>("دور انداختن دارو / تجهیزات");
        var result = await dialog.Result;
        if (!result!.Canceled)
        {
            DiscardList!.Reload();
        }
    }

    #region DiscardGrid
    private XPCollection<DisposedMedicine>? DiscardList { get; set; }
    private string? _searchString;

    private Func<DisposedMedicine, bool> _quickFilter => x =>
   {
       if (string.IsNullOrWhiteSpace(_searchString))
           return true;

       if (x.MedicName.Name.Contains(_searchString, StringComparison.OrdinalIgnoreCase))
           return true;

       return false;
   };

    private void RestoreMedication(DisposedMedicine item)
    {
        //Add to stock
        //Remove from stock
        var medStock = Session1.Query<MedicationStock>().FirstOrDefault(x => x.RigNo.Oid == item.RigNo.Oid &&
                                                                        x.MedicName.Oid == item.MedicName.Oid);
        if (medStock != null)
        {
            medStock.AvailCount += item.MedCount;
            medStock.Save();
        }

        item.Delete();
    }
    #endregion
}