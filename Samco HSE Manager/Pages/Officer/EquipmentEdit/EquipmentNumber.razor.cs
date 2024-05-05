using System.Data.Entity;
using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Samco_HSE.HSEData;
using DialogResult = DevExpress.Utils.CommonDialogs.Internal.DialogResult;

namespace Samco_HSE_Manager.Pages.Officer.EquipmentEdit;

public partial class EquipmentNumber
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    [CascadingParameter] public MudDialogInstance MudDialogParent { get; set; } = null!;
    [Parameter] public int? EquipmentId { get; set; }
    private EquipmentStock? _selEquipmentStock;
    private IEnumerable<Rig>? Rigs { get; set; }
    private Rig? _selRig;

    private Session Session1 { get; set; } = null!;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        Session1 = new Session(DataLayer);
        if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
        {
            var loggedUser =
                Session1.FindObject<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
            Rigs = loggedUser.Rigs;
        }
        else
        {
            //Owner
            Rigs = Session1.Query<Rig>().ToList();
        }

        _selEquipmentStock = new EquipmentStock(Session1)
        {
            EquipmentName = Session1.GetObjectByKey<Equipment>(EquipmentId),
        };
    }

    private void RigSelectionChanged(Rig? itm)
    {
        if (itm == null) return;
        //Get stock items
        var stockItm = Session1.Query<EquipmentStock>().Where(x => x.RigNo.Oid == itm.Oid &&
                                                                   x.EquipmentName.Oid == EquipmentId);
        _selRig = itm;
        if (stockItm.Any())
        {
            _selEquipmentStock = stockItm.First();
        }
        else
        {
            _selEquipmentStock = new EquipmentStock(Session1)
            {
                EquipmentName = Session1.GetObjectByKey<Equipment>(EquipmentId),
                RigNo = _selRig
            };
        }
    }
    private void SetCountOkBtnClick()
    {
        if (_selEquipmentStock?.RigNo == null)
        {
            Snackbar.Add("لطفاً دکل را انتخاب کنید.", Severity.Error);
            return;
        }

        _selEquipmentStock?.Save();
        Snackbar.Add("اطلاعات با موفقیت ذخیره شدند.", Severity.Success);
        MudDialogParent.Close(DialogResult.OK);
    }
}