using DevExpress.DashboardWeb;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Samco_HSE_Manager.Pages.Shared;

public partial class DashDesign
{
    [Inject] private DashboardConfigurator DashboardConfigurator { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private IWebHostEnvironment HostingEnvironment { get; set; } = null!;

    private IEnumerable<string> Dashboards { get; set; } = null!;
    private string? _selDash;
    protected override void OnInitialized()
    {
        Dashboards = DashboardConfigurator.DashboardStorage.GetAvailableDashboardsInfo().Select(dashboardInfo => dashboardInfo.ID).ToList();
    }

    private async Task RemoveDashboard()
    {
        if (string.IsNullOrEmpty(_selDash))
        {
            Snackbar.Add("لطفاً یک داشبورد را انتخاب کنید.", Severity.Error);
            return;
        }

        if (await DialogService.ShowMessageBox("حذف داشبورد",
                $"آیا از حذف داشبورد {_selDash} مطمئنید؟ این کار غیر قابل بازگشت است.", yesText: "حذف", cancelText: "انصراف") == true)
        {
            try
            {
                File.Delete(Path.Combine(HostingEnvironment.ContentRootPath, "Data", "Dashboards", $"{_selDash}.xml"));
            }
            catch (Exception e)
            {
                Snackbar.Add("خطا در حذف داشبورد" + Environment.NewLine + e.Message, Severity.Error);
                return;
            }
            Dashboards = DashboardConfigurator.DashboardStorage.GetAvailableDashboardsInfo().Select(dashboardInfo => dashboardInfo.ID).ToList();
            _selDash = null;
            StateHasChanged();
        }
    }
}