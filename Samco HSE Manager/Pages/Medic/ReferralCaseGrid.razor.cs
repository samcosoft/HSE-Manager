using DevExpress.Blazor;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using System.Diagnostics.CodeAnalysis;
using MudBlazor;

namespace Samco_HSE_Manager.Pages.Medic;

public partial class ReferralCaseGrid
{
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;

    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    [Parameter]
    public MedicalVisit? Visit { get; set; }
    [Parameter]
    public bool IsEditable { get; set; }

    private DxGrid ReferralGrid { get; set; } = null!;
    private IEnumerable<MedicalReferral>? DetailGridData { get; set; }
    private Session Session1 { get; set; } = null!;

    private IEnumerable<string>? _specialistList;
    private MedicalReferral? _selReferral;

    protected override void OnInitialized()
    {
        Session1 = Visit!.Session;

        DetailGridData = Visit?.MedicalReferrals.ToList();
        if (IsEditable)
        {
            //Load data
            _specialistList = File.ReadAllLines(Path.Combine(HostEnvironment.WebRootPath, "content", "SpecialistsList.txt"));
            _selReferral = new MedicalReferral(Session1);
        }
    }

    #region Edit

    private void AddReferralBtnClick()
    {
        //validation
        if (string.IsNullOrEmpty(_selReferral?.Specialist))
        {
            Snackbar.Add("لطفاً تخصص مورد نظر را برای ارجاع انتخاب کنید.", Severity.Error);
            return;
        }
        _selReferral.Status = "باز";
        Visit!.MedicalReferrals.Add(_selReferral);
        DetailGridData = Visit?.MedicalReferrals.ToList();
        _selReferral = new MedicalReferral(Session1);
    }
    private void ReferralGrid_SelectionChanged(object itm)
    {
         _selReferral = (MedicalReferral?)itm;
    }
    private void DelReferralClick()
    {
        if (ReferralGrid.SelectedDataItem == null)
        {
            Snackbar.Add("لطفاً یک مورد را از لیست زیر انتخاب کنید.", Severity.Error);
            return;
        }
        var dataItem = (MedicalReferral)ReferralGrid.SelectedDataItem;
        dataItem.Delete();
        DetailGridData = Visit?.MedicalReferrals.ToList();
        _selReferral = new MedicalReferral(Session1);

    }

    #endregion
}