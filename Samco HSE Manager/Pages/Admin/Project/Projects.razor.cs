using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Samco_HSE_Manager.Models;
using Samco_HSE.HSEData;
using Syncfusion.Blazor.DropDowns;
using Syncfusion.Blazor.Grids;
using Action = Syncfusion.Blazor.Grids.Action;

namespace Samco_HSE_Manager.Pages.Admin.Project;

public partial class Projects : IDisposable
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private Syncfusion.Blazor.Popups.SfDialogService ConfirmDialog { get; set; } = null!;
    private Session Session1 { get; set; } = null!;
    private IEnumerable<ListProject>? DrillProjects { get; set; }
    private Samco_HSE.HSEData.Project? _selProject;
    private int[]? _selProjectOid;

    private IEnumerable<ListWell>? Wells { get; set; }
    private Well? _selWell;
    private int[]? _selWellOid;

    private IEnumerable<Rig>? Rigs { get; set; }
    private IEnumerable<SamcoRigSelector.ComboRig>? ListRigs { get; set; }
    private Rig? _selRig;
    private int[]? _selRigOid;
    private IEnumerable<WellWork>? WellWorks { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
        await LoadInformation();
    }

    private async Task LoadInformation()
    {
        DrillProjects = await Session1.Query<Samco_HSE.HSEData.Project>().Select(x => new ListProject(x)).ToListAsync();
        Rigs = await Session1.Query<Rig>().ToListAsync();
        ListRigs = Rigs.Select(x => new SamcoRigSelector.ComboRig(x));
        WellWorks = await Session1.Query<WellWork>().ToListAsync();
        _selRigOid = null;
        _selWellOid = null;
    }

    #region PopupControlls

    private void OnProjectChange(ListBoxChangeEventArgs<int[], ListProject> obj)
    {
        if (!obj.Items.Any()) return;
        Wells = Session1.GetObjectByKey<Samco_HSE.HSEData.Project>(obj.Value.First()).Wells
            .Select(x => new ListWell(x));
        _selWellOid = null;
    }

    private async Task NewBtnClick(EditController.EditType editType)
    {
        switch (editType)
        {
            case EditController.EditType.Project:
                _selProject = new Samco_HSE.HSEData.Project(Session1);
                break;
            case EditController.EditType.Rig:
                _selRig = new Rig(Session1);
                break;
            case EditController.EditType.Well:
                if (_selProjectOid == null)
                {
                    Snackbar.Add("لطفاً ابتدا یک پروژه را انتخاب کنید.", Severity.Warning);
                    return;
                }

                _selProject =
                    await Session1.GetObjectByKeyAsync<Samco_HSE.HSEData.Project>(_selProjectOid!.First(), true);
                _selWell = new Well(Session1) { ProjectName = _selProject };
                break;
            case EditController.EditType.WellWork:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(editType), editType, null);
        }

        await OpenModal(editType);
    }

    private async Task EditBtnClick(EditController.EditType editType)
    {
        switch (editType)
        {
            case EditController.EditType.Project:
                if (_selProjectOid == null || !_selProjectOid.Any())
                {
                    Snackbar.Add("لطفاً ابتدا یک پروژه را انتخاب کنید.", Severity.Warning);
                    return;
                }

                _selProject =
                    await Session1.GetObjectByKeyAsync<Samco_HSE.HSEData.Project>(_selProjectOid.First(), true);
                break;
            case EditController.EditType.Rig:
                if (_selRigOid == null || !_selRigOid.Any())
                {
                    Snackbar.Add("لطفاً ابتدا یک دکل را انتخاب کنید.", Severity.Warning);
                    return;
                }

                _selRig = await Session1.GetObjectByKeyAsync<Rig>(_selRigOid.First(), true);
                break;
            case EditController.EditType.Well:
                if (_selWellOid == null || !_selWellOid.Any())
                {
                    Snackbar.Add("لطفاً ابتدا یک چاه را انتخاب کنید.", Severity.Warning);
                    return;
                }

                _selWell = await Session1.GetObjectByKeyAsync<Well>(_selWellOid.First(), true);
                break;
            case EditController.EditType.WellWork:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(editType), editType, null);
        }

        await OpenModal(editType);
    }

    private async Task OpenModal(EditController.EditType editType)
    {
        IDialogReference dialog = null!;
        switch (editType)
        {
            case EditController.EditType.Project:
                dialog = await DialogService.ShowAsync<ProjectModal>("اطلاعات پروژه",
                    new DialogParameters { { "SelProject", _selProject } });
                break;
            case EditController.EditType.Rig:
                dialog = await DialogService.ShowAsync<RigModal>("اطلاعات دکل / دفتر",
                    new DialogParameters { { "SelRig", _selRig } });
                break;
            case EditController.EditType.Well:
                dialog = await DialogService.ShowAsync<WellModal>("اطلاعات چاه",
                    new DialogParameters { { "SelWell", _selWell } });
                break;
            case EditController.EditType.WellWork:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(editType), editType, null);
        }

        var result = await dialog.Result;
        if (!result.Canceled)
        {
            await LoadInformation();
            StateHasChanged();
        }
    }

    private async Task DelBtnClick(EditController.EditType editType)
    {
        try
        {
            switch (editType)
            {
                case EditController.EditType.Project:
                    if (_selProjectOid == null || _selProjectOid.Any() == false)
                    {
                        Snackbar.Add("لطفاً ابتدا یک پروژه را انتخاب کنید.", Severity.Warning);
                        return;
                    }

                    _selProject =
                        await Session1.GetObjectByKeyAsync<Samco_HSE.HSEData.Project>(_selProjectOid.First(), true);
                    _selProject?.Delete();
                    break;
                case EditController.EditType.Rig:
                    if (_selRigOid == null || !_selRigOid.Any())
                    {
                        Snackbar.Add("لطفاً ابتدا یک دکل را انتخاب کنید.", Severity.Warning);
                        return;
                    }

                    _selRig = await Session1.GetObjectByKeyAsync<Rig>(_selRigOid.First(), true);
                    _selRig?.Delete();
                    break;
                case EditController.EditType.Well:
                    if (_selWellOid == null || !_selWellOid.Any())
                    {
                        Snackbar.Add("لطفاً ابتدا یک چاه را انتخاب کنید.", Severity.Warning);
                        return;
                    }

                    _selWell = await Session1.GetObjectByKeyAsync<Well>(_selWellOid.First(), true);
                    _selWell?.Delete();
                    break;
                case EditController.EditType.WellWork:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(editType), editType, null);
            }
        }
        catch (Exception)
        {
            //Ignored
        }

        await LoadInformation();
        StateHasChanged();
    }

    #endregion

    #region ProjectGrid

    private IEnumerable<ListWell>? _selProjectWllList;
    private ListWell? _selProjectWell;
    private SfGrid<WellWork>? WorkGrid { get; set; }

    private async Task WorkGrid_Action(ActionEventArgs<WellWork> e)
    {
        _selProjectWllList = Session1.Query<Well>().Select(x => new ListWell(x)).ToList();
        switch (e.RequestType)
        {
            case Action.Add:
                e.Data ??= new WellWork(Session1);
                break;
            case Action.BeginEdit:
                e.Data = await Session1.GetObjectByKeyAsync<WellWork>(e.RowData.Oid, true);
                _selProjectWell = ListWell.GetListWell(_selProjectWllList, e.Data.WellNo);
                break;
            case Action.Save:
            {
                var editModel = e.Data;
                //Validation
                if (editModel.RigNo == null || _selProjectWell == null)
                {
                    Snackbar.Add("انتخاب چاه، دکل و تاریخ شروع عملیات لازم است.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                if (editModel.EndDate != null && editModel.EndDate < editModel.StartDate)
                {
                    Snackbar.Add("تاریخ پایان عملیات باید از شروع آن بزرگتر باشد.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                var otherRigIsActive = Session1.Query<WellWork>().Where(x => x.WellNo.Oid == editModel.WellNo.Oid &&
                                                                             x.RigNo.Oid != editModel.RigNo.Oid &&
                                                                             x.IsActive).ToList();
                if (otherRigIsActive.Any())
                {
                    Snackbar.Add($"در حال حاضر دکل {otherRigIsActive.First().RigNo.Name} بر روی این چاه و پروژه در حال فعالیت است.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                editModel.WellNo = await Session1.GetObjectByKeyAsync<Well>(_selProjectWell.Oid, true);
                if (editModel.IsActive)
                {
                    var prevWork = await Session1.Query<WellWork>()
                        .FirstOrDefaultAsync(x => x.WellNo.Oid == editModel.WellNo.Oid && x.IsActive);
                    if (prevWork != null)
                    {
                        prevWork.IsActive = false;
                        prevWork.Save();
                    }
                }

                editModel.Save();
                break;
            }
            case Action.Delete:
            {
                if (!await ConfirmDialog.ConfirmAsync(
                        "اخطار! با حذف پروژه کاری تمامی اطلاعات زیر مجموعه آن شامل گزارشات مرتبط نیز حذف خواهند شد. آیا مطمئنید؟",
                        "اخطار حذف عملیات")) return;
                var dataItem = e.RowData;
                dataItem.Delete();
                break;
            }
        }
    }

    private async Task WorkToolbarClickHandler(Syncfusion.Blazor.Navigations.ClickEventArgs args)
    {
        if (args.Item.Id == "workGrid_Excel Export")
        {
            Snackbar.Add("سیستم در حال ایجاد فایل است. لطفاً تا دانلود گزارش شکیبا باشید...", Severity.Info);
            var exportProperties = new ExcelExportProperties
            {
                FileName = "WorkList.xlsx"
            };
            await WorkGrid!.ExportToExcelAsync(exportProperties);
        }
    }

    #endregion

    private class ListProject
    {
        public int Oid { get; set; }
        public string? Name { get; set; }

        public ListProject(Samco_HSE.HSEData.Project project)
        {
            Oid = project.Oid;
            Name = project.Name;
        }
    }

    private class ListWell
    {
        public int Oid { get; set; }
        public string? Name { get; set; }

        public string? ProjectName { get; set; }
        public string? Location { get; set; }

        public ListWell(Well well)
        {
            Oid = well.Oid;
            Name = well.Name;
            ProjectName = well.ProjectName?.Name;
            Location = well.Location;
        }

        public static ListWell? GetListWell(IEnumerable<ListWell>? rigList, Well selRig)
        {
            return rigList?.FirstOrDefault(x => x.Oid == selRig.Oid);
        }
    }

    public void Dispose()
    {
        Session1.Dispose();
    }
}