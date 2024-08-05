using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using System.Text.RegularExpressions;
using DevExpress.DashboardWeb;
using MudBlazor;
using Syncfusion.Blazor.Grids;
using Syncfusion.Blazor.Popups;
using Action = Syncfusion.Blazor.Grids.Action;

namespace Samco_HSE_Manager.Pages.Admin;

public partial class Users : IDisposable
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;
    [Inject] private DashboardConfigurator DashboardConfigurator { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private Session Session1 { get; set; } = null!;
    private IEnumerable<User>? SystemUsers { get; set; }

    private IEnumerable<string>? RigRoles { get; set; }
    private IEnumerable<string>? Dashboards { get; set; }

    private readonly Dictionary<string, string> _roles = SamcoSoftShared.Roles;

    private readonly IEnumerable<string> _status = new List<string>
    {
        "فعال", "خاتمه همکاری", "انتقال به سایر شرکت‌ها"
    };

    private IEnumerable<Rig>? Rigs { get; set; }
    private SfGrid<User>? UserGrid { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
        RigRoles = await File.ReadAllLinesAsync(Path.Combine(HostEnvironment.WebRootPath, "content", "RigRoles.txt"));
        Dashboards = DashboardConfigurator.DashboardStorage.GetAvailableDashboardsInfo().Select(x => x.ID).ToList();
        if (SamcoSoftShared.CurrentUserRole == SamcoSoftShared.SiteRoles.Admin)
        {
            //Admin
            var loggedUser =
                await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
            SystemUsers = await Session1.Query<User>().Where(x => x.SiteRole != "Owner")
                .Where(x => loggedUser.Rigs.Contains(x.ActiveRig)).ToListAsync();
            Rigs = loggedUser.Rigs;
        }
        else
        {
            //Owner
            SystemUsers = await Session1.Query<User>().ToListAsync();
            Rigs = await Session1.Query<Rig>().ToListAsync();
        }
    }

    private async Task UserGrid_Action(ActionEventArgs<User> e)
    {
        switch (e.RequestType)
        {
            case Action.Add:
                e.Data = new User(Session1);
                break;
            case Action.BeginEdit:
                //prevent owner editing
                if (e.RowData.SiteRole == "Owner")
                {
                    Snackbar.Add("شما اجازه تغییر اطلاعات مدیر سیستم را ندارید.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                e.Data = await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", e.RowData.Oid));
                break;
            case Action.Save:
            {
                var editModel = e.Data;
                //Validation
                if (string.IsNullOrEmpty(editModel.Username) || string.IsNullOrEmpty(editModel.SiteRole) ||
                    editModel.ActiveRig == null ||
                    string.IsNullOrEmpty(editModel.PersonnelName) || string.IsNullOrEmpty(editModel.CurrentRole) ||
                    string.IsNullOrEmpty(editModel.Status) || string.IsNullOrEmpty(editModel.PersonnelNum))
                {
                    Snackbar.Add("لطفاً موارد الزامی را تکمیل کنید.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                if (Regex.IsMatch(editModel.Username, "[^a-z0-9.]+"))
                {
                    Snackbar.Add("نام کاربری فقط می‌تواند به زبان انگلیسی شامل حروف کوچک، عدد و نقطه (.) باشد.",
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

                //Check user not existed before
                var selPersonnel =
                    Session1.FindObject<Samco_HSE.HSEData.Personnel>(
                        new BinaryOperator(nameof(Samco_HSE.HSEData.Personnel.NationalID), editModel.NationalID));
                if (editModel.Oid < 0)
                {
                    if (selPersonnel != null)
                    {
                        //User existed
                        Snackbar.Add(
                            $"پرسنل با نام {selPersonnel.PersonnelName} با همین کد ملی در سیستم ثبت شده است. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.",
                            Severity.Error);
                        e.Cancel = true;
                        return;
                    }
                }
                else
                {
                    if (selPersonnel != null && selPersonnel.Oid != editModel.Oid)
                    {
                        //User existed
                        Snackbar.Add(
                            $"پرسنل با نام {selPersonnel.PersonnelName} با همین کد ملی در سیستم ثبت شده است. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.",
                            Severity.Error);
                        e.Cancel = true;
                        return;
                    }
                }

                //check username
                var selPerson =
                    Session1.FindObject<User>(new BinaryOperator(nameof(User.Username), editModel.Username));

                if (selPerson != null && selPerson.Oid != editModel.Oid)
                {
                    //Duplicate username
                    Snackbar.Add("کاربر دیگری با همین نام کاربری وجود دارد. لطفاً نام کاربری دیگری را برگزینید.",
                        Severity.Error);
                    e.Cancel = true;
                    return;
                }

                while (editModel.Rigs.Any())
                {
                    editModel.Rigs.Remove(editModel.Rigs[0]);
                }

                if (!editModel.Rigs.Contains(editModel.ActiveRig)) editModel.Rigs.Add(editModel.ActiveRig);
                editModel.Save();
                break;
            }
            case Action.Delete:
            {
                var dataItem = e.RowData;
                if (dataItem is { SiteRole: "Owner" })
                    //prevent delete Owner
                {
                    Snackbar.Add("شما اجازه حذف مدیر سیستم را ندارید.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                //prevent delete  current user
                if (dataItem!.Oid == SamcoSoftShared.CurrentUser?.Oid)
                {
                    Snackbar.Add("شما اجازه‌ی حذف اطلاعات کاربری خود را ندارید.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                dataItem.Delete();
                break;
            }
        }
    }

    private async Task UserToolbarClickHandler(Syncfusion.Blazor.Navigations.ClickEventArgs args)
    {
        if (args.Item.Id == "userGrid_Excel Export")
        {
            Snackbar.Add("سیستم در حال ایجاد فایل است. لطفاً تا دانلود گزارش شکیبا باشید...", Severity.Info);
            var exportProperties = new ExcelExportProperties
            {
                FileName = "UserList.xlsx"
            };
            await UserGrid!.ExportToExcelAsync(exportProperties);
        }
    }

    #region ChangePassword

    private SfDialog? PassModal { get; set; }
    private string? _newPass, _confPass;

    private async Task OnNewPassBtnClick()
    {
        if (UserGrid?.SelectedRecords.Any() == false)
        {
            Snackbar.Add("لطفاً یک کاربر را از لیست زیر انتخاب کنید.", Severity.Warning);
            return;
        }

        await PassModal!.ShowAsync();
    }

    private async Task PassOkBtnClick()
    {
        //check password
        if (string.IsNullOrWhiteSpace(_newPass))
        {
            Snackbar.Add("کلمه عبور جدید را وارد کنید.", Severity.Error);
            return;
        }

        if (_newPass != _confPass)
        {
            Snackbar.Add("کلمه‌های عبور وارد شده با هم همخوانی ندارند.", Severity.Error);
            return;
        }

        if (SamcoSoftShared.PasswordStrengthChecker(_newPass) < SamcoSoftShared.PasswordScore.Medium)
        {
            Snackbar.Add("کلمه عبور وارد شده بسیار ضعیف است. کلمه عبور پیچیده‌تری انتخاب کنید.", Severity.Warning);
            return;
        }

        var selUser = UserGrid!.SelectedRecords.First();
        selUser.Password = BCrypt.Net.BCrypt.EnhancedHashPassword(_newPass);
        selUser.Save();
        Snackbar.Add($"کلمه عبور برای کاربر {selUser.Username} با موفقیت تغییر یافت.", Severity.Success);
        _newPass = string.Empty;
        _confPass = string.Empty;
        await PassModal!.HideAsync();
    }

    #endregion

    #region Permission

    private SfDialog? PermitModal { get; set; }
    private IEnumerable<Rig>? PermitRigList { get; set; }

    private async Task OnPermitBtnClick()
    {
        if (UserGrid!.SelectedRecords.Any() == false)
        {
            Snackbar.Add("لطفاً یک کاربر را از لیست زیر انتخاب کنید.", Severity.Warning);
            return;
        }

        var selUser = UserGrid.SelectedRecords.First();
        if (selUser.SiteRole == "Owner")
        {
            Snackbar.Add("مدیر سیستم همواره به همه اطلاعات دسترسی دارد.", Severity.Warning);
            return;
        }

        if (selUser.Oid == SamcoSoftShared.CurrentUser?.Oid)
        {
            Snackbar.Add("شما نمی‌توانید سطح دسترسی خود را تغییر دهید.", Severity.Warning);
            return;
        }

        PermitRigList = selUser.Rigs.ToList();
        await PermitModal!.ShowAsync();
    }

    private async Task PermitOkBtnClick()
    {
        if (PermitRigList?.Any() == false)
        {
            Snackbar.Add("لطفاً حداقل یک دکل را انتخاب کنید.", Severity.Error);
            return;
        }

        var selUser = UserGrid!.SelectedRecords.First();
        while (selUser.Rigs.Any())
        {
            selUser.Rigs.Remove(selUser.Rigs[0]);
        }

        selUser.Rigs.AddRange(PermitRigList);
        if (!selUser.Rigs.Contains(selUser.ActiveRig)) selUser.Rigs.Add(selUser.ActiveRig);
        selUser.Save();
        Snackbar.Add("دسترسی‌ها با موفقیت ثبت شدند.", Severity.Success);
        await PermitModal!.HideAsync();
    }

    #endregion

    #region UpgradePersonnel

    private SfDialog? UpgradeModal { get; set; }
    private IEnumerable<Samco_HSE.HSEData.Personnel> PersonnelList { get; set; } = null!;
    private Samco_HSE.HSEData.Personnel? _selPersonnel;
    private string? _personnelUsername, _personnelRole;

    private async Task OnUpgradeBtnClick()
    {
        PersonnelList = Session1.Query<Samco_HSE.HSEData.Personnel>().AsEnumerable()
            .Where(x => x.GetType() != typeof(User)).ToList();
        _selPersonnel = null;
        _personnelUsername = string.Empty;
        _personnelRole = string.Empty;
        await UpgradeModal!.ShowAsync();
    }

    private async Task UpgradeOkBtnClick()
    {
        //Validation
        if (string.IsNullOrEmpty(_personnelUsername) || string.IsNullOrEmpty(_personnelRole) || _selPersonnel == null)
        {
            Snackbar.Add("لطفاً موارد الزامی را تکمیل کنید.", Severity.Error);
            return;
        }

        if (Regex.IsMatch(_personnelUsername, "[^a-z0-9.]+"))
        {
            Snackbar.Add("نام کاربری فقط می‌تواند به زبان انگلیسی شامل حروف کوچک، عدد و نقطه (.) باشد.",
                Severity.Error);
            return;
        }

        //check username
        var selPerson =
            Session1.FindObject<User>(new BinaryOperator(nameof(User.Username), _personnelUsername));

        if (selPerson != null)
        {
            //Duplicate username
            Snackbar.Add("کاربر دیگری با همین نام کاربری وجود دارد. لطفاً نام کاربری دیگری را برگزینید.",
                Severity.Error);
            return;
        }

        try
        {
            //Prepare sql query
            var queryString =
                $"DECLARE @UserGroup AS INT\r\nSELECT @UserGroup = [ObjectType]\r\nFROM\r\n\tdbo.Personnel\r\nWHERE\r\n\tPersonnel.OID = 1\r\n\r\nUPDATE dbo.Personnel\r\nSET ObjectType = @UserGroup\r\nWHERE Personnel.OID = {_selPersonnel.Oid};\r\n\r\nINSERT INTO [User] (OID,Username,SiteRole)\r\nVALUES ({_selPersonnel.Oid},'{_personnelUsername}','{_personnelRole}');";
            var updateResult = await Session1.ExecuteNonQueryAsync(queryString);

            if (updateResult == 0)
            {
                //Error occurred
                Snackbar.Add("بروز خطای سیستمی. با پشتیبانی تماس بگیرید.", Severity.Error);
                return;
            }
        }
        catch (Exception)
        {
            Snackbar.Add("بروز خطای سیستمی. با پشتیبانی تماس بگیرید.", Severity.Error);
            return;
        }

        Session1 = new Session(DataLayer);
        var selUser = await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", _selPersonnel.Oid));
        if (selUser == null)
        {
            Snackbar.Add("بروز خطای سیستمی. با پشتیبانی تماس بگیرید.", Severity.Error);
            return;
        }

        selUser.Rigs.Add(selUser.ActiveRig);
        selUser.Save();
        Snackbar.Add($"دسترسی برای {selUser.PersonnelName} ایجاد شد. لطفاً کلمه عبور ایشان را نیز تعیین کنید.",
            Severity.Success);
        await UpgradeModal!.HideAsync();
    }

    #endregion

    public void Dispose()
    {
        Session1.Dispose();
    }
}