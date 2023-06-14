using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using MudBlazor;
using Syncfusion.Blazor.Grids;
using Action = Syncfusion.Blazor.Grids.Action;

namespace Samco_HSE_Manager.Pages.Officer;

public partial class StopCards : IDisposable
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private Session Session1 { get; set; } = null!;
    private IEnumerable<StopCard>? StopsList { get; set; }
    private IEnumerable<Samco_HSE.HSEData.Personnel>? PersonnelList { get; set; }

    private IEnumerable<string>? RigRoles { get; set; }

    private readonly IEnumerable<string> _stopRisk = new List<string>
    {
        "ایمن", "کم", "متوسط", "زیاد", "خیلی زیاد"
    };

    private readonly IEnumerable<string> _stopProb = new List<string>
    {
        "خیلی کم", "کم", "متوسط", "زیاد", "خیلی زیاد"
    };

    private readonly IEnumerable<string> _status = new List<string>
    {
        "باز", "بسته", "لغو شده"
    };

    private IEnumerable<string> _category = null!;
    private IEnumerable<string> _location = null!;
    private Rig? _selRig;
    private IEnumerable<Rig>? Rigs { get; set; }

    private SfGrid<StopCard>? StopGrid { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
        RigRoles = await File.ReadAllLinesAsync(Path.Combine(HostEnvironment.WebRootPath, "content", "RigRoles.txt"));
        _category = await File.ReadAllLinesAsync(Path.Combine(HostEnvironment.WebRootPath, "content",
            "STOPCardCategory.txt"));
        _location = await File.ReadAllLinesAsync(
            Path.Combine(HostEnvironment.WebRootPath, "content", "RigLocation.txt"));
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
            PersonnelList = await Session1.Query<Samco_HSE.HSEData.Personnel>()
                .Where(x => loggedUser.Rigs.Contains(x.ActiveRig)).ToListAsync();
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

    #region StopGrid

    private List<string>? _photoList;
    private bool _photoShowVisible;

    private void StopGrid_Action(ActionEventArgs<StopCard> e)
    {
        switch (e.RequestType)
        {
            case Action.Add:
                e.Data ??= new StopCard(Session1);
                break;
            case Action.BeginEdit:
                var dataItem = e.RowData;
                if (dataItem.WorkID != null) _selRig = dataItem.WorkID.RigNo;
                //Get related photos
                _photoList = null;
                var path = Path.Combine(HostEnvironment.WebRootPath, "upload", "STOPCards", dataItem.Oid.ToString());
                if (Directory.Exists(path))
                    _photoList = Directory.GetFiles(path).Select(x => Path.Combine(NavigationManager.BaseUri, "upload",
                        "STOPCards", dataItem.Oid.ToString(), x.Split("\\").Last())).ToList();
                e.Data = Session1.GetObjectByKey<StopCard>(e.RowData.Oid);
                break;
            case Action.Save:
                var editModel = e.Data;
                var loggedUser =
                    Session1.FindObject<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
                //Validation
                if (_selRig == null || editModel.Reporter == null || editModel.ReportDate == null ||
                    string.IsNullOrEmpty(editModel.Location) || string.IsNullOrEmpty(editModel.Category) ||
                    string.IsNullOrEmpty(editModel.Observation))
                {
                    Snackbar.Add("لطفاً موارد الزامی را تکمیل کنید.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                var wellWork = Session1.Query<WellWork>().FirstOrDefault(x => x.RigNo.Oid == _selRig.Oid &&
                    x.IsActive);

                if (wellWork == null)
                {
                    Snackbar.Add("دکل انتخاب شده در هیچ پروژه‌ای فعال نیست.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                //Check date
                if (editModel.ReportDate < wellWork.StartDate)
                {
                    Snackbar.Add("تاریخ انتخاب شده با تاریخ شروع پروژه مطابقت ندارد.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                editModel.WorkID = wellWork;
                editModel.Agent ??= loggedUser;
                editModel.Save();
                break;
            case Action.Delete:
                if (SamcoSoftShared.CurrentUserRole > SamcoSoftShared.SiteRoles.Supervisor)
                {
                    Snackbar.Add("شما اجازه ویرایش تجهیزات را ندارید.", Severity.Warning);
                    e.Cancel = true;
                    return;
                }

                e.RowData.Delete();
                break;
        }
    }

    private async Task StopToolbarClickHandler(Syncfusion.Blazor.Navigations.ClickEventArgs args)
    {
        if (args.Item.Id == "stopGrid_Excel Export")
        {
            Snackbar.Add("سیستم در حال ایجاد فایل است. لطفاً تا دانلود گزارش شکیبا باشید...", Severity.Info);
            var exportProperties = new ExcelExportProperties
            {
                FileName = "StopCardList.xlsx"
            };
            await StopGrid!.ExportToExcelAsync(exportProperties);
        }
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

    private async Task RigChanged(Rig obj)
    {
        PersonnelList = await Session1.Query<Samco_HSE.HSEData.Personnel>().Where(x => x.ActiveRig.Oid == obj.Oid)
            .ToListAsync();
    }
    #endregion
}