using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using DevExpress.Data.Filtering;
using DevExpress.Blazor;
using MudBlazor;

namespace Samco_HSE_Manager.Pages.Medic;

public partial class Visits
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;

    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private DxGrid VisitGrid { get; set; } = null!;
    private Session Session1 { get; set; } = null!;
    private IEnumerable<Rig>? Rigs { get; set; }

    private IEnumerable<MedicalVisit>? VisitList { get; set; }
    private IEnumerable<Samco_HSE.HSEData.Personnel>? PersonnelList { get; set; }

    private IEnumerable<string>? _category;

    private readonly Dictionary<string, string> _visitType = new()
    {
        { "Medicine", "بیمار سرپایی" },
        { "MedEvac", "اعزام بیمار (Medical Evacuation)" },
        { "FAC", "کمکهای اولیه (First Aid Case - FAC)" },
        { "MTC", "کمکهای پزشکی (Medical Treatment Case - MTC)" },
        { "LWDC", "نیازمند استراحت پزشکی (Lost Work Day Case - LWDC)" },
        { "RWC", "کار کردن محدود (Restricted Work Case - RWC)" },
        { "Fatality", "مرگ و میر (Fatality)" },
    };

    private bool _showLwd;

    private bool _showNames;
    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
        _category = await File.ReadAllLinesAsync(Path.Combine(HostEnvironment.WebRootPath, "content", "MedicalCategory.txt"));
        _showNames = SamcoSoftShared.CurrentUserRole is SamcoSoftShared.SiteRoles.Owner or SamcoSoftShared.SiteRoles.Medic;
        await LoadInformation();
    }

    private async Task LoadInformation()
    {
        _specialist = await File.ReadAllLinesAsync(Path.Combine(HostEnvironment.WebRootPath, "content", "SpecialistsList.txt"));
        if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
        {
            var loggedUser =
                await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));

            VisitList = Session1.Query<MedicalVisit>().Where(x => x.DoctorName.Oid == loggedUser.Oid || loggedUser.Rigs.Contains(x.Patient.ActiveRig));
            PersonnelList = await Session1.Query<Samco_HSE.HSEData.Personnel>().Where(x => loggedUser.Rigs.Contains(x.ActiveRig)).ToListAsync();
            ReferList = await Session1.Query<MedicalReferral>().Where(x => loggedUser.Rigs.Contains(x.MedicalVisit.DoctorName.ActiveRig)).ToListAsync();
            Rigs = loggedUser.Rigs;
        }
        else
        {
            //Owner
            VisitList = Session1.Query<MedicalVisit>();
            PersonnelList = await Session1.Query<Samco_HSE.HSEData.Personnel>().ToListAsync();
            ReferList = await Session1.Query<MedicalReferral>().ToListAsync();
            Rigs = await Session1.Query<Rig>().ToListAsync();
        }
    }

    #region Visits

    private void VisitColumnChooserOnClick()
    {
        VisitGrid.ShowColumnChooser(".visit-column-chooser");
    }
    private void VisitEditStart(GridEditStartEventArgs e)
    {
        //prevent owner editing
        var dataItem = (MedicalVisit?)e.DataItem;
        switch (e.IsNew)
        {
            case true:
                {
                    if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Medic &&
                        SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
                    {
                        Snackbar.Add("فقط پزشک اجازه ویزیت بیماران را دارد.", Severity.Error);
                        e.Cancel = true;
                    }

                    break;
                }
            case false when dataItem!.DoctorName.Oid != SamcoSoftShared.CurrentUserId:
                Snackbar.Add("شما اجازه ویرایش ویزیت پزشک دیگری را ندارید.", Severity.Error);
                e.Cancel = true;
                break;
        }
    }

    private void VisitEditModel(GridCustomizeEditModelEventArgs e)
    {
        var dataItem = (MedicalVisit?)e.DataItem ?? new MedicalVisit(Session1);
        e.EditModel = dataItem;
    }
    private void MedicalTypeChanged(KeyValuePair<string, string> visitType)
    {
        _showLwd = visitType.Key == "LWDC";
    }

    private async Task OnEditModelSaving(GridEditModelSavingEventArgs e)
    {
        var editModel = (MedicalVisit)e.EditModel;
        var loggedUser =
            await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
        //Validation
        if (editModel.Patient == null || string.IsNullOrEmpty(editModel.Diagnose)
                                      || string.IsNullOrEmpty(editModel.Category) || string.IsNullOrEmpty(editModel.VisitType))
        {
            Snackbar.Add("لطفاً موارد الزامی را تکمیل کنید.", Severity.Error);
            e.Cancel = true;
            return;
        }
        //LWD Validation
        if (editModel is { VisitType: "LWDC", LWD: null })
        {
            Snackbar.Add("لطفاً تعداد روزهای استراحت را وارد کنید.", Severity.Error);
            e.Cancel = true;
            return;
        }
        editModel.RigNo = loggedUser.ActiveRig ?? editModel.Patient.ActiveRig;
        editModel.DoctorName = loggedUser;
        editModel.Save();

        await LoadInformation();
    }

    private async Task OnDataItemDeleting(GridDataItemDeletingEventArgs e)
    {
        var dataItem =
            await Session1.FindObjectAsync<MedicalVisit>(new BinaryOperator("Oid", (e.DataItem as MedicalVisit)!.Oid));
        if (dataItem != null)
        {
            if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Medic &&
                SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
            {
                Snackbar.Add("فقط پزشک اجازه حذف ویزیت را دارد.", Severity.Error);
                e.Cancel = true;
            }
            //prevent other doctor visit
            if (dataItem.DoctorName.Oid != SamcoSoftShared.CurrentUserId)
            {
                Snackbar.Add("شما اجازه حذف ویزیت پزشک دیگری را ندارید.", Severity.Error);
                e.Cancel = true;
                return;
            }
            //restore drugs

            dataItem.Delete();
            await LoadInformation();
        }
    }
    private async Task OnVisitPrintBtnClick()
    {
        Snackbar.Add("سیستم در حال ایجاد فایل است. لطفاً تا دانلود گزارش شکیبا باشید...", Severity.Info);
        await VisitGrid.ExportToXlsxAsync("Visits", new GridXlExportOptions
        {
            CustomizeSheet = SamcoSoftShared.CustomizeSheet,
            CustomizeCell = SamcoSoftShared.CustomizeCell,
            CustomizeSheetFooter = SamcoSoftShared.CustomizeFooter
        })!;
    }

    #endregion

    #region Referrals

    private DxGrid ReferGrid { get; set; } = null!;

    private IEnumerable<MedicalReferral>? ReferList { get; set; }

    private IEnumerable<string>? _specialist;

    private readonly IEnumerable<string> _status = new List<string>
    {
        "ارجاع به متخصص","بازگشت به کار","کار کردن محدود","عدم تحویل نامه ارجاع"
    };

    #endregion

    private Task OnReferEditBtnClick()
    {
        throw new NotImplementedException();
    }

    private Task OnReferDeleteBtnClick()
    {
        throw new NotImplementedException();
    }

    private Task OnReferPrintBtnClick()
    {
        throw new NotImplementedException();
    }

    private void ReferColumnChooserOnClick()
    {
        ReferGrid.ShowColumnChooser(".refer-column-chooser");
    }
}