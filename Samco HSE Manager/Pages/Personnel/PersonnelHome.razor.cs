using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using Samco_HSE.HSEData;
using Syncfusion.Blazor.Grids;
using Syncfusion.Blazor.Inputs;
using Syncfusion.Blazor.Popups;

namespace Samco_HSE_Manager.Pages.Personnel;

public partial class PersonnelHome : IDisposable
{
    [CascadingParameter]
    private Task<AuthenticationState> AuthenticationStateTask { get; set; } = null!;
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

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
    private SfGrid<StopCard>? StopGrid { get; set; }
    private bool _isOpen;
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
            StateHasChanged();
        }
        catch (Exception)
        {
            //Ignore
        }
    }
    private void Customize_Row(RowDataBoundEventArgs<StopCard> args)
    {
        var status = args.Data.Status;
        if (status != null)
        {
            args.Row.AddClass(new[]
            {
                status switch
                {
                    "باز" => "danger-item",
                    "بسته" => "safe-item",
                    _ => ""
                }
            });
        }
    }

    #region StopCard
    private StopCard? SelectedCard { get; set; }
    private string? _cardEditorHeader;
    private List<string>? _photoList;
    private bool _photoShowVisible;
    private SfDialog EditPopup { get; set; } = null!;

    private void OnNewBtnClick()
    {
        _cardEditorHeader = "افزودن کارت جدید";
        SelectedCard = new StopCard(Session1);
        EditPopup.ShowAsync();
    }

    private async void OnEditBtnClick()
    {
        var selCard = StopGrid!.SelectedRecords.FirstOrDefault();
        if (selCard == null)
        {
            Snackbar.Add("یک مورد را انتخاب کنید.", Severity.Warning);
            return;
        }
        if (selCard.IsApproved)
        {
            Snackbar.Add("امکان ویرایش کارت تأیید شده وجود ندارد.", Severity.Warning);
            return;
        }
        SelectedCard = selCard;
        _cardEditorHeader = $"ویرایش کارت {SelectedCard?.Oid} ({SelectedCard?.ReportDate!.Value.Year})";
        await EditPopup.ShowAsync();
    }
    private async void SaveCardInfoClick()
    {
        //Validation
        if (string.IsNullOrEmpty(SelectedCard!.Observation))
        {
            Snackbar.Add("لطفاً موارد الزامی را تکمیل کنید.", Severity.Error);
            return;
        }

        var loggedUser =
            await Session1.FindObjectAsync<Samco_HSE.HSEData.Personnel>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));

        if (string.IsNullOrEmpty(SelectedCard!.Observation))
        {
            Snackbar.Add("برای شما هیچ محل کاری تعریف نشده است.", Severity.Error);
            return;
        }

        var wellWork = await Session1.Query<WellWork>().FirstOrDefaultAsync(x => x.RigNo.Oid == loggedUser.ActiveRig.Oid &&
            x.IsActive);

        if (wellWork == null)
        {
            Snackbar.Add("محل انتخاب شده در هیچ پروژه‌ای فعال نیست.", Severity.Error);
            return;
        }
        SelectedCard!.WorkID = wellWork;
        SelectedCard!.Reporter = loggedUser;
        SelectedCard!.ReportDate = DateTime.Now;
        SelectedCard!.Save();
        SelectedCard = null;
        await EditPopup.HideAsync();
        await LoadInformation();
    }
    private async Task OnDelBtnClick()
    {
        var selCard = StopGrid!.SelectedRecords.FirstOrDefault();
        if (selCard == null)
        {
            Snackbar.Add("یک مورد را انتخاب کنید.", Severity.Warning);
            return;
        }
        if (selCard.IsApproved)
        {
            Snackbar.Add("امکان حذف کارت تأیید شده وجود ندارد.", Severity.Warning);
            return;
        }

        selCard.Delete();
        await LoadInformation();
    }

    private string? DataId { get; set; }
    private void CardUploadStart(BeforeUploadEventArgs obj)
    {
        if (string.IsNullOrEmpty(SelectedCard!.Observation))
        {
            Snackbar.Add("لطفاً ابتدا موارد الزامی را تکمیل کنید.", Severity.Warning);
            obj.Cancel = true;
            return;
        }
        SelectedCard!.Save();
        DataId = SelectedCard.Oid.ToString();
        //obj.RequestData.Add("CardId", SelectedCard!.Oid.ToString());
    }
    private void ShowImages(int oid)
    {
        _photoList = null;
        //Get related photos
        var path = Path.Combine(HostEnvironment.WebRootPath, "upload", "STOPCards", oid.ToString());
        if (Directory.Exists(path))
            _photoList = Directory.GetFiles(path).Select(x =>
                    Path.Combine(NavigationManager.BaseUri, "upload", "STOPCards", oid.ToString(),
                        x.Split("\\").Last()))
                .ToList();
        if (_photoList != null && _photoList.Any())
        {
            _photoShowVisible = true;
        }
        else
        {
            Snackbar.Add("تصویری وجود ندارد.", Severity.Warning);
        }
    }
    #endregion
}