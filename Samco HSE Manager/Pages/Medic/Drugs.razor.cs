using DevExpress.Blazor;
using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Samco_HSE.HSEData;
using MudBlazor;

namespace Samco_HSE_Manager.Pages.Medic;

public partial class Drugs
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject]
    private IConfiguration Configuration { get; set; } = null!;
    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private Session Session1 { get; set; } = null!;
    private IEnumerable<Rig> Rigs { get; set; } = null!;

    private IEnumerable<Medication>? MedicineList { get; set; }

    private readonly IEnumerable<string> _category = new List<string>
        {
            "دارو","تجهیز"
        };
    private readonly IEnumerable<string> _drugFormList = new List<string>
    {
        "Ampule","Syrup","Tablet","Otic Drop","Ophthalmic Ointment",
        "Ophthalmic Drop","Bottle","Suppository","Vial","Spray","Nasal Spray",
        "Inhaler","Topical Gel","Suspension","Sub-lingual Tablet","Cream",
        "Ointment","Oral Drop","Pearl","Sachet","Ophthalmic Solution","IV Fluid",
        "Effervescent Tablet",
        "Pack","Pads","Support","Splint","Roll","Tools","Bandage","Tube","Plaster",
        "Catheter","Airway","Syringe","Suture","Gauze","Dressing"
    };
    private DxGrid? MedicineGrid { get; set; }

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

    private RenderFragment BuildColumnsGrid()
    {
        using var tempSession = new Session(DataLayer);
        IEnumerable<Rig> rigs;
        if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
        {
            var loggedUser =
                tempSession.FindObject<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
            rigs = loggedUser.Rigs.ToList();
        }
        else
        {
            //Owner
            rigs = tempSession.Query<Rig>().ToList();
        }

        void NewColumns(RenderTreeBuilder b)
        {
            foreach (var rig in rigs)
            {
                b.OpenComponent(0, typeof(DxGridDataColumn));
                b.AddAttribute(0, "FieldName", rig.Oid.ToString());
                b.AddAttribute(0, "Caption", $"تعداد در {rig.Name}");
                b.AddAttribute(0, "MinWidth", 100);
                b.AddAttribute(0, "UnboundType", GridUnboundColumnType.Integer);
                b.CloseComponent();
            }
        }

        return NewColumns;
    }
    private void MedicationGridUnbound(GridUnboundColumnDataEventArgs e)
    {
        using var tempSession = new Session(DataLayer);
        var currentMedication =
            tempSession.FindObject<Medication>(new BinaryOperator("Oid", ((Medication)e.DataItem).Oid));
        var medStock = tempSession.Query<MedicationStock>().FirstOrDefault(itm => itm.RigNo.Oid == int.Parse(e.FieldName) && itm.MedicName.Oid == currentMedication.Oid);
        e.Value = medStock?.AvailCount ?? 0;
    }

    private void MedicineEditStart(GridEditStartEventArgs e)
    {
        if (SamcoSoftShared.CurrentUserRole <= SamcoSoftShared.SiteRoles.Supervisor) return;
        Snackbar.Add("شما اجازه ویرایش دارو و تجهیزات را ندارید.", Severity.Warning);
        e.Cancel = true;
    }
    private void MedicineEditModel(GridCustomizeEditModelEventArgs e)
    {
        var dataItem = (Medication?)e.DataItem ?? new Medication(Session1);
        e.EditModel = dataItem;
    }

    private async Task OnEditModelSaving(GridEditModelSavingEventArgs e)
    {
        var editModel = (Medication)e.EditModel;
        //Validation
        if (string.IsNullOrEmpty(editModel.Name) || string.IsNullOrEmpty(editModel.Dose) ||
            string.IsNullOrEmpty(editModel.DrugForm) || string.IsNullOrEmpty(editModel.Category))
        {
            Snackbar.Add("لطفاً موارد الزامی را تکمیل کنید.", Severity.Error);
            e.Cancel = true;
            return;
        }

        //Check drug not existed before
        var selMedication = await Session1.Query<Medication>().FirstOrDefaultAsync(x => x.Name == editModel.Name &&
            x.Dose == editModel.Dose &&
            x.DrugForm == editModel.DrugForm);
        if (e.IsNew)
        {
            if (selMedication != null)
            {
                //drug existed
                Snackbar.Add("دارو / تجهیز با همین مشخصات در سیستم موجود است. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.", Severity.Error);
                e.Cancel = true;
                return;
            }
        }
        else
        {
            if (selMedication != null && selMedication.Oid != editModel.Oid)
            {
                //User existed
                Snackbar.Add("دارو / تجهیز با همین مشخصات در سیستم موجود است. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.", Severity.Error);
                e.Cancel = true;
                return;
            }
        }

        editModel.Save();
        MedicineList = await Session1.Query<Medication>().ToListAsync();
    }

    private async Task OnDataItemDeleting(GridDataItemDeletingEventArgs e)
    {
        //prevent owner editing
        if (SamcoSoftShared.CurrentUserRole > SamcoSoftShared.SiteRoles.Supervisor)
        {
            Snackbar.Add("شما اجازه ویرایش دارو و تجهیزات را ندارید.", Severity.Warning);
            e.Cancel = true;
            return;
        }

        var dataItem =
            await Session1.FindObjectAsync<Medication>(new BinaryOperator("Oid", (e.DataItem as Medication)!.Oid));
        if (dataItem != null)
        {
            dataItem.Delete();
            MedicineList = await Session1.Query<Medication>().ToListAsync();
        }
    }

    #endregion

    #region MedicationCount

    private DxPopup? SetNumberModal { get; set; }
    private DxComboBox<Rig, Rig>? RigBx { get; set; }
    private MedicationStock? _selMedicationStock;
    private async Task OnSetNumberBtnClick()
    {
        if (MedicineGrid!.SelectedDataItems.Any() == false)
        {
            Snackbar.Add("لطفاً یک دارو / تجهیز را از لیست زیر انتخاب کنید.", Severity.Warning);
            return;
        }

        _selMedicationStock = new MedicationStock(Session1) { MedicName = (Medication)MedicineGrid!.SelectedDataItem };
        //auto select rig
        await SetNumberModal!.ShowAsync();
#pragma warning disable BL0005
        if (Rigs.Count() == 1) RigBx!.Text = Rigs.First().Name;
#pragma warning restore BL0005
    }

    private void RigSelectionChanged(object itm)
    {
        //Change data source if needed
        var selMedication = (Medication)MedicineGrid!.SelectedDataItem;
        //Get stock items
        var stockItm = Session1.Query<MedicationStock>().Where(x => x.RigNo.Oid == ((Rig)itm).Oid &&
                                                                   x.MedicName.Oid == selMedication.Oid);
        if (stockItm.Any())
        {
            _selMedicationStock = stockItm.First();
        }
        else
        {
            _selMedicationStock = new MedicationStock(Session1)
            {
                MedicName = (Medication)MedicineGrid!.SelectedDataItem,
                RigNo = (Rig)itm
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
        MedicineGrid!.Reload();
        await SetNumberModal!.CloseAsync();
    }
    #endregion

    #region DrugRequest


    #endregion
    private DxPopup? DrugRequestModal { get; set; }
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

        var parameter = $"ReportName=MedicineRequest&Parameters=RigId--{_selRig.Oid}|Title--شرکت {Configuration["CompanyInfo:Name"]} - دکل {_selRig.Name}";
        NavigationManager.NavigateTo("report?" + parameter);
    }
}