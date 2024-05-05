using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using DevExpress.Data.Filtering;
using MudBlazor;
using Syncfusion.Blazor.Grids;
using Action = Syncfusion.Blazor.Grids.Action;

namespace Samco_HSE_Manager.Pages.Medic;

public partial class Visits : IDisposable
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;

    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private SfGrid<MedicalVisit> VisitGrid { get; set; } = null!;
    private Session Session1 { get; set; } = null!;

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
        { "Fatality", "مرگ و میر (Fatality)" }
    };

    private bool _showNames;

    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
        _category = await File.ReadAllLinesAsync(Path.Combine(HostEnvironment.WebRootPath, "content",
            "MedicalCategory.txt"));
        _showNames =
            SamcoSoftShared.CurrentUserRole is SamcoSoftShared.SiteRoles.Owner or SamcoSoftShared.SiteRoles.Medic;
        _specialistList =
            await File.ReadAllLinesAsync(Path.Combine(HostEnvironment.WebRootPath, "content", "SpecialistsList.txt"));
        if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
        {
            var loggedUser =
                await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));

            VisitList = await Session1.Query<MedicalVisit>().Where(x =>
                x.DoctorName.Oid == loggedUser.Oid || loggedUser.Rigs.Contains(x.Patient.ActiveRig)).ToListAsync();
            PersonnelList = await Session1.Query<Samco_HSE.HSEData.Personnel>()
                .Where(x => loggedUser.Rigs.Contains(x.ActiveRig) &&
                            x.Status == SamcoSoftShared.GetPersonnelStatus(SamcoSoftShared.PersonnelStatus.Active)).ToListAsync();
            ReferList = await Session1.Query<MedicalReferral>()
                .Where(x => loggedUser.Rigs.Contains(x.MedicalVisit.DoctorName.ActiveRig)).ToListAsync();
        }
        else
        {
            //Owner
            VisitList = await Session1.Query<MedicalVisit>().ToListAsync();
            PersonnelList = await Session1.Query<Samco_HSE.HSEData.Personnel>()
                .Where(x => x.Status == SamcoSoftShared.GetPersonnelStatus(SamcoSoftShared.PersonnelStatus.Active)).ToListAsync();
            ReferList = await Session1.Query<MedicalReferral>().ToListAsync();
        }
    }

    #region Visits

    private async Task VisitGrid_Action(ActionEventArgs<MedicalVisit> e)
    {
        switch (e.RequestType)
        {
            case Action.Add:
                if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Medic &&
                    SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
                {
                    Snackbar.Add("فقط پزشک اجازه ویزیت بیماران را دارد.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                e.Data = new MedicalVisit(Session1);
                break;
            case Action.BeginEdit:
                //prevent owner editing
                if (e.RowData.DoctorName?.Oid != SamcoSoftShared.CurrentUserId)
                {
                    Snackbar.Add("شما اجازه ویرایش ویزیت پزشک دیگری را ندارید.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                e.Data = await Session1.FindObjectAsync<MedicalVisit>(new BinaryOperator("Oid", e.RowData.Oid));
                break;
            case Action.Save:
                {
                    var editModel = e.Data;
                    //Validation
                    var loggedUser =
                        await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
                    if (editModel.Patient == null || string.IsNullOrEmpty(editModel.Diagnose)
                                           || string.IsNullOrEmpty(editModel.Category) ||
                                           string.IsNullOrEmpty(editModel.VisitType))
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

                    if (editModel.VisitType != "LWDC")
                    {
                        editModel.LWD = null;
                    }

                    editModel.RigNo = loggedUser.ActiveRig ?? editModel.Patient.ActiveRig;
                    editModel.DoctorName = loggedUser;
                    editModel.Save();
                    break;
                }
            case Action.Delete:
                {
                    if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Medic &&
                        SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
                    {
                        Snackbar.Add("فقط پزشک اجازه حذف ویزیت را دارد.", Severity.Error);
                        e.Cancel = true;
                        return;
                    }

                    //prevent other doctor visit
                    if (e.RowData.DoctorName.Oid != SamcoSoftShared.CurrentUserId)
                    {
                        Snackbar.Add("شما اجازه حذف ویزیت پزشک دیگری را ندارید.", Severity.Error);
                        e.Cancel = true;
                        return;
                    }

                    //restore drugs
                    foreach (var itm in e.RowData.UsedMedicines)
                    {
                        var availDrug = Session1.Query<MedicationStock>().First(x =>
                            x.RigNo.Oid == SamcoSoftShared.CurrentUser!.ActiveRig.Oid &&
                            x.MedicName.Oid == itm.MedicName.Oid);
                        //Change stock medicines
                        availDrug.AvailCount += itm.MedCount;
                        availDrug.Save();
                    }

                    e.RowData.Delete();
                    break;
                }
        }
    }

    private async Task VisitToolbarClickHandler(Syncfusion.Blazor.Navigations.ClickEventArgs args)
    {
        if (args.Item.Id == "visitGrid_Excel Export")
        {
            Snackbar.Add("سیستم در حال ایجاد فایل است. لطفاً تا دانلود گزارش شکیبا باشید...", Severity.Info);
            var exportProperties = new ExcelExportProperties
            {
                FileName = "VisitList.xlsx"
            };
            await VisitGrid.ExportToExcelAsync(exportProperties);
        }
    }
    #endregion

    #region Referrals

    private SfGrid<MedicalReferral> ReferGrid { get; set; } = null!;
    private MedicalReferral? _selReferral;
    private bool _fileVisible;

    private IEnumerable<MedicalReferral>? ReferList { get; set; }

    private IEnumerable<string>? _specialistList;

    private readonly IEnumerable<string> _status = new List<string>
    {
        "ارجاع به متخصص", "بازگشت به کار", "کار کردن محدود", "عدم تحویل نامه ارجاع"
    };

    private void Customize_Row(RowDataBoundEventArgs<MedicalReferral> args)
    {
        switch (args.Data.Status)
        {
            case "ارجاع به متخصص":
                {
                    args.Row.AddClass(new[] { "warning-item" });
                    break;
                }
            case "بازگشت به کار":
                {
                    args.Row.AddClass(new[] { "safe-item" });
                    break;
                }
            case "عدم تحویل نامه ارجاع":
                {
                    args.Row.AddClass(new[] { "danger-item" });
                    break;
                }
            default:
                {
                    args.Row.AddClass(new[] { "warning-item" });
                    break;
                }
        }
    }

    private async Task ReferGrid_Action(ActionEventArgs<MedicalReferral> e)
    {
        switch (e.RequestType)
        {
            case Action.BeginEdit:
                e.Data = await Session1.FindObjectAsync<MedicalReferral>(new BinaryOperator("Oid", e.RowData.Oid));
                break;
            case Action.Save:
                var editModel = e.Data;
                //Validation
                if (string.IsNullOrEmpty(editModel.Specialist) || string.IsNullOrEmpty(editModel.Status))
                {
                    Snackbar.Add("لطفاً موارد الزامی را تکمیل کنید.", Severity.Error);
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
    private void ReferralSelectionChanged(RowSelectEventArgs<MedicalReferral> obj)
    {
        _selReferral = obj.Data;
    }

    private async Task ToolbarClickHandler(Syncfusion.Blazor.Navigations.ClickEventArgs args)
    {
        if (args.Item.Id == "referGrid_Excel Export")
        {
            Snackbar.Add("سیستم در حال ایجاد فایل است. لطفاً تا دانلود گزارش شکیبا باشید...", Severity.Info);
            var exportProperties = new ExcelExportProperties
            {
                FileName = "ReferList.xlsx"
            };
            await ReferGrid.ExportToExcelAsync(exportProperties);
        }
    }

    private void OpenFileDialog(MedicalReferral refer)
    {
        _selReferral = refer;
        _fileVisible = true;
    }

    public void Dispose()
    {
        Session1.Dispose();
    }

    #endregion
}