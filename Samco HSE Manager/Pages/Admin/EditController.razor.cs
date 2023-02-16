using BootstrapBlazor.Components;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;

namespace Samco_HSE_Manager.Pages.Admin
{
    public partial class EditController
    {
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
            await OnDelButtonClicked.InvokeAsync(EditorType);
        }
    }
}
