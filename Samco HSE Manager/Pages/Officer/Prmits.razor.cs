using BootstrapBlazor.Components;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using System.Diagnostics.CodeAnalysis;
using DevExpress.Blazor;
using DevExpress.Data.Filtering;

namespace Samco_HSE_Manager.Pages.Officer
{
    public partial class Prmits : IDisposable
    {
        [Inject] private IDataLayer DataLayer { get; set; } = null!;
        [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;
        [Inject]
        [NotNull]
        private ToastService? ToastService { get; set; }

        private Session Session1 { get; set; } = null!;
        private IEnumerable<Permit>? PermitsList { get; set; }
        private IEnumerable<Samco_HSE.HSEData.Personnel>? PersonnelList { get; set; }
        private IEnumerable<Rig>? Rigs { get; set; }
        private Rig? _selRig;
        private IEnumerable<string> _location = null!;
        private IEnumerable<string> _permitType = null!;
        private DxGrid? PermitGrid { get; set; }

        protected override async Task OnInitializedAsync()
        {
            Session1 = new Session(DataLayer);
            _location = await File.ReadAllLinesAsync(Path.Combine(HostEnvironment.WebRootPath, "content", "RigLocation.txt"));
            _permitType = await File.ReadAllLinesAsync(Path.Combine(HostEnvironment.WebRootPath, "content", "PermitTypes.txt"));
            await LoadInformation();
        }

        public void Dispose()
        {
            Session1.Dispose();
        }

        private async Task LoadInformation()
        {
            if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
            {
                var loggedUser =
                    await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));

                PermitsList = Session1.Query<Permit>().Where(x => loggedUser.Rigs.Contains(x.WorkID.RigNo));
                PersonnelList = await Session1.Query<Samco_HSE.HSEData.Personnel>().Where(x => loggedUser.Rigs.Contains(x.ActiveRig)).ToListAsync();
                Rigs = loggedUser.Rigs;
            }
            else
            {
                //Owner
                PermitsList = Session1.Query<Permit>();
                PersonnelList = await Session1.Query<Samco_HSE.HSEData.Personnel>().ToListAsync();
                Rigs = await Session1.Query<Rig>().ToListAsync();
            }
        }
        private void PermitGrid_CustomizeElement(GridCustomizeElementEventArgs e)
        {
            if (e.ElementType != GridElementType.DataRow) return;
            var status = (DateTime?)e.Grid.GetRowValue(e.VisibleIndex, nameof(Permit.EndTime));
            e.CssClass = status == null ? "danger-item" : "";
        }
        private void PermitGridUnbound(GridUnboundColumnDataEventArgs itm)
        {
            if (itm.FieldName != "IsClosed") return;
            var permit = (Permit)itm.DataItem;
            itm.Value = permit.EndTime != null;
        }
        private void PermitEditModel(GridCustomizeEditModelEventArgs e)
        {
            var dataItem = (Permit?)e.DataItem ?? new Permit(Session1);
            if (dataItem.WorkID != null) _selRig = dataItem.WorkID.RigNo;
            e.EditModel = dataItem;
        }
        private async Task OnEditModelSaving(GridEditModelSavingEventArgs e)
        {
            var editModel = (Permit)e.EditModel;
            var loggedUser =
                await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
            //Validation
            if (_selRig == null || editModel.ArchiveNo == null || editModel.Holder == null ||
                editModel.Responsible == null || editModel.Approver == null || string.IsNullOrEmpty(editModel.PermitType) ||
                string.IsNullOrEmpty(editModel.Location) || string.IsNullOrEmpty(editModel.Description))
            {
                await ToastService.Error("خطا در ثبت مجوز", "لطفاً موارد الزامی را تکمیل کنید.");
                e.Cancel = true;
                return;
            }

            if (editModel.EndTime != null && editModel.EndTime < editModel.StartTime)
            {
                await ToastService.Error("خطا در ثبت مجوز", "تاریخ و ساعت پایان کار نباید قبل از شروع آن باشد.");
                e.Cancel = true;
                return;
            }

            var wellWork = await Session1.Query<WellWork>().FirstOrDefaultAsync(x => x.RigNo.Oid == _selRig.Oid &&
                x.IsActive);

            if (wellWork == null)
            {
                await ToastService.Error("خطا در ثبت مجوز", "دکل انتخاب شده در هیچ پروژه‌ای فعال نیست.");
                e.Cancel = true;
                return;
            }

            //Check serial number
            var prevItem =
                await Session1.FindObjectAsync<Permit>(
                    new BinaryOperator(nameof(Permit.ArchiveNo), editModel.ArchiveNo));

            if (prevItem != null && prevItem.Oid != editModel.Oid)
            {
                await ToastService.Error("خطا در ثبت مجوز", "شماره سریال مجوز تکراری است. لطفاً آن را بررسی کرده و دوباره تلاش کنید.");
                e.Cancel = true;
                return;
            }

            editModel.WorkID = wellWork;
            editModel.Agent ??= loggedUser;
            editModel.Save();

            await LoadInformation();
        }
        private async Task OnDataItemDeleting(GridDataItemDeletingEventArgs e)
        {
            var dataItem =
                await Session1.FindObjectAsync<Permit>(new BinaryOperator("Oid", (e.DataItem as Permit)!.Oid));
            dataItem?.Delete();
            await LoadInformation();
        }
        private async Task OnPrintBtnClick()
        {
            await ToastService.Show(new ToastOption
            {
                Content = "سیستم در حال ایجاد فایل است. لطفاً تا دانلود گزارش شکیبا باشید...",
                Title = "دریافت گزارش",
                Category = ToastCategory.Information,
                Delay = 6000,
                ForceDelay = true
            });

            //var getReport = PersonnelGrid?.ExportToXlsxAsync("Personnel", new GridXlExportOptions
            await PermitGrid?.ExportToXlsxAsync("Permits", new GridXlExportOptions
            {
                CustomizeSheet = SamcoSoftShared.CustomizeSheet,
                CustomizeCell = SamcoSoftShared.CustomizeCell,
                CustomizeSheetFooter = SamcoSoftShared.CustomizeFooter
            })!;
        }
        private void ColumnChooserOnClick()
        {
            PermitGrid?.ShowColumnChooser(".column-chooser-button");
        }
    }
}
