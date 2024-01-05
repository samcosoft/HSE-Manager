using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;

namespace Samco_HSE_Manager.Pages.Personnel
{
    public partial class PersonnelInfo
    {
        [Inject] private IDataLayer DataLayer { get; set; } = null!;

        [Parameter]
        public int PersonId { get; set; }

        public Samco_HSE.HSEData.Personnel? SelPerson { get; set; }
        private Session Session1 { get; set; } = null!;

        protected override async Task OnInitializedAsync()
        {
            Session1 = new Session(DataLayer);
            SelPerson = await Session1.GetObjectByKeyAsync<Samco_HSE.HSEData.Personnel>(PersonId);
        }
    }
}
