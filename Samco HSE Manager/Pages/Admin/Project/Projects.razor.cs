using DevExpress.Blazor;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using DevExpress.Data.Filtering;
using MudBlazor;

namespace Samco_HSE_Manager.Pages.Admin.Project;

public partial class Projects : IDisposable
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    private Session Session1 { get; set; } = null!;
    private IEnumerable<Samco_HSE.HSEData.Project>? DrillProjects { get; set; }
    private Samco_HSE.HSEData.Project? _selProject;

    private IEnumerable<Well>? Wells { get; set; }
    private Well? _selWell;
    private IEnumerable<Rig>? Rigs { get; set; }
    private Rig? _selRig;

    private IEnumerable<WellWork>? WellWorks { get; set; }
    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
        await LoadInformation();
    }

    private async Task LoadInformation()
    {
        _selProject = new Samco_HSE.HSEData.Project(Session1);
        DrillProjects = await Session1.Query<Samco_HSE.HSEData.Project>().ToListAsync();
        Rigs = await Session1.Query<Rig>().ToListAsync();
        Wells = await Session1.Query<Well>().ToListAsync();
        WellWorks = await Session1.Query<WellWork>().ToListAsync();
    }
    #region PopupControlls

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
                if (_selProject == null)
                {
                    Snackbar.Add("لطفاً ابتدا یک پروژه را انتخاب کنید.", Severity.Warning);
                    return;
                }
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
                if (_selProject == null)
                {
                    Snackbar.Add("لطفاً ابتدا یک پروژه را انتخاب کنید.", Severity.Warning);
                    return;
                }
                break;
            case EditController.EditType.Rig:
                if (_selRig == null)
                {
                    Snackbar.Add("لطفاً ابتدا یک دکل را انتخاب کنید.", Severity.Warning);
                    return;
                }
                break;
            case EditController.EditType.Well:
                if (_selWell == null)
                {
                    Snackbar.Add("لطفاً ابتدا یک چاه را انتخاب کنید.", Severity.Warning);
                    return;
                }
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
                dialog = await DialogService.ShowAsync<ProjectModal>("اطلاعات پروژه", new DialogParameters { { "SelProject", _selProject } });
                break;
            case EditController.EditType.Rig:
                dialog = await DialogService.ShowAsync<RigModal>("اطلاعات دکل / دفتر", new DialogParameters { { "SelRig", _selRig } });
                break;
            case EditController.EditType.Well:
                dialog = await DialogService.ShowAsync<WellModal>("اطلاعات چاه", new DialogParameters { { "SelWell", _selWell } });
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
                    _selProject?.Delete();
                    _selProject = new Samco_HSE.HSEData.Project(Session1);
                    DrillProjects = await Session1.Query<Samco_HSE.HSEData.Project>().ToListAsync();
                    break;
                case EditController.EditType.Rig:
                    _selRig?.Delete();
                    _selRig = new Rig(Session1);
                    Rigs = await Session1.Query<Rig>().ToListAsync();
                    break;
                case EditController.EditType.Well:
                    _selWell?.Delete();
                    _selWell = new Well(Session1);
                    Wells = await Session1.Query<Well>().ToListAsync();
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

        StateHasChanged();
    }

    #endregion

    #region ProjectGrid
    private void ProjectsGridUnbound(GridUnboundColumnDataEventArgs e)
    {
        using var tempSession = new Session(DataLayer);
        var currentWork = tempSession.FindObject<WellWork>(new BinaryOperator("Oid", ((WellWork)e.DataItem).Oid));
        //if (currentWork == null) { return; }

        e.Value = e.FieldName switch
        {
            //"EquipmentsCount" => currentWork.MaterialRequests.SelectMany(x=>x.MaterialLists).Sum(x=>x.Counts),
            "WarningsCount" => currentWork.Warnings.Count,
            "AccidentsCount" => currentWork.AccidentReports.Count,
            "PracticeCount" => currentWork.Practices.Count,
            "TrainingCount" => currentWork.Trainings.Count,
            _ => e.Value
        };
    }

    private void ProjectEditModel(GridCustomizeEditModelEventArgs e)
    {
        var dataItem = (WellWork?)e.DataItem;
        e.EditModel = dataItem ?? new WellWork(Session1);
    }

    private async Task OnEditModelSaving(GridEditModelSavingEventArgs e)
    {
        var editModel = (WellWork)e.EditModel;
        //Validation
        if (editModel.RigNo == null || editModel.WellNo == null)
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
        WellWorks = await Session1.Query<WellWork>().ToListAsync();
    }

    private async Task OnDataItemDeleting(GridDataItemDeletingEventArgs e)
    {
        var dataItem =
            await Session1.FindObjectAsync<WellWork>(new BinaryOperator("Oid", ((e.DataItem as WellWork)!).Oid));
        if (dataItem != null)
        {
            dataItem.Delete();
            WellWorks = await Session1.Query<WellWork>().ToListAsync();
        }
    }


    #endregion

    public void Dispose()
    {
        Session1.Dispose();
    }
}