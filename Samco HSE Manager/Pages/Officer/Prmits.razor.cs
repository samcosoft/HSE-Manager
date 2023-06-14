using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using DevExpress.Data.Filtering;
using MudBlazor;
using Syncfusion.Blazor.Grids;
using Action = Syncfusion.Blazor.Grids.Action;

namespace Samco_HSE_Manager.Pages.Officer;

public partial class Prmits : IDisposable
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private Session Session1 { get; set; } = null!;
    private IEnumerable<Permit>? PermitsList { get; set; }
    private IEnumerable<Samco_HSE.HSEData.Personnel>? PersonnelList { get; set; }
    private IEnumerable<Rig>? Rigs { get; set; }
    private Rig? _selRig;
    private IEnumerable<string> _location = null!;
    private IEnumerable<string> _permitType = null!;
    private TimeSpan? _startTime, _endTime;
    private SfGrid<Permit>? PermitGrid { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
        _location = await File.ReadAllLinesAsync(
            Path.Combine(HostEnvironment.WebRootPath, "content", "RigLocation.txt"));
        _permitType =
            await File.ReadAllLinesAsync(Path.Combine(HostEnvironment.WebRootPath, "content", "PermitTypes.txt"));
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
            PersonnelList = await Session1.Query<Samco_HSE.HSEData.Personnel>()
                .Where(x => loggedUser.Rigs.Contains(x.ActiveRig)).ToListAsync();
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

    private void Customize_Row(RowDataBoundEventArgs<Permit> args)
    {
        if (args.Data.EndTime == null)
        {
            args.Row.AddClass(new[] { "expired-item" });
        }
    }

    private void PermitGrid_Action(ActionEventArgs<Permit> e)
    {
        switch (e.RequestType)
        {
            case Action.Add:
                e.Data ??= new Permit(Session1);
                break;
            case Action.BeginEdit:
                e.Data = Session1.FindObject<Permit>(new BinaryOperator("Oid", e.RowData.Oid));
                _selRig = e.Data.WorkID.RigNo;
                break;
            case Action.Save:
                var editModel = e.Data;
                var loggedUser =
                    Session1.FindObject<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
                //Validation
                if (_selRig == null || editModel.ArchiveNo == null || editModel.Holder == null ||
                    editModel.Responsible == null || editModel.Approver == null ||
                    string.IsNullOrEmpty(editModel.PermitType) ||
                    string.IsNullOrEmpty(editModel.Location) || string.IsNullOrEmpty(editModel.Description))
                {
                    Snackbar.Add("لطفاً موارد الزامی را تکمیل کنید.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                if (editModel.EndTime != null && editModel.EndTime < editModel.StartTime)
                {
                    Snackbar.Add("تاریخ و ساعت پایان کار نباید قبل از شروع آن باشد.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                if (_startTime != null) editModel.StartTime = editModel.StartTime.Add(_startTime.Value);
                if (_endTime != null) editModel.EndTime = editModel.EndTime?.Add(_endTime.Value);

                var wellWork = Session1.Query<WellWork>().FirstOrDefault(x => x.RigNo.Oid == _selRig.Oid &&
                    x.IsActive);

                if (wellWork == null)
                {
                    Snackbar.Add("دکل انتخاب شده در هیچ پروژه‌ای فعال نیست.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                //Check date
                if (editModel.StartTime < wellWork.StartDate)
                {
                    Snackbar.Add("تاریخ و ساعت انتخاب شده با تاریخ شروع پروژه مطابقت ندارد.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                //Check serial number
                var prevItem =
                    Session1.FindObject<Permit>(
                        new BinaryOperator(nameof(Permit.ArchiveNo), editModel.ArchiveNo));

                if (prevItem != null && prevItem.Oid != editModel.Oid)
                {
                    Snackbar.Add("شماره سریال مجوز تکراری است. لطفاً آن را بررسی کرده و دوباره تلاش کنید.",
                        Severity.Error);
                    e.Cancel = true;
                    return;
                }

                editModel.WorkID = wellWork;
                editModel.Agent ??= loggedUser;
                editModel.Save();
                break;
            case Action.Delete:
                e.RowData.Delete();
                break;
        }
    }

    private async Task PermitToolbarClickHandler(Syncfusion.Blazor.Navigations.ClickEventArgs args)
    {
        if (args.Item.Id == "permitGrid_Excel Export")
        {
            Snackbar.Add("سیستم در حال ایجاد فایل است. لطفاً تا دانلود گزارش شکیبا باشید...", Severity.Info);
            var exportProperties = new ExcelExportProperties
            {
                FileName = "UserList.xlsx"
            };
            await PermitGrid!.ExportToExcelAsync(exportProperties);
        }
    }
}