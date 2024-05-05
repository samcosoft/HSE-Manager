using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Samco_HSE.HSEData;
using Samco_HSE_Manager.Pages.Shared;

namespace Samco_HSE_Manager.Pages.Admin.FormModals
{
    public partial class FormFileModal:IDisposable
    {
        
        [Inject] private IDataLayer DataLayer { get; set; } = null!;
        [Inject] private ISnackbar Snackbar { get; set; } = null!;

        [CascadingParameter] private MudDialogInstance MudDialog { get; set; } = null!;

        [Parameter] public int SelFormId { get; set; }
        private Session Session1 { get; set; } = null!;

        private FileUploader? _uploader;

        protected override void OnInitialized()
        {
            Session1 = new Session(DataLayer);
            base.OnInitialized();
        }

        private void Submit()
        {
            if (!(_uploader?.FileList.Count > 0)) return;
            //update file database
            var selForm = Session1.GetObjectByKey<HSEForm>(SelFormId);
            selForm.FormType = _uploader.FileList.First().Type;
            selForm.Save();
        }

        private void Cancel() => MudDialog.Cancel();

        public void Dispose()
        {
            Session1.Dispose();
        }
    }
}
