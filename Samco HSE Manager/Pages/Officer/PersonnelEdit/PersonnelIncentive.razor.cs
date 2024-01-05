using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Samco_HSE.HSEData;

namespace Samco_HSE_Manager.Pages.Officer.PersonnelEdit;

public partial class PersonnelIncentive
{
    [CascadingParameter] private MudDialogInstance IncentiveDialog { get; set; } = null!;
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Parameter] public int PersonId { get; set; }
    private Session? Session1 { get; set; }
    private Incentive SelIncentive { get; set; } = null!;
    protected override void OnInitialized()
    {
        Session1 = new Session(DataLayer);
        var selPerson =
            Session1.Query<Samco_HSE.HSEData.Personnel>().First(x => x.Oid == PersonId);
        var wellWork = Session1.GetObjectByKey<WellWork>(selPerson.ActiveRig.WellWorks.First(x => x.IsActive).Oid);
        var loggedUser = Session1.GetObjectByKey<User>(SamcoSoftShared.CurrentUserId);
        SelIncentive = new Incentive(Session1) { WorkID = wellWork, PersonnelName = selPerson, Issuer = loggedUser };
    }

    private void SaveIncentive()
    {
        if (SelIncentive.IssueDate == null || string.IsNullOrEmpty(SelIncentive.Reason))
        {
            Snackbar.Add("لطفاً تمام اطلاعات خواسته شده را وارد کنید.", Severity.Error);
            return;
        }
        SelIncentive.Save();
        Snackbar.Add("تشویق ثبت گردید.", Severity.Success);
        IncentiveDialog.Close(DialogResult.Ok(true));
    }

    private void Cancel() => IncentiveDialog.Cancel();
}