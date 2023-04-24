using DevExpress.Blazor.Reporting;
using Microsoft.AspNetCore.Components;

namespace Samco_HSE_Manager.Pages.Shared;

public partial class ReportViewer
{
    [Parameter]
    [SupplyParameterFromQuery]
    public string? ReportName { get; set; }
    [Parameter]
    [SupplyParameterFromQuery]

    public int RigId { get; set; }

    [Parameter]
    [SupplyParameterFromQuery]
    public string? Parameters { get; set; }
    private DxDocumentViewer? _dxDocumentViewer;

    protected override void OnInitialized()
    {
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
    }
}