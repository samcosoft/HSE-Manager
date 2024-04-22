using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using MudBlazor;
using DevExpress.Data.Filtering;
using Syncfusion.Blazor.Grids;
using Action = Syncfusion.Blazor.Grids.Action;

namespace Samco_HSE_Manager.Pages.Medic;

public partial class ReferralCaseGrid
{
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    [Parameter] public MedicalVisit Visit { get; set; } = null!;
    [Parameter] public bool CanAdd { get; set; }

    [Parameter] public bool CanEdit { get; set; }

    private IEnumerable<MedicalReferral>? DetailGridData { get; set; }
    private Session Session1 { get; set; } = null!;

    private IEnumerable<string>? _specialistList;

    protected override void OnInitialized()
    {
        Session1 = Visit.Session;
        //Visit = Session1.FindObject<MedicalVisit>(new BinaryOperator("Oid", VisitOid));

        DetailGridData = Visit.MedicalReferrals.ToList();
        if (CanAdd || CanEdit)
        {
            //Load data
            _specialistList =
                File.ReadAllLines(Path.Combine(HostEnvironment.WebRootPath, "content", "SpecialistsList.txt"));
        }
    }

    #region Edit

    private async Task ReferGrid_Action(ActionEventArgs<MedicalReferral> e)
    {
        switch (e.RequestType)
        {
            case Action.Add:
                e.Data = new MedicalReferral(Session1);
                break;
            case Action.BeginEdit:
                e.Data = await Session1.FindObjectAsync<MedicalReferral>(new BinaryOperator("Oid", e.RowData.Oid));
                break;
            case Action.Save:
                var editModel = e.Data;
                //Validation
                if (string.IsNullOrEmpty(editModel?.Specialist))
                {
                    Snackbar.Add("لطفاً تخصص مورد نظر را برای ارجاع انتخاب کنید.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                //Check not added before
                if (Visit.MedicalReferrals.Any(x => x.Specialist == editModel.Specialist))
                {
                    Snackbar.Add("تخصص انتخاب شده تکراری است.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                editModel.Status = "ارجاع به متخصص";
                Visit.MedicalReferrals.Add(editModel);
                break;
            case Action.Delete:
                e.RowData.Delete();
                break;
        }
    }
    #endregion
}