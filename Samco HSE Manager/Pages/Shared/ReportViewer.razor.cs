using Microsoft.AspNetCore.Components;

namespace Samco_HSE_Manager.Pages.Shared;

public partial class ReportViewer
{
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;

    [Parameter]
    [SupplyParameterFromQuery]
    public string? ReportName { get; set; }
    [Parameter]
    [SupplyParameterFromQuery]

    public int RigId { get; set; }

    [Parameter]
    [SupplyParameterFromQuery]
    public string? Parameters { get; set; }

    protected override void OnInitialized()
    {
        Stimulsoft.Base.StiLicense.LoadFromFile(Path.Combine(HostEnvironment.WebRootPath, "content","stimulsoft", "License.key"));

        if (Parameters != null)
        {
            //Load parameters
            ReportName = ReportName + "?" + Parameters.Replace("--", "=").Replace("|", "&");
        }
        else
        {
            ReportName = ReportName + "?" + "RigNo=" + RigId;
        }

        //ReportName = "MedicineRequest?RigNo=1&Title=شرکت پترو ایران - دکل DCI 2";
        base.OnInitialized();
    }
}