using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Samco_HSE.HSEData;
using Samco_HSE_Manager.Pages.Officer.PersonnelEdit;
using Syncfusion.Blazor.Grids;
using Action = Syncfusion.Blazor.Grids.Action;

namespace Samco_HSE_Manager.Pages.Officer;

public partial class Personnel : IDisposable
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private Session Session1 { get; set; } = null!;
    private IEnumerable<Samco_HSE.HSEData.Personnel>? Personnels { get; set; }
    private Samco_HSE.HSEData.Personnel? SelPersonnel { get; set; }
    private IEnumerable<string>? RigRoles { get; set; }

    private readonly IEnumerable<string> _status = new List<string>
    {
        "فعال", "خاتمه همکاری", "انتقال به سایر شرکت‌ها"
    };

    private IEnumerable<Rig>? Rigs { get; set; }
    private SfGrid<Samco_HSE.HSEData.Personnel>? PersonnelGrid { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
        RigRoles = await File.ReadAllLinesAsync(Path.Combine(HostEnvironment.WebRootPath, "content", "RigRoles.txt"));
        await LoadInformation();
    }

    private async Task LoadInformation()
    {
        if (SamcoSoftShared.CurrentUserRole > SamcoSoftShared.SiteRoles.Admin)
        {
            var loggedUser =
                await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
            Personnels = await Session1.Query<Samco_HSE.HSEData.Personnel>()
                .Where(x => loggedUser.Rigs.Contains(x.ActiveRig)).ToListAsync();
            Rigs = loggedUser.Rigs;
        }
        else
        {
            //Owner
            Personnels = await Session1.Query<Samco_HSE.HSEData.Personnel>().ToListAsync();
            Rigs = await Session1.Query<Rig>().ToListAsync();
        }
    }

    private async Task PersonnelGrid_Action(ActionEventArgs<Samco_HSE.HSEData.Personnel> e)
    {
        switch (e.RequestType)
        {
            case Action.Add:
                e.Data = new Samco_HSE.HSEData.Personnel(Session1);
                break;
            case Action.BeginEdit:
                var existUser = await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", e.RowData.Oid));
                if (existUser != null && existUser.Oid != SamcoSoftShared.CurrentUserId)
                {
                    Snackbar.Add("شما اجازه تغییر اطلاعات کاربران سیستم را ندارید.", Severity.Error);
                    e.Cancel = true;
                }

                e.Data = await Session1.GetObjectByKeyAsync<Samco_HSE.HSEData.Personnel>(e.RowData.Oid);
                break;
            case Action.Save:
                {
                    var editModel = e.Data;
                    //Validation
                    if (editModel.ActiveRig == null || string.IsNullOrEmpty(editModel.PersonnelName) ||
                        string.IsNullOrEmpty(editModel.CurrentRole) || string.IsNullOrEmpty(editModel.Status))
                    {
                        Snackbar.Add("لطفاً موارد الزامی را تکمیل کنید.", Severity.Error);
                        e.Cancel = true;
                        return;
                    }

                    if (string.IsNullOrEmpty(editModel.NationalID) == false && editModel.NationalID.Length < 10)
                    {
                        Snackbar.Add("کد ملی باید حداقل 10 رقم باشد.", Severity.Error);
                        e.Cancel = true;
                        return;
                    }

                    //Check user not existed before
                    var selPerson = Session1.FindObject<Samco_HSE.HSEData.Personnel>(
                        new BinaryOperator(nameof(Samco_HSE.HSEData.Personnel.NationalID), editModel.NationalID));
                    if (selPerson != null && selPerson.Oid != editModel.Oid)
                    {
                        //User existed
                        Snackbar.Add(
                            $"پرسنل با نام {selPerson.PersonnelName} با همین کد ملی در سیستم ثبت شده است. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.",
                            Severity.Error);
                        e.Cancel = true;
                        return;
                    }

                    if (string.IsNullOrEmpty(editModel.NationalID) == false && editModel.NationalID.Length < 10)
                    {
                        Snackbar.Add("کد ملی باید حداقل 10 رقم باشد.", Severity.Error);
                        e.Cancel = true;
                        return;
                    }

                    editModel.Save();
                    break;
                }
            case Action.Delete:
                {
                    var systemUser = await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", e.RowData.Oid));
                    if (systemUser != null && systemUser.GetType() == typeof(User))
                    {
                        Snackbar.Add("شما اجازه حذف اطلاعات کاربران سیستم را ندارید.", Severity.Error);
                        e.Cancel = true;
                    }

                    var dataItem = e.RowData;
                    dataItem.Delete();
                    break;
                }
        }
    }

    private async Task PersonnelToolbarClickHandler(Syncfusion.Blazor.Navigations.ClickEventArgs args)
    {
        if (args.Item.Id == "personnelGrid_Excel Export")
        {
            Snackbar.Add("سیستم در حال ایجاد فایل است. لطفاً تا دانلود گزارش شکیبا باشید...", Severity.Info);
            var exportProperties = new ExcelExportProperties
            {
                FileName = "PersonnelList.xlsx"
            };
            await PersonnelGrid!.ExportToExcelAsync(exportProperties);
        }
    }

    public void Dispose()
    {
        Session1.Dispose();
    }
    private void PersonnelSelectionChanged(RowSelectEventArgs<Samco_HSE.HSEData.Personnel> obj)
    {
        SelPersonnel = obj.Data;
    }

    private async Task OpenIncentiveDialog()
    {
        if (SelPersonnel == null)
        {
            Snackbar.Add("لطفاً یک نفر را از لیست انتخاب کنید.", Severity.Error);
            return;
        }

        if (SelPersonnel.ActiveRig.WellWorks.FirstOrDefault(x => x.IsActive) == null)
        {
            Snackbar.Add("پرسنل انتخاب شده در هیچ پروژه فعالی قرار ندارد.", Severity.Error);
            return;
        }
        await DialogService.ShowAsync<PersonnelIncentive>($"ثبت تشویق برای {SelPersonnel.PersonnelName}",
            new DialogParameters { { "PersonId", SelPersonnel.Oid } }, new DialogOptions(){ClassBackground = "safe-item"});
    }

    private async Task OpenWarningDialog()
    {
        if (SelPersonnel == null)
        {
            Snackbar.Add("لطفاً یک نفر را از لیست انتخاب کنید.", Severity.Error);
            return;
        }

        if (SelPersonnel.ActiveRig.WellWorks.FirstOrDefault(x => x.IsActive) == null)
        {
            Snackbar.Add("پرسنل انتخاب شده در هیچ پروژه فعالی قرار ندارد.", Severity.Error);
            return;
        }
        await DialogService.ShowAsync<PersonnelWarning>($"ثبت اخطار برای {SelPersonnel.PersonnelName}",
            new DialogParameters { { "PersonId", SelPersonnel.Oid } }, new DialogOptions(){ClassBackground = "danger-item"});
    }

    private async Task OpenEquipmentDialog()
    {
        if (SelPersonnel == null)
        {
            Snackbar.Add("لطفاً یک نفر را از لیست انتخاب کنید.", Severity.Error);
            return;
        }

        if (SelPersonnel.ActiveRig.WellWorks.FirstOrDefault(x => x.IsActive) == null)
        {
            Snackbar.Add("پرسنل انتخاب شده در هیچ پروژه فعالی قرار ندارد.", Severity.Error);
            return;
        }
        
        await DialogService.ShowAsync<EquipmentSelector>($"تحویل تجهیزات به {SelPersonnel.PersonnelName}",
            new DialogParameters { { "PersonId", SelPersonnel.Oid } }, new DialogOptions(){ClassBackground = "danger-item"});
    }
}