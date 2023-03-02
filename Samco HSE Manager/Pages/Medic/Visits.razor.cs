using BootstrapBlazor.Components;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using System.Diagnostics.CodeAnalysis;
using DevExpress.Data.Filtering;
using DevExpress.Blazor;

namespace Samco_HSE_Manager.Pages.Medic;

public partial class Visits
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;

    [Inject]
    [NotNull]
    private ToastService? ToastService { get; set; }

    private Session Session1 { get; set; } = null!;

    private IEnumerable<MedicalVisit>? VisitList { get; set; }
    private IEnumerable<Samco_HSE.HSEData.Personnel>? PersonnelList { get; set; }

    private IEnumerable<string>? _category;

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
        if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
        {
            var loggedUser =
                await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));

            VisitList = Session1.Query<MedicalVisit>().Where(x => x.DoctorName.Oid == loggedUser.Oid || loggedUser.Rigs.Contains(x.Patient.ActiveRig));
            PersonnelList = await Session1.Query<Samco_HSE.HSEData.Personnel>().Where(x => loggedUser.Rigs.Contains(x.ActiveRig)).ToListAsync();
        }
        else
        {
            //Owner
            VisitList = Session1.Query<MedicalVisit>();
            PersonnelList = await Session1.Query<Samco_HSE.HSEData.Personnel>().ToListAsync();
        }
    }
    private async Task VisitEditStart(GridEditStartEventArgs e)
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
                        await ToastService.Error("خطا در ثبت ویزیت", "فقط پزشک اجازه ویزیت بیماران را دارد.");
                        e.Cancel = true;
                    }

                    break;
                }
            case false when dataItem!.DoctorName.Oid != SamcoSoftShared.CurrentUserId:
                await ToastService.Error("خطا در ویرایش ویزیت", "شما اجازه ویرایش ویزیت پزشک دیگری را ندارید.");
                e.Cancel = true;
                break;
        }
    }

    private void VisitEditModel(GridCustomizeEditModelEventArgs e)
    {
        var dataItem = (MedicalVisit?)e.DataItem ?? new MedicalVisit(Session1);
        e.EditModel = dataItem;
    }

    private async Task OnEditModelSaving(GridEditModelSavingEventArgs e)
    {
        var editModel = (MedicalVisit)e.EditModel;
        var loggedUser =
            await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
        //Validation
        if (editModel.Patient == null || string.IsNullOrEmpty(editModel.Diagnose) || string.IsNullOrEmpty(editModel.Category))
        {
            await ToastService.Error("خطا در ثبت ویزیت", "لطفاً موارد الزامی را تکمیل کنید.");
            e.Cancel = true;
            return;
        }
        editModel.RigNo = loggedUser.ActiveRig;
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
                await ToastService.Error("خطا در حذف ویزیت", "فقط پزشک اجازه حذف ویزیت را دارد.");
                e.Cancel = true;
            }
            //prevent other doctor visit
            if (dataItem.DoctorName.Oid != SamcoSoftShared.CurrentUserId)
            {
                await ToastService.Error("خطا در حذف ویزیت", "شما اجازه حذف ویزیت پزشک دیگری را ندارید.");
                e.Cancel = true;
                return;
            }
            //restore drugs

            dataItem.Delete();
            await LoadInformation();
        }
    }

}