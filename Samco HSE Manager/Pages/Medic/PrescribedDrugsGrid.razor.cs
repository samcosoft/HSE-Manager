using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using MudBlazor;
using Syncfusion.Blazor.Grids;
using Action = Syncfusion.Blazor.Grids.Action;

namespace Samco_HSE_Manager.Pages.Medic;

public partial class PrescribedDrugsGrid
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    [Parameter] public MedicalVisit Visit { get; set; } = null!;
    [Parameter] public bool IsEditable { get; set; }

    private IEnumerable<UsedMedicine>? DetailGridData { get; set; }
    private Session Session1 { get; set; } = null!;

    private IEnumerable<Medication>? _medicationList;
    private UsedMedicine? _selUsedMedicine;
    private string? _selMedicineOid;

    protected override void OnInitialized()
    {
        Session1 = Visit.Session;
        //Visit = Session1.FindObject<MedicalVisit>(new BinaryOperator("Oid", VisitOid));
        DetailGridData = Visit.UsedMedicines.ToList();
        if (IsEditable)
        {
            //DetailGridData = new List<UsedMedicine>();
            //Load data
            while (Session1.IsObjectsLoading)
            {
                Task.Delay(1000);
            }

            _medicationList = Session1.Query<Medication>().Where(x => x.MedicationStocks.Any(y =>
                y.RigNo.Oid == SamcoSoftShared.CurrentUser!.ActiveRig.Oid
                && y.AvailCount > 0)).ToList();
            _selUsedMedicine = new UsedMedicine(Session1) { MedCount = 1 };
        }
    }

    #region Edit

    private void AddMedicineBtnClick()
    {
        _selUsedMedicine!.MedicName = Session1.FindObject<Medication>(new BinaryOperator("Oid", _selMedicineOid));
        //validation
        if (_selUsedMedicine.MedCount == 0)
        {
            Snackbar.Add("لطفاً نام دارو و یا تجهیز و همچنین تعداد آن را وارد کنید.", Severity.Error);
            return;
        }

        //Check repeated data
        var prevDrug = Visit.UsedMedicines.FirstOrDefault(x => x.MedicName.Oid == _selUsedMedicine.MedicName.Oid);
        if (prevDrug != null && prevDrug.Oid != _selUsedMedicine.Oid)
        {
            Snackbar.Add("دارو / تجهیز تکراری است.", Severity.Error);
            return;
        }

        //Check availability
        var availDrug = Session1.Query<MedicationStock>().FirstOrDefault(x =>
            x.RigNo.Oid == SamcoSoftShared.CurrentUser!.ActiveRig.Oid &&
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
            //_selUsedMedicine.PatientName = Visit;
            //_selUsedMedicine.Save();
            Visit.UsedMedicines.Add(_selUsedMedicine);
            Visit.Save();
        }
        else
        {
            availDrug.AvailCount += _prevCount;
            availDrug.AvailCount -= _selUsedMedicine.MedCount;
            _prevCount = _selUsedMedicine.MedCount;
            availDrug.Save();
        }

        DetailGridData = Visit.UsedMedicines.ToList();
        _selUsedMedicine = new UsedMedicine(Session1) { MedCount = 1 };
    }

    private short _prevCount;

    private void Grid_Save(ActionEventArgs<UsedMedicine> e)
    {
        if (e.RequestType == Action.Delete)
        {
            var dataItem = e.Data;
            var availDrug = Session1.Query<MedicationStock>().First(x =>
                x.RigNo.Oid == SamcoSoftShared.CurrentUser!.ActiveRig.Oid &&
                x.MedicName.Oid == dataItem.MedicName.Oid);
            //Change stock medicines
            availDrug.AvailCount += dataItem.MedCount;
            availDrug.Save();
            e.RowData.Delete();
        }
    }

    #endregion
}