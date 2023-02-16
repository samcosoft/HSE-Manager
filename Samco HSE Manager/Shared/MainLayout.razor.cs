using BootstrapBlazor.Components;
using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Samco_HSE.HSEData;
using Samco_HSE_Manager.Authentication;
using System.Diagnostics.CodeAnalysis;

namespace Samco_HSE_Manager.Shared
{
    public partial class MainLayout
    {
        [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;
        [Inject] private NavigationManager NavMan { get; set; } = null!;
        [Inject] private IDataLayer DataLayer { get; set; } = null!;

        [CascadingParameter]
        [NotNull]
        private BootstrapBlazorRoot? Root { get; set; }

        protected override async Task OnInitializedAsync()
        {
            var session1 = new Session(DataLayer);
            //SamcoSoftShared.CurrentUser = new User(session1);
            try
            {
                var authStat = await AuthenticationStateProvider.GetAuthenticationStateAsync();
                if (authStat.User.Identity == null) return;
                SamcoSoftShared.CurrentUser = await session1.FindObjectAsync<User>(new BinaryOperator("Username",
                   authStat.User.Identity!.Name));
                if (SamcoSoftShared.CurrentUser != null)
                {
                    SamcoSoftShared.CurrentUserRole =
                        Enum.Parse<SamcoSoftShared.SiteRoles>(SamcoSoftShared.CurrentUser.SiteRole);
                    SamcoSoftShared.CurrentUserId = SamcoSoftShared.CurrentUser.Oid;
                }
            }
            catch (Exception)
            {
                //_loggedUser = new User(session1);
            }

            var toastContainer = Root.ToastContainer;
            toastContainer.SetPlacement(Placement.MiddleCenter);
        }

        private async void LogOut_Click()
        {
            await ((CustomAuthenticationStateProvider)AuthenticationStateProvider).MarkUserAsLoggedOut();
            NavMan.NavigateTo("");
        }

        private void LogIn_Click()
        {
            NavMan.NavigateTo("/login");
        }
    }
}
