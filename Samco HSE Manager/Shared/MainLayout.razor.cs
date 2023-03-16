using BootstrapBlazor.Components;
using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Samco_HSE.HSEData;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Samco_HSE_Manager.Shared
{
    public partial class MainLayout : IDisposable
    {
        [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;
        [Inject] private NavigationManager NavigationManager { get; set; } = null!;
        [Inject] private IDataLayer DataLayer { get; set; } = null!;

        [CascadingParameter]
        [NotNull]
        private BootstrapBlazorRoot? Root { get; set; }


        string? NavMenuCssClass { get; set; }
        bool _isMobileLayout;
        bool IsMobileLayout
        {
            get => _isMobileLayout;
            set
            {
                _isMobileLayout = value;
                IsSidebarExpanded = !_isMobileLayout;
            }
        }

        bool _isSidebarExpanded = true;
        bool IsSidebarExpanded
        {
            get => _isSidebarExpanded;
            set
            {
                if (_isSidebarExpanded != value)
                {
                    NavMenuCssClass = value ? "expand" : "collapse";
                    _isSidebarExpanded = value;
                }
            }
        }


        protected override async Task OnInitializedAsync()
        {
            var session1 = new Session(DataLayer);
            //SamcoSoftShared.CurrentUser = new User(session1);
            try
            {
                var authStat = await AuthenticationStateProvider.GetAuthenticationStateAsync();
                if (authStat.User.Identity == null) return;

                var loggedUser = await session1.FindObjectAsync<User>(new BinaryOperator("Username",
                    authStat.User.Identity?.Name));
                if (loggedUser != null)
                {
                    SamcoSoftShared.CurrentUser = loggedUser;
                    SamcoSoftShared.CurrentUserId = loggedUser.Oid;
                    SamcoSoftShared.CurrentUserRole = Enum.Parse<SamcoSoftShared.SiteRoles>(SamcoSoftShared.CurrentUser.SiteRole);
                }
                else
                {
                    return;
                }
            }
            catch (Exception)
            {
                //_loggedUser = new User(session1);
            }

            var toastContainer = Root.ToastContainer;
            toastContainer.SetPlacement(Placement.MiddleCenter);
            NavigationManager.LocationChanged += OnLocationChanged;
        }

        private string? GetApplicationVersion()
        {
            return Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                  ?.InformationalVersion;
        }

        async void OnLocationChanged(object? sender, LocationChangedEventArgs args)
        {
            if (IsMobileLayout)
            {
                IsSidebarExpanded = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        public void Dispose()
        {
            NavigationManager.LocationChanged -= OnLocationChanged;
        }
    }
}
