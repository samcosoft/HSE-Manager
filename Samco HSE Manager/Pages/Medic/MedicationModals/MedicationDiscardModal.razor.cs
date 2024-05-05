using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Samco_HSE.HSEData;

namespace Samco_HSE_Manager.Pages.Medic.MedicationModals;

public partial class MedicationDiscardModal
{
    [CascadingParameter]
    MudDialogInstance MudDialog { get; set; } = null!;

    private Session Session1 { get; set; } = null!;
    private IEnumerable<Rig> Rigs { get; set; } = null!;
    private IEnumerable<Medication> MedicationList { get; set; } = null!;
    private readonly Dictionary<Medication, short> _selMedicationList = new();
    private Rig? _selRig;
    private int? _selMedication;
    private short _selCount;
    private string? _alertText;
    private string? _reason;

    protected override void OnInitialized()
    {
        Session1 = new Session(DataLayer);
        if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
        {
            var loggedUser =
                Session1.GetObjectByKey<User>(SamcoSoftShared.CurrentUserId);
            Rigs = loggedUser.Rigs;
        }
        else
        {
            //Owner
            Rigs = Session1.Query<Rig>().ToList();
        }

        MedicationList = Session1.Query<Medication>().ToList();

        base.OnInitialized();
    }

    private void AddToList()
    {
        if (_selMedication == null || _selCount == 0)
        {
            Snackbar.Add("لطفاً نام دارو و یا تجهیز و تعداد آن را وارد کنید.", Severity.Warning);
            return;
        }

        var selMedication = Session1.GetObjectByKey<Medication>(_selMedication);

        var medStock = Session1.Query<MedicationStock>().FirstOrDefault(x => x.RigNo.Oid == _selRig!.Oid &&
                                                                                x.MedicName.Oid == selMedication.Oid);
        if (medStock == null)
        {
            Snackbar.Add("این دارو / تجهیز در این محل وجود ندارد.", Severity.Warning);
            return;
        }

        if (!_selMedicationList.TryAdd(selMedication, _selCount))
        {
            Snackbar.Add("این دارو / تجهیز قبلاً انتخاب شده است.", Severity.Warning);
        }

        CheckAvailability();
        _selMedication = null;
        _selCount = 0;
    }

    private void RemoveFromList(Medication item)
    {
        _selMedicationList.Remove(item);
        CheckAvailability();
    }

    private void CheckAvailability()
    {
        _alertText = string.Empty;
        if (_selMedicationList.Count == 0 || _selRig == null) return;
        foreach (var itm in _selMedicationList)
        {
            var medStock = Session1.Query<MedicationStock>().FirstOrDefault(x => x.RigNo.Oid == _selRig!.Oid &&
                                                                                x.MedicName.Oid == itm.Key.Oid);

            if (medStock != null && medStock.AvailCount < itm.Value)
            {
                _alertText += $"تعداد دارو / تجهیز {itm.Key.Name} از موجودی محل بیشتر است." + Environment.NewLine;
            }
        }
    }

    void Cancel() => MudDialog.Cancel();

    private void RigSelectionChanged(Rig obj)
    {
        _selRig = obj;
        CheckAvailability();
    }

    private void Submit()
    {
        if (_selMedicationList.Count == 0 || _selRig == null)
        {
            Snackbar.Add("لطفاً نام محل و موارد دور انداخته شده را وارد کنید.", Severity.Error);
            return;
        }

        foreach (var itm in _selMedicationList)
        {
            //Remove from stock
            var medStock = Session1.Query<MedicationStock>().FirstOrDefault(x => x.RigNo.Oid == _selRig!.Oid &&
                                                                                                x.MedicName.Oid == itm.Key.Oid);
            if (medStock != null)
            {
                medStock.AvailCount -= itm.Value;
                if (medStock.AvailCount < 0) medStock.AvailCount = 0;
                medStock.Save();
            }
            else { return; }

            var discard = new DisposedMedicine(Session1)
            {
                RigNo = _selRig,
                MedicName = itm.Key,
                MedCount = itm.Value,
                DisDate = DateTime.Today,
                Reason = _reason
            };

            discard.Save();
        }

        Snackbar.Add("اطلاعات با موفقیت ثبت شد.", Severity.Success);
        MudDialog.Close(DialogResult.Ok(true));
    }
}