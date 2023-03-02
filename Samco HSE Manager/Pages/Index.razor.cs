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
        [Inject] private IDataLayer DataLayer { get; set; } = null!;

        private Session? _session1;
        private User? _loggedUser;
        private IEnumerable<Rig>? _locationList;
        private IEnumerable<Rig>? _selLocation;
        private DateTime _fromDate, _toDate;

        protected override async Task OnInitializedAsync()
        {
            _session1 = new Session(DataLayer);

            var claimsPrincipal = (await AuthenticationStateTask).User;

            if (claimsPrincipal.Identity is { IsAuthenticated: true } && claimsPrincipal.IsInRole("Personnel"))
            {
                NavigationManager.NavigateTo("/personnelHome");
                return;
            }

            //try
            //{
            //    _loggedUser = await _session1.FindObjectAsync<User>(new BinaryOperator("Username",
            //        (await AuthenticationStateTask).User.Identity?.Name));
            //    if (_loggedUser != null)
            //    {
            //        SamcoSoftShared.CurrentUser = _loggedUser;
            //        SamcoSoftShared.CurrentUserId = _loggedUser.Oid;
            //        SamcoSoftShared.CurrentUserRole = Enum.Parse<SamcoSoftShared.SiteRoles>(SamcoSoftShared.CurrentUser.SiteRole);
            //    }
            //    else
            //    {
            //        return;
            //    }
            //}
            //catch (Exception)
            //{
            //    return;
            //}

            _locationList = await _session1.Query<Rig>().ToListAsync();
            _fromDate = DateTime.Parse("2023/01/01");
            _toDate = DateTime.Now;
        }

        #region Dashboard

        private void LoadStatistics(IEnumerable<string> obj)
        {

        }

        #endregion
    }
}
