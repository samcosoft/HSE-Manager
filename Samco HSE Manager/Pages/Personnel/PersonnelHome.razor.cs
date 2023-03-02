using System.Diagnostics.CodeAnalysis;
using BootstrapBlazor.Components;
using DevExpress.Blazor;
using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Samco_HSE.HSEData;

namespace Samco_HSE_Manager.Pages.Personnel
{
    public partial class PersonnelHome : IDisposable
    {
        [CascadingParameter]
        private Task<AuthenticationState> AuthenticationStateTask { get; set; } = null!;
        [Inject] private IDataLayer DataLayer { get; set; } = null!;

        [Inject]
        [NotNull]
        private ToastService? ToastService { get; set; }

        private Session Session1 { get; set; } = null!;
        private IEnumerable<StopCard>? StopsList { get; set; }
        private readonly IEnumerable<string> _stopRisk = new List<string>
        {
            "ایمن", "کم","متوسط","زیاد","خیلی زیاد"
        };
        private readonly IEnumerable<string> _stopProb = new List<string>
        {
            "خیلی کم",  "کم","متوسط","زیاد","خیلی زیاد"
        };
        private DxGrid? StopGrid { get; set; }
        protected override async Task OnInitializedAsync()
        {
            Session1 = new Session(DataLayer);
            await LoadInformation();
        }

        public void Dispose()
        {
            Session1.Dispose();
        }
        private async Task LoadInformation()
        {
            try
            {
                using var session1 = new Session(DataLayer);
                var loggedUser = await session1.FindObjectAsync<Samco_HSE.HSEData.Personnel>(new BinaryOperator("NationalID",
                    (await AuthenticationStateTask).User.Identity?.Name));
                SamcoSoftShared.CurrentUserId = loggedUser.Oid;
                SamcoSoftShared.CurrentUserRole = SamcoSoftShared.SiteRoles.Personnel;
                StopsList = await Session1.Query<StopCard>().Where(x => x.Reporter.Oid == loggedUser.Oid).ToListAsync();
            }
            catch (Exception)
            {
                //Ignore
            }
        }
        private void StopGrid_CustomizeElement(GridCustomizeElementEventArgs e)
        {
            if (e.ElementType != GridElementType.DataRow) return;
            var status = (string?)e.Grid.GetRowValue(e.VisibleIndex, "Status");
            if (status != null)
            {
                e.CssClass = status switch
                {
                    "باز" => "danger-item",
                    "بسته" => "safe-item",
                    _ => ""
                };
            }
        }
        private void StopGridUnbound(GridUnboundColumnDataEventArgs itm)
        {
            if (itm.FieldName != "Risk") return;
            var card = (StopCard)itm.DataItem;
            var sever = _stopRisk.ToList().IndexOf(card.Severity) + 1;
            var prob = _stopProb.ToList().IndexOf(card.Probablety) + 1;
            var result = (sever * prob) switch
            {
                >= 16 => "High",
                >= 9 => "Medium",
                _ => "Low"
            };
            itm.Value = result;
        }

        #region StopCard
        private StopCard? SelectedCard { get; set; }
        private string? _cardEditorHeader;
        private DxPopup EditPopup { get; set; } = null!;

        private void OnNewBtnClick()
        {
            _cardEditorHeader = "افزودن کارت جدید";
            SelectedCard = new StopCard(Session1);
            EditPopup.ShowAsync();
        }

        private async void OnEditBtnClick()
        {
            var selCard = StopGrid!.SelectedDataItem;
            if (selCard == null)
            {
                await ToastService.Warning("خطا در ویرایش", "یک مورد را انتخاب کنید.");
                return;
            }
            if (((StopCard)selCard).IsApproved)
            {
                await ToastService.Warning("خطا در ویرایش", "امکان ویرایش کارت تأیید شده وجود ندارد.");
                return;
            }
            SelectedCard = (StopCard)StopGrid!.SelectedDataItem;
            _cardEditorHeader = $"ویرایش کارت {SelectedCard?.Oid} ({SelectedCard?.ReportDate!.Value.Year})";
            await EditPopup.ShowAsync();
        }
        private async void SaveCardInfoClick()
        {
            //Validation
            if (string.IsNullOrEmpty(SelectedCard!.Observation))
            {
                await ToastService.Error("خطا در ثبت STOP Card", "لطفاً موارد الزامی را تکمیل کنید.");
                return;
            }

            var loggedUser =
                await Session1.FindObjectAsync<Samco_HSE.HSEData.Personnel>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));

            if (string.IsNullOrEmpty(SelectedCard!.Observation))
            {
                await ToastService.Error("خطا در ثبت STOP Card", "برای شما هیچ محل کاری تعریف نشده است.");
                return;
            }

            var wellWork = await Session1.Query<WellWork>().FirstOrDefaultAsync(x => x.RigNo.Oid == loggedUser.ActiveRig.Oid &&
                x.IsActive);

            if (wellWork == null)
            {
                await ToastService.Error("خطا در ثبت STOP Card", "محل انتخاب شده در هیچ پروژه‌ای فعال نیست.");
                return;
            }
            SelectedCard!.WorkID = wellWork;
            SelectedCard!.Reporter = loggedUser;
            SelectedCard!.ReportDate = DateTime.Now;
            SelectedCard!.Save();
            SelectedCard = null;
            await EditPopup.CloseAsync();
            await LoadInformation();
        }
        private async Task OnDelBtnClick()
        {
            var selCard = StopGrid!.SelectedDataItem;
            if (selCard == null)
            {
                await ToastService.Warning("خطا در حذف", "یک مورد را انتخاب کنید.");
                return;
            }
            if (((StopCard)selCard).IsApproved)
            {
                await ToastService.Warning("خطا در حذف", "امکان حذف کارت تأیید شده وجود ندارد.");
                return;
            }

            ((StopCard)selCard).Delete();
            await LoadInformation();
        }

        private void CardUploadStart(FileUploadStartEventArgs obj)
        {
            if (string.IsNullOrEmpty(SelectedCard!.Observation))
            {
                ToastService.Error("خطا در ارسال فایل", "لطفاً ابتدا موارد الزامی را تکمیل کنید.");
                return;
            }
            SelectedCard!.Save();
            obj.RequestData.Add("CardId", SelectedCard!.Oid.ToString());
        }

        #endregion
    }
}
