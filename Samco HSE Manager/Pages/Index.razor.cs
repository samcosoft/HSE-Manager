using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;

namespace Samco_HSE_Manager.Pages
{
    public partial class Index
    {
        [CascadingParameter]
        private Task<AuthenticationState> AuthenticationStateTask { get; set; } = null!;
        [Inject]private IDataLayer DataLayer { get; set; } = null!;

        private Session? _session1;
        private User? _loggedUser;

        protected override async Task OnInitializedAsync()
        {
            _session1 = new Session(DataLayer);

            try
            {
                _loggedUser = await _session1.FindObjectAsync<User>(new BinaryOperator("Username",
                    (await AuthenticationStateTask).User.Identity?.Name));
            }
            catch (Exception)
            {
                //Ignore
            }
        }
    }
}
