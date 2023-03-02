using BootstrapBlazor.Components;
using DevExpress.Blazor;
using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using System.Diagnostics.CodeAnalysis;

namespace Samco_HSE_Manager.Pages.Officer;

public partial class StopCards : IDisposable
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;

    [Inject]
    [NotNull]
    private ToastService? ToastService { get; set; }

    private Session Session1 { get; set; } = null!;
    private IEnumerable<StopCard>? StopsList { get; set; }
    private IEnumerable<Samco_HSE.HSEData.Personnel>? PersonnelList { get; set; }

    private IEnumerable<string>? RigRoles { get; set; }

    private readonly IEnumerable<string> _stopRisk = new List<string>
    {
        "ایمن","کم","متوسط","زیاد","خیلی زیاد"
    };
    private readonly IEnumerable<string> _stopProb = new List<string>
    {
       "خیلی کم","کم","متوسط","زیاد","خیلی زیاد"
    };
    private readonly IEnumerable<string> _status = new List<string>
    {
        "باز","بسته","لغو شده"
    };

    private IEnumerable<string> _category = null!;
    private IEnumerable<string> _location = null!;
    private Rig? _selRig;
    private IEnumerable<Rig>? Rigs { get; set; }

    private DxGrid? StopGrid { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
        RigRoles = await File.ReadAllLinesAsync(Path.Combine(HostEnvironment.WebRootPath, "content", "RigRoles.txt"));
        _category = await File.ReadAllLinesAsync(Path.Combine(HostEnvironment.WebRootPath, "content", "STOPCardCategory.txt"));
        _location = await File.ReadAllLinesAsync(Path.Combine(HostEnvironment.WebRootPath, "content", "RigLocation.txt"));
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

            StopsList = Session1.Query<StopCard>().Where(x => loggedUser.Rigs.Contains(x.WorkID.RigNo));
            PersonnelList = await Session1.Query<Samco_HSE.HSEData.Personnel>().Where(x => loggedUser.Rigs.Contains(x.ActiveRig)).ToListAsync();
            Rigs = loggedUser.Rigs;
        }
        else
        {
            //Owner
            StopsList = Session1.Query<StopCard>();
            PersonnelList = await Session1.Query<Samco_HSE.HSEData.Personnel>().ToListAsync();
            Rigs = await Session1.Query<Rig>().ToListAsync();
        }
    }
    private void StopGrid_CustomizeElement(GridCustomizeElementEventArgs e)
    {
        if (e.ElementType == GridElementType.DataRow)
        {
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

    #region StopGrid

    private List<string>? _photoList;
    private bool _photoShowVisible;
    private void StopEditModel(GridCustomizeEditModelEventArgs e)
    {
        var dataItem = (StopCard?)e.DataItem ?? new StopCard(Session1);
        if (dataItem.WorkID != null) _selRig = dataItem.WorkID.RigNo;
        //Get related photos
        _photoList = null;
        var path = Path.Combine(HostEnvironment.WebRootPath, "upload", "STOPCards", dataItem.Oid.ToString());
        if (Directory.Exists(path))
            _photoList = Directory.GetFiles(path).Select(x => Path.Combine(NavigationManager.BaseUri, "upload", "STOPCards", dataItem.Oid.ToString(), x.Split("\\").Last())).ToList();
        e.EditModel = dataItem;
    }

    private void ShowImages(int oid)
    {
        _photoList = null;
        //Get related photos
        var path = Path.Combine(HostEnvironment.WebRootPath, "upload", "STOPCards", oid.ToString());
        if (Directory.Exists(path))
            _photoList = Directory.GetFiles(path).Select(x => Path.Combine(NavigationManager.BaseUri, "upload", "STOPCards", oid.ToString(), x.Split("\\").Last())).ToList();
        if (_photoList != null && _photoList.Any())
        {
            _photoShowVisible = true;
        }
        else
        {
            ToastService.Warning("نمایش تصویر", "تصویری وجود ندارد.");
        }
            
    }

    private async Task OnEditModelSaving(GridEditModelSavingEventArgs e)
    {
        var editModel = (StopCard)e.EditModel;
        var loggedUser =
            await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
        //Validation
        if (_selRig == null || editModel.Reporter == null || editModel.ReportDate == null ||
            string.IsNullOrEmpty(editModel.Location) || string.IsNullOrEmpty(editModel.Category) ||
            string.IsNullOrEmpty(editModel.Observation))
        {
            await ToastService.Error("خطا در ثبت STOP Card", "لطفاً موارد الزامی را تکمیل کنید.");
            e.Cancel = true;
            return;
        }

        var wellWork = await Session1.Query<WellWork>().FirstOrDefaultAsync(x => x.RigNo.Oid == _selRig.Oid &&
            x.IsActive);

        if (wellWork == null)
        {
            await ToastService.Error("خطا در ثبت STOP Card", "دکل انتخاب شده در هیچ پروژه‌ای فعال نیست.");
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
            await Session1.FindObjectAsync<StopCard>(new BinaryOperator("Oid", (e.DataItem as StopCard)!.Oid));
        dataItem?.Delete();
        await LoadInformation();
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

        //var getReport = PersonnelGrid?.ExportToXlsxAsync("Personnel", new GridXlExportOptions
        await StopGrid?.ExportToXlsxAsync("StopCards", new GridXlExportOptions
        {
            CustomizeSheet = SamcoSoftShared.CustomizeSheet,
            CustomizeCell = SamcoSoftShared.CustomizeCell,
            CustomizeSheetFooter = SamcoSoftShared.CustomizeFooter
        })!;
    }
    private void ColumnChooserOnClick()
    {
        StopGrid?.ShowColumnChooser(".column-chooser-button");
    }

    #endregion
}