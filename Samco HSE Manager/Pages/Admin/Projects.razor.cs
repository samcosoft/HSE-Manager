using BootstrapBlazor.Components;
using DevExpress.Blazor;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using System.Diagnostics.CodeAnalysis;
using DevExpress.Data.Filtering;
using static Samco_HSE_Manager.Pages.Admin.EditController;

namespace Samco_HSE_Manager.Pages.Admin;

public partial class Projects : IDisposable
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;

    [Inject]
    [NotNull]
    private ToastService? ToastService { get; set; }

    private Session Session1 { get; set; } = null!;
    private IEnumerable<Project>? DrillProjects { get; set; }
    private Project _selProject = null!;

    private IEnumerable<Well>? Wells { get; set; }
    private Well _selWell = null!;
    private IEnumerable<Rig>? Rigs { get; set; }
    private Rig _selRig = null!;

    private IEnumerable<WellWork>? WellWorks { get; set; }
    public Modal? ProjectModal { get; set; }
    private Modal? RigModal { get; set; }
    private Modal? WellModal { get; set; }

    protected override async Task OnInitializedAsync()
    {

        Session1 = new Session(DataLayer);
        _selProject = new Project(Session1);
        _selWell = new Well(Session1);
        _selRig = new Rig(Session1);

        DrillProjects = await Session1.Query<Project>().ToListAsync();
        Rigs = await Session1.Query<Rig>().ToListAsync();
        Wells = await Session1.Query<Well>().ToListAsync();
        WellWorks = await Session1.Query<WellWork>().ToListAsync();
    }

    #region PopupControlls

    private async Task NewBtnClick(EditType editType)
    {
        switch (editType)
        {
            case EditType.Project:
                _selProject = new Project(Session1);
                break;
            case EditType.Rig:
                _selRig = new Rig(Session1);
                break;
            case EditType.Well:
                if (string.IsNullOrEmpty(_selProject.Name))
                {
                    await ToastService.Warning("انتخاب پروژه", "لطفاً ابتدا یک پروژه را انتخاب کنید.",
                        autoHide: true);
                    //"انتخاب پروژه", "لطفاً ابتدا یک پروژه را انتخاب کنید.", autoHide: true
                    return;
                }
                _selWell = new Well(Session1);
                break;
            case EditType.WellWork:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(editType), editType, null);
        }

        await OpenModal(editType);
    }

    private async Task EditBtnClick(EditType editType)
    {
        switch (editType)
        {
            case EditType.Project:
                if (string.IsNullOrEmpty(_selProject.Name))
                {
                    await ToastService.Warning("انتخاب پروژه", "لطفاً ابتدا یک پروژه را انتخاب کنید.",
                        autoHide: true);
                    return;
                }
                break;
            case EditType.Rig:
                if (string.IsNullOrEmpty(_selRig.Name))
                {
                    await ToastService.Warning("انتخاب دکل", "لطفاً ابتدا یک دکل را انتخاب کنید.",
                        autoHide: true);
                    return;
                }
                break;
            case EditType.Well:
                if (string.IsNullOrEmpty(_selWell.Name))
                {
                    await ToastService.Warning("انتخاب چاه", "لطفاً ابتدا یک چاه را انتخاب کنید.",
                        autoHide: true);
                    return;
                }
                break;
            case EditType.WellWork:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(editType), editType, null);
        }

        await OpenModal(editType);
    }

    private async Task OpenModal(EditType editType)
    {
        switch (editType)
        {
            case EditType.Project:
                await ProjectModal!.Show();
                break;
            case EditType.Rig:
                await RigModal!.Show();
                break;
            case EditType.Well:
                await WellModal!.Show();
                break;
            case EditType.WellWork:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(editType), editType, null);
        }
    }

    private async Task DelBtnClick(EditType editType)
    {
        try
        {
            switch (editType)
            {
                case EditType.Project:
                    _selProject.Delete();
                    _selProject = new Project(Session1);
                    DrillProjects = await Session1.Query<Project>().ToListAsync();
                    break;
                case EditType.Rig:
                    _selRig.Delete();
                    _selRig = new Rig(Session1);
                    Rigs = await Session1.Query<Rig>().ToListAsync();
                    break;
                case EditType.Well:
                    _selWell.Delete();
                    _selWell = new Well(Session1);
                    Wells = await Session1.Query<Well>().ToListAsync();
                    break;
                case EditType.WellWork:
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
    private async void ProjOkBtnClick()
    {
        if (_selProject.Name == null)
        {
            await ToastService.Error("خطا در افزودن پروژه", "نام پروژه الزامی است.");
            return;
        }
        if (_selProject.EndDate != null && _selProject.EndDate < _selProject.StartDate)
        {
            await ToastService.Error("خطا در افزودن پروژه", "تاریخ پایان پروژه باید از شروع آن بزرگتر باشد.");
            return;
        }
        _selProject.Save();
        await ProjectModal!.Close();
        DrillProjects = await Session1.Query<Project>().ToListAsync();
        StateHasChanged();
    }
    private async void WellOkBtnClick()
    {
        if (_selWell.Name == null)
        {
            await ToastService.Error("خطا در افزودن چاه", "نام چاه الزامی است.");
            return;
        }
        _selWell.ProjectName = _selProject;
        _selWell.Save();
        await WellModal!.Close();
        DrillProjects = await Session1.Query<Project>().ToListAsync();
        Wells = await Session1.Query<Well>().ToListAsync();
        StateHasChanged();
    }
    private async Task RigOkBtnClick()
    {
        if (_selRig.Name == null)
        {
            await ToastService.Error("خطا در افزودن دکل", "نام دکل الزامی است.");
            return;
        }
        _selRig.Save();
        await RigModal!.Close();
        Rigs = await Session1.Query<Rig>().ToListAsync();
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
            await ToastService.Error("خطا در افزودن عملیات", "انتخاب چاه، دکل و تاریخ شروع پروژه لازم است.");
            e.Cancel = true;
            return;
        }
        if (editModel.EndDate != null && editModel.EndDate < editModel.StartDate)
        {
            await ToastService.Error("خطا در افزودن عملیات", "تاریخ پایان عملیات باید از شروع آن بزرگتر باشد.");
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