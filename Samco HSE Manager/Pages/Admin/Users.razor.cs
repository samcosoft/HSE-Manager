using BootstrapBlazor.Components;
using DevExpress.Blazor;
using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Samco_HSE_Manager.Pages.Admin
{
    public partial class Users : IDisposable
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
        private IEnumerable<User>? SystemUsers { get; set; }

        private IEnumerable<string>? RigRoles { get; set; }

        private readonly Dictionary<string, string> _roles = new()
        {
                   { "Admin", "مدیر ایمنی" },
                   { "Officer", "افسر ایمنی" },
                   { "Supervisor", "ناظر ایمنی" },
                   { "Medic", "پزشک" },
                   { "Teacher", "مدرس ایمنی" },
                   { "Disabled", "غیر فعال" },
        };
        private readonly IEnumerable<string> _status = new List<string>()
        {
            "فعال","خاتمه همکاری","انتقال به سایر شرکت‌ها"
        };
        private IEnumerable<Rig>? Rigs { get; set; }

        private DxGrid? UserGrid { get; set; }

        protected override async Task OnInitializedAsync()
        {
            Session1 = new Session(DataLayer);
            RigRoles = await File.ReadAllLinesAsync(Path.Combine(HostEnvironment.WebRootPath, "content", "RigRoles.txt"));
            await LoadInformation();
        }

        private async Task LoadInformation()
        {
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

        private void UserGridUnbound(GridUnboundColumnDataEventArgs e)
        {
            using var tempSession = new Session(DataLayer);
            var currentUser = tempSession.FindObject<User>(new BinaryOperator("Oid", ((User)e.DataItem).Oid));
            //if (currentWork == null) { return; }

            e.Value = e.FieldName switch
            {
                "EquipmentsCount" => (from itm in currentUser.MaterialRequests
                                      select itm.MaterialLists
                    into grp
                                      from listItm in grp
                                      select listItm).Sum(x => x.Counts),
                "WarningsCount" => currentUser.Warnings.Count,
                "AccidentsCount" => currentUser.AccidentReports.Count,
                "PracticeCount" => currentUser.Practices.Count,
                "TrainingCount" => currentUser.Trainings.Count,
                _ => e.Value
            };
        }

        private async Task UserGrid_EditStart(GridEditStartEventArgs e)
        {
            //prevent owner editing
            var dataItem = (User?)e.DataItem ?? new User(Session1);
            if (e.IsNew == false && dataItem.SiteRole == "Owner")
            {
                await ToastService.Error("خطا در ویرایش کاربر", "شما اجازه تغییر اطلاعات مدیر سیستم را ندارید.");
                e.Cancel = true;
            }
        }
        private void UserEditModel(GridCustomizeEditModelEventArgs e)
        {
            var dataItem = (User?)e.DataItem ?? new User(Session1);
            e.EditModel = dataItem;
        }

        private async Task OnEditModelSaving(GridEditModelSavingEventArgs e)
        {
            var editModel = (User)e.EditModel;
            //Validation
            if (string.IsNullOrEmpty(editModel.Username) || string.IsNullOrEmpty(editModel.SiteRole) || editModel.ActiveRig == null ||
                string.IsNullOrEmpty(editModel.PersonnelName) || string.IsNullOrEmpty(editModel.CurrentRole) || string.IsNullOrEmpty(editModel.Status))
            {
                await ToastService.Error("خطا در افزودن کاربر", "لطفاً موارد الزامی را تکمیل کنید.");
                e.Cancel = true;
                return;
            }

            if (Regex.IsMatch(editModel.Username, "[^a-z0-9.]+"))
            {
                await ToastService.Error("خطا در افزودن کاربر", "نام کاربری فقط می‌تواند به زبان انگلیسی شامل حروف کوچک، عدد و نقطه (.) باشد.");
                e.Cancel = true;
                return;
            }

            //Check user not existed before
            var selPersonnel =
                Session1.FindObject<Samco_HSE.HSEData.Personnel>(new BinaryOperator(nameof(Samco_HSE.HSEData.Personnel.NationalID), editModel.NationalID));
            if (e.IsNew)
            {
                if (selPersonnel != null)
                {
                    //User existed
                    await ToastService.Error("خطا در ثبت اطلاعات",
                        $"پرسنل با نام {selPersonnel.PersonnelName} با همین کد ملی در سیستم ثبت شده است. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.");
                    e.Cancel = true;
                    return;
                }
            }
            else
            {
                if (selPersonnel != null && selPersonnel.Oid != editModel.Oid)
                {
                    //User existed
                    await ToastService.Error("خطا در ثبت اطلاعات",
                        $"پرسنل با نام {selPersonnel.PersonnelName} با همین کد ملی در سیستم ثبت شده است. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.");
                    e.Cancel = true;
                    return;
                }
            }

            //check username
            var selPerson =
                Session1.FindObject<User>(new BinaryOperator(nameof(User.Username), editModel.Username));

            if (selPerson != null && e.IsNew)
            {
                //Duplicate username
                await ToastService.Error("خطا در ثبت اطلاعات",
                    "کاربر دیگری با همین نام کاربری وجود دارد. لطفاً نام کاربری دیگری را برگزینید.");
                e.Cancel = true;
                return;
            }

            if (selPerson != null && selPerson.Oid != editModel.Oid)
            {
                //Duplicate username
                await ToastService.Error("خطا در ثبت اطلاعات",
                    "کاربر دیگری با همین نام کاربری وجود دارد. لطفاً نام کاربری دیگری را برگزینید.");
                e.Cancel = true;
                return;
            }
            while (editModel.Rigs.Any())
            {
                editModel.Rigs.Remove(editModel.Rigs[0]);
            }
            editModel.Rigs.Add(editModel.ActiveRig);
            editModel.Save();
            await LoadInformation();
        }

        private async Task OnDataItemDeleting(GridDataItemDeletingEventArgs e)
        {
            var dataItem =
                await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", (e.DataItem as User)!.Oid));
            if (dataItem != null)
            {
                //prevent delete Owner
                if (dataItem.SiteRole == "Owner")
                {
                    await ToastService.Error("خطا در حذف کاربر", "شما اجازه حذف مدیر سیستم را ندارید.");
                    e.Cancel = true;
                    return;
                }

                //prevent delete  current user
                if (dataItem.Oid == SamcoSoftShared.CurrentUser?.Oid)
                {
                    await ToastService.Error("خطا در حذف کاربر", "شما اجازه‌ی حذف اطلاعات کاربری خود را ندارید.");
                    e.Cancel = true;
                    return;
                }
                dataItem.Delete();
                await LoadInformation();
            }
        }

        #region ChangePassword

        private Modal? PassModal { get; set; }
        private string? _newPass, _confPass;
        private async Task OnNewPassBtnClick()
        {
            if (UserGrid?.SelectedDataItems.Any() == false)
            {
                await ToastService.Warning("خطا در تغییر رمز", "لطفاً یک کاربر را از لیست زیر انتخاب کنید.");
                return;
            }

            await PassModal!.Show();
        }

        private async Task PassOkBtnClick()
        {
            //check password
            if (string.IsNullOrWhiteSpace(_newPass))
            {
                await MessageService.Show(new MessageOption()
                {
                    Color = Color.Danger,
                    IsAutoHide = true,
                    ShowDismiss = true,
                    Content = "کلمه عبور جدید را وارد کنید."
                });
                return;
            }

            if (_newPass != _confPass)
            {
                await MessageService.Show(new MessageOption()
                {
                    Color = Color.Danger,
                    IsAutoHide = true,
                    ShowDismiss = true,
                    Content = "کلمه‌های عبور وارد شده با هم همخوانی ندارند."
                });
                return;
            }

            if (SamcoSoftShared.PasswordStrengthChecker(_newPass) < SamcoSoftShared.PasswordScore.Medium)
            {
                await MessageService.Show(new MessageOption()
                {
                    Color = Color.Warning,
                    IsAutoHide = true,
                    ShowDismiss = true,
                    Content = "کلمه عبور وارد شده بسیار ضعیف است. کلمه عبور پیچیده‌تری انتخاب کنید."
                });
                return;
            }

            var selUser = (User)UserGrid!.SelectedDataItem;
            selUser.Password = BCrypt.Net.BCrypt.EnhancedHashPassword(_newPass);
            selUser.Save();
            await MessageService.Show(new MessageOption()
            {
                Color = Color.Success,
                IsAutoHide = true,
                ShowDismiss = true,
                Content = $"کلمه عبور برای کاربر {selUser.Username} با موفقیت تغییر یافت."
            });
            _newPass = string.Empty;
            _confPass = string.Empty;
            await PassModal!.Close();
        }

        #endregion

        #region Permission

        private Modal? PermitModal { get; set; }
        private IEnumerable<Rig>? PermitRigList { get; set; }

        private async Task OnPermitBtnClick()
        {
            if (UserGrid!.SelectedDataItems.Any() == false)
            {
                await ToastService.Warning("خطا در تعیین دسترسی", "لطفاً یک کاربر را از لیست زیر انتخاب کنید.");
                return;
            }
            var selUser = (User)UserGrid.SelectedDataItem;
            if (selUser.SiteRole == "Owner")
            {
                await ToastService.Warning("خطا در تعیین دسترسی", "مدیر سیستم همواره به همه اطلاعات دسترسی دارد.");
                return;
            }
            if (selUser.Oid == SamcoSoftShared.CurrentUser?.Oid)
            {
                await ToastService.Warning("خطا در تعیین دسترسی", "شما نمی‌توانید سطح دسترسی خود را تغییر دهید.");
                return;
            }
            PermitRigList = selUser.Rigs.ToList();
            await PermitModal!.Show();
        }

        private async Task PermitOkBtnClick()
        {
            if (PermitRigList?.Any() == false)
            {
                await MessageService.Show(new MessageOption()
                {
                    Color = Color.Danger,
                    Content = "لطفاً حداقل یک دکل را انتخاب کنید.",
                    IsAutoHide = true
                });
                return;
            }

            var selUser = (User)UserGrid!.SelectedDataItem;
            while (selUser.Rigs.Any())
            {
                selUser.Rigs.Remove(selUser.Rigs[0]);
            }
            selUser.Rigs.AddRange(PermitRigList);
            selUser.Rigs.Add(selUser.ActiveRig);
            selUser.Save();
            await MessageService.Show(new MessageOption()
            {
                Color = Color.Success,
                Content = "دسترسی‌ها با موفقیت ثبت شدند.",
                IsAutoHide = true
            });
            await PermitModal!.Close();
        }

        #endregion

        #region UpgradePersonnel

        private DxPopup? UpgradeModal { get; set; }
        private IEnumerable<Samco_HSE.HSEData.Personnel> PersonnelList { get; set; } = null!;
        private Samco_HSE.HSEData.Personnel? _selPersonnel;
        private string? _personnelUsername, _personnelRole;
        private async Task OnUpgradeBtnClick()
        {
            PersonnelList = Session1.Query<Samco_HSE.HSEData.Personnel>().AsEnumerable().Where(x => x.GetType() != typeof(User));
            _selPersonnel = null;
            _personnelUsername = string.Empty;
            _personnelRole = string.Empty;
            await UpgradeModal!.ShowAsync();
        }
        #endregion

        private async Task UpgradeOkBtnClick()
        {
            //Validation
            if (string.IsNullOrEmpty(_personnelUsername) || string.IsNullOrEmpty(_personnelRole) || _selPersonnel == null)
            {
                await ToastService.Error("خطا در افزودن کاربر", "لطفاً موارد الزامی را تکمیل کنید.");
                return;
            }

            if (Regex.IsMatch(_personnelUsername, "[^a-z0-9.]+"))
            {
                await ToastService.Error("خطا در افزودن کاربر", "نام کاربری فقط می‌تواند به زبان انگلیسی شامل حروف کوچک، عدد و نقطه (.) باشد.");
                return;
            }

            //check username
            var selPerson =
                Session1.FindObject<User>(new BinaryOperator(nameof(User.Username), _personnelUsername));

            if (selPerson != null)
            {
                //Duplicate username
                await ToastService.Error("خطا در ثبت اطلاعات",
                    "کاربر دیگری با همین نام کاربری وجود دارد. لطفاً نام کاربری دیگری را برگزینید.");
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
                    await ToastService.Error("خطا در ثبت اطلاعات",
                        "بروز خطای سیستمی. با پشتیبانی تماس بگیرید.");
                    return;
                }
            }
            catch (Exception e)
            {
                await ToastService.Error("خطا در ثبت اطلاعات",
                    "بروز خطای سیستمی. با پشتیبانی تماس بگیرید." + Environment.NewLine + e.Message);
                return;
            }

            Session1 = new Session(DataLayer);
            var selUser = await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", _selPersonnel.Oid));
            if (selUser == null)
            {
                await ToastService.Error("خطا در ثبت اطلاعات",
                    "بروز خطای سیستمی. با پشتیبانی تماس بگیرید.");
                return;
            }

            selUser.Rigs.Add(selUser.ActiveRig);
            selUser.Save();
            await MessageService.Show(new MessageOption()
            {
                Color = Color.Success,
                IsAutoHide = true,
                ShowDismiss = true,
                Content = $"دسترسی برای {selUser.PersonnelName} ایجاد شد. لطفاً کلمه عبور ایشان را نیز تعیین کنید."
            });
            await UpgradeModal!.CloseAsync();
            await LoadInformation();
        }

        public void Dispose()
        {
            Session1.Dispose();
        }
    }
}
