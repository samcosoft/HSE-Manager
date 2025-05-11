using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Samco_HSE.HSEData;
using Samco_HSE_Manager.Pages.Shared;

namespace Samco_HSE_Manager.Pages.Admin.FormModals
{
    public partial class FormFileModal : IDisposable
    {

        [Inject] private IDataLayer DataLayer { get; set; } = null!;
        [Inject] private ISnackbar Snackbar { get; set; } = null!;

        [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

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
            var selForm = Session1.GetObjectByKey<HSEForm>(SelFormId);
            //update file database
            selForm.FormType = _uploader?.FileList.Count > 0 ? _uploader?.FileList.First().Key.Split('.').Last() : null;
            selForm.Save();
            MudDialog.Close(DialogResult.Ok(true));
        }

        private void Cancel() => MudDialog.Cancel();

        public void Dispose()
        {
            Session1.Dispose();
        }
    }
}
