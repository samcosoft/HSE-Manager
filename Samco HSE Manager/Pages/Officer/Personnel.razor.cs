using System.Diagnostics.CodeAnalysis;
using BootstrapBlazor.Components;
using DevExpress.Blazor;
using DevExpress.Data.Filtering;
using DevExpress.Export;
using DevExpress.Printing.ExportHelpers;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;

namespace Samco_HSE_Manager.Pages.Officer;

public partial class Personnel : IDisposable
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;

    [Inject]
    [NotNull]
    private ToastService? ToastService { get; set; }

    [Inject]
    [NotNull]
    private MessageService? MessageService { get; set; }

    private Session Session1 { get; set; } = null!;
    private IEnumerable<Samco_HSE.HSEData.Personnel>? Personnels { get; set; }

    private IEnumerable<string>? RigRoles { get; set; }

    private readonly IEnumerable<string> _status = new List<string>
    {
        "فعال","خاتمه همکاری","انتقال به سایر شرکت‌ها"
    };
    private IEnumerable<Rig>? Rigs { get; set; }

    private DxGrid? PersonnelGrid { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
        RigRoles = await File.ReadAllLinesAsync(Path.Combine(HostEnvironment.WebRootPath, "content", "RigRoles.txt"));
        await LoadInformation();
    }

    private async Task LoadInformation()
    {
        if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
        {
            var loggedUser =
                await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
            Personnels = await Session1.Query<Samco_HSE.HSEData.Personnel>().Where(x => loggedUser.Rigs.Contains(x.ActiveRig)).ToListAsync();
            Rigs = loggedUser.Rigs;
        }
        else
        {
            //Owner
            Personnels = await Session1.Query<Samco_HSE.HSEData.Personnel>().ToListAsync();
            Rigs = await Session1.Query<Rig>().ToListAsync();
        }
    }

    private void PersonnelGridUnbound(GridUnboundColumnDataEventArgs e)
    {
        using var tempSession = new Session(DataLayer);
        var currentUser = tempSession.FindObject<Samco_HSE.HSEData.Personnel>(new BinaryOperator("Oid", ((Samco_HSE.HSEData.Personnel)e.DataItem).Oid));
        //if (currentWork == null) { return; }

        e.Value = e.FieldName switch
        {
            "PPECounts" => currentUser.PPEs.Count,
            "WarningsCount" => currentUser.Warnings.Count,
            "IncentivesCount" => currentUser.Incentives.Count,
            "AccidentsCount" => currentUser.Accidents.Count,
            "PracticeCount" => currentUser.Practices.Count,
            "TrainingCount" => currentUser.Trainings.Count,
            "StopReportCount" => currentUser.StopCardsReports.Count(x=>x.IsApproved),
            "MedicalVisitsCount" => currentUser.MedicalVisits.Count,
            _ => e.Value
        };
    }

    private async Task PersonnelGrid_EditStart(GridEditStartEventArgs e)
    {
        var dataItem = (Samco_HSE.HSEData.Personnel)e.DataItem;
        //prevent owner editing
        if (e.IsNew == false && dataItem.GetType() == typeof(User) && ((User)dataItem).SiteRole == "Owner")
        {
            await ToastService.Error("خطا در ویرایش کاربر", "شما اجازه تغییر اطلاعات مدیر سیستم را ندارید.");
            e.Cancel = true;
        }
        if (e.IsNew == false && dataItem.GetType() == typeof(User) && dataItem.Oid != SamcoSoftShared.CurrentUserId)
        {
            await ToastService.Error("خطا در ویرایش کاربر", "شما اجازه تغییر اطلاعات کاربران سیستم را ندارید.");
            e.Cancel = true;
        }
    }

    private void PersonnelEditModel(GridCustomizeEditModelEventArgs e)
    {
        var dataItem = (Samco_HSE.HSEData.Personnel?)e.DataItem ?? new Samco_HSE.HSEData.Personnel(Session1);
        e.EditModel = dataItem;
    }

    private async Task OnEditModelSaving(GridEditModelSavingEventArgs e)
    {
        var editModel = (Samco_HSE.HSEData.Personnel)e.EditModel;
        //Validation
        if (editModel.ActiveRig == null || string.IsNullOrEmpty(editModel.PersonnelName) ||
            string.IsNullOrEmpty(editModel.CurrentRole) || string.IsNullOrEmpty(editModel.Status))
        {
            await ToastService.Error("خطا در افزودن پرسنل", "لطفاً موارد الزامی را تکمیل کنید.");
            e.Cancel = true;
            return;
        }
        //Check user not existed before
        var selPerson = Session1.FindObject<Samco_HSE.HSEData.Personnel>(new BinaryOperator(nameof(Samco_HSE.HSEData.Personnel.NationalID), editModel.NationalID));
        if (e.IsNew)
        {
            if (selPerson != null)
            {
                //User existed
                await ToastService.Error("خطا در ثبت اطلاعات",
                    $"پرسنل با نام {selPerson.PersonnelName} با همین کد ملی در سیستم ثبت شده است. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.");
                e.Cancel = true;
                return;
            }
        }
        else
        {
            if (selPerson != null && selPerson.Oid != editModel.Oid)
            {
                //User existed
                await ToastService.Error("خطا در ثبت اطلاعات",
                    $"پرسنل با نام {selPerson.PersonnelName} با همین کد ملی در سیستم ثبت شده است. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.");
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
            await Session1.FindObjectAsync<Samco_HSE.HSEData.Personnel>(new BinaryOperator("Oid", (e.DataItem as Samco_HSE.HSEData.Personnel)!.Oid));
        if (dataItem != null && dataItem.GetType() == typeof(User))
        {
            //prevent delete users
            await ToastService.Error("خطا در حذف کاربر", "شما اجازه‌ی حذف اطلاعات کاربران سیستم را ندارید.");
            e.Cancel = true;
            return;
        }
        dataItem?.Delete();
        await LoadInformation();
    }

    private void ColumnChooserOnClick()
    {
        PersonnelGrid?.ShowColumnChooser(".column-chooser-button");
    }

    public void Dispose()
    {
        Session1.Dispose();
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

        await PersonnelGrid?.ExportToXlsxAsync("StopCards", new GridXlExportOptions
        {
            CustomizeSheet = SamcoSoftShared.CustomizeSheet,
            CustomizeCell = SamcoSoftShared.CustomizeCell,
            CustomizeSheetFooter = SamcoSoftShared.CustomizeFooter
        })!;
    }
}