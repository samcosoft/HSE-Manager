using Microsoft.AspNetCore.Components;
using Stimulsoft.Report;

namespace Samco_HSE_Manager.Pages.Admin;

public partial class ReportDesign
{
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;

    private List<string>? _reportList;

    private StiReport? _report;

    protected override void OnInitialized()
    {
        //Stimulsoft.Base.StiLicense.LoadFromFile(Path.Combine(HostEnvironment.WebRootPath, "stimulsoft", "license.key"));

        _reportList =
            Directory.GetFiles(Path.Combine(HostEnvironment.ContentRootPath, "Data", "Reports")).Select(x => x.Split("\\").Last()).ToList();

        base.OnInitialized();
    }

    private void OpenReport(string reportName)
    {
        var report = new StiReport();

        _report = report.Load(Path.Combine(HostEnvironment.ContentRootPath, "Data", "Reports", reportName));
    }
}