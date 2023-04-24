using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using System.Diagnostics.CodeAnalysis;
using DevExpress.Blazor;
using MudBlazor;

namespace Samco_HSE_Manager.Pages.Medic;

public partial class PrescribedDrugsGrid
{
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    [Parameter]
    public MedicalVisit? Visit { get; set; }
    [Parameter]
    public bool IsEditable { get; set; }

    private DxGrid? MedicineGrid { get; set; }
    private IEnumerable<UsedMedicine>? DetailGridData { get; set; }
    private Session Session1 { get; set; } = null!;

    private IEnumerable<Medication>? _medicationList;
    private UsedMedicine? _selUsedMedicine;

    protected override void OnInitialized()
    {
        Session1 = Visit!.Session;

        DetailGridData = Visit?.UsedMedicines.ToList();
        if (IsEditable)
        {
            //DetailGridData = new List<UsedMedicine>();
            //Load data
            _medicationList = Session1.Query<Medication>();
            _selUsedMedicine = new UsedMedicine(Session1) { MedCount = 1 };
        }
    }

    #region Edit

    private void AddMedicineBtnClick()
    {
        //validation
        if (_selUsedMedicine?.MedicName == null || _selUsedMedicine.MedCount == 0)
        {
            Snackbar.Add("لطفاً نام دارو و یا تجهیز و همچنین تعداد آن را وارد کنید.", Severity.Error);
            return;
        }
        //Check repeated data
        var prevDrug = Visit!.UsedMedicines.FirstOrDefault(x => x.MedicName.Oid == _selUsedMedicine.MedicName.Oid);
        if (prevDrug != null && prevDrug.Oid != _selUsedMedicine.Oid)
        {
            Snackbar.Add("دارو / تجهیز تکراری است.", Severity.Error);
            return;
        }
        //Check availability
        var availDrug = Session1.Query<MedicationStock>().FirstOrDefault(x => x.RigNo.Oid == Visit!.Patient.ActiveRig.Oid &&
            x.MedicName.Oid == _selUsedMedicine.MedicName.Oid);
        if (availDrug == null || availDrug.AvailCount < _selUsedMedicine.MedCount)
        {
            Snackbar.Add("تعداد داروی درخواست شده از موجودی دکل بیشتر است.", Severity.Error);
            return;
        }

        if (_selUsedMedicine.Oid < 0)
        {
            //Change stock medicines
            availDrug.AvailCount -= _selUsedMedicine.MedCount;
            availDrug.Save();
            Visit!.UsedMedicines.Add(_selUsedMedicine);
        }
        else
        {
            availDrug.AvailCount += _prevCount;
            availDrug.AvailCount -= _selUsedMedicine.MedCount;
            _prevCount = _selUsedMedicine.MedCount;
            availDrug.Save();
        }

        DetailGridData = Visit?.UsedMedicines.ToList();
        _selUsedMedicine = new UsedMedicine(Session1) { MedCount = 1 };
    }

    private short _prevCount;
    private void MedicineGrid_SelectionChanged(object itm)
    {
        _selUsedMedicine = (UsedMedicine?)itm;
        _prevCount = _selUsedMedicine!.MedCount;
    }
    private void DelMedicineClick()
    {
        if (MedicineGrid?.SelectedDataItem == null)
        {
            Snackbar.Add("لطفاً یک مورد را از لیست زیر انتخاب کنید.", Severity.Error);
            return;
        }

        var dataItem = (UsedMedicine)MedicineGrid.SelectedDataItem;
        //Check availability
        var availDrug = Session1.Query<MedicationStock>().First(x => x.RigNo.Oid == Visit!.Patient.ActiveRig.Oid &&
                                                                              x.MedicName.Oid == dataItem.MedicName.Oid);
        //Change stock medicines
        availDrug.AvailCount += dataItem.MedCount;
        availDrug.Save();

        dataItem.Delete();
        DetailGridData = Visit?.UsedMedicines.ToList();
        _selUsedMedicine = new UsedMedicine(Session1) { MedCount = 1 };
    }

    #endregion

}