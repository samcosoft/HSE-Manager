using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Samco_HSE.HSEData;
using Syncfusion.Blazor.DropDowns;
using DialogResult = DevExpress.Utils.CommonDialogs.Internal.DialogResult;

namespace Samco_HSE_Manager.Pages.Officer.PersonnelEdit;

public partial class EquipmentSelector : IDisposable
{
    [CascadingParameter] public MudDialogInstance MudDialogParent { get; set; } = null!;
    [Parameter] public int? PersonId { get; set; }
    private Samco_HSE.HSEData.Personnel? SelPersonnel { get; set; }
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private Session? Session1 { get; set; }
    private Rig? _currentRig;
    private IEnumerable<Equipment>? EquipmentsList { get; set; }

    // private SfGrid<Equipment>? EquipmentGrid { get; set; }
    private SfDropDownList<int, Equipment>? _equipmentSelectorList;
    private MudNumericField<int>? EquipCountBox { get; set; }

    private readonly Dictionary<Equipment, int> _selectedEquipment = new();

    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
        var loggedUser = await
            Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
        _currentRig = loggedUser.ActiveRig;
        EquipmentsList = await Session1.Query<EquipmentStock>()
            .Where(x => x.RigNo.Oid == _currentRig.Oid && x.Counts > 0)
            .Select(x => x.EquipmentName).ToListAsync();
        SelPersonnel = await Session1.GetObjectByKeyAsync<Samco_HSE.HSEData.Personnel>(PersonId);
    }

    private void AddToListClick()
    {
        if(_equipmentSelectorList?.Value == null) return;
        //Check availability
        if (SelPersonnel!.ActiveRig.EquipmentStocks.FirstOrDefault(x =>
                    x.EquipmentName.Oid == _equipmentSelectorList!.Value)!
                .Counts < EquipCountBox!.Value)
        {
            Snackbar.Add($"تعداد تجهیز انتخاب شده از موجودی این تجهیز در آنجا بیشتر است.",
                Severity.Error);
            return;
        }

        _selectedEquipment.Add(Session1!.GetObjectByKey<Equipment>(_equipmentSelectorList!.Value),
            EquipCountBox!.Value);
    }

    private async Task SubmitChanges()
    {
        //Adding equipment
        var loggedUser =
            await Session1!.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
        foreach (var itm in _selectedEquipment)
        {
            for (var i = 0; i < itm.Value; i++)
            {
                SelPersonnel!.PPEs.Add(new PPE(Session1)
                {
                    Agent = loggedUser,
                    DeliverDate = DateTime.Today,
                    EquipmentName = itm.Key,
                });
            }
        }

        SelPersonnel!.Save();

        //set stacks
        foreach (var itm in _selectedEquipment)
        {
            var equipStack = Session1
                .Query<EquipmentStock>()
                .First(x => x.RigNo.Oid == SelPersonnel.ActiveRig.Oid && x.EquipmentName.Oid == itm.Key.Oid);
            equipStack.Counts -= itm.Value;
            equipStack.Save();
        }

        Snackbar.Add("اطلاعات با موفقیت ذخیره شدند.", Severity.Success);
        MudDialogParent.Close(DialogResult.OK);
    }

    public void Dispose()
    {
        (_equipmentSelectorList as IDisposable)?.Dispose();
        Session1?.Dispose();
        (EquipCountBox as IDisposable)?.Dispose();
    }
}