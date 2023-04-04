using DevExpress.DashboardWeb;
using Microsoft.AspNetCore.Components;

namespace Samco_HSE_Manager.Pages.Shared;

public partial class DashDesign
{
    [Inject] private DashboardConfigurator DashboardConfigurator { get; set; } = null!;
    private IEnumerable<string> Dashboards { get; set; } = null!;
    private string? _selDash;
    protected override void OnInitialized()
    {
        Dashboards = DashboardConfigurator.DashboardStorage.GetAvailableDashboardsInfo().Select(dashboardInfo => dashboardInfo.ID).ToList();
    }
}