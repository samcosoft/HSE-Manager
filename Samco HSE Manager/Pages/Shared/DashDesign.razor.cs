using DevExpress.DashboardWeb;
using Microsoft.AspNetCore.Components;

namespace Samco_HSE_Manager.Pages.Shared
{
    public partial class DashDesign
    {
        [Inject] private DashboardConfigurator DashboardConfigurator { get; set; }
        private IEnumerable<string> Dashboards { get; set; }
        private string? _selDash;
        protected override async Task OnInitializedAsync()
        {
            Dashboards = DashboardConfigurator.DashboardStorage.GetAvailableDashboardsInfo().Select(dashboardInfo => dashboardInfo.ID).ToList();
        }
    }
}
