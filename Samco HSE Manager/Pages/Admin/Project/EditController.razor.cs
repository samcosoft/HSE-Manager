using Microsoft.AspNetCore.Components;
using Syncfusion.Blazor.Popups;

namespace Samco_HSE_Manager.Pages.Admin.Project;

public partial class EditController
{
    [Inject] private SfDialogService DialogService { get; set; } = null!;

    public enum EditType
    {
        Project,
        Rig,
        Well,
        WellWork
    }

    [Parameter] public EditType EditorType { get; set; }
    [Parameter] public EventCallback<EditType> OnNewButtonClicked { get; set; }
    [Parameter] public EventCallback<EditType> OnEditButtonClicked { get; set; }
    [Parameter] public EventCallback<EditType> OnDelButtonClicked { get; set; }

    private async void OpenNewClick()
    {
        await OnNewButtonClicked.InvokeAsync(EditorType);
    }

    private async void EditClick()
    {
        await OnEditButtonClicked.InvokeAsync(EditorType);
    }

    private async Task DeleteClick()
    {
        if (await DialogService.ConfirmAsync("آیا از حذف این مورد مطمئنید؟", "حذف مورد"))
            await OnDelButtonClicked.InvokeAsync(EditorType);
    }
}