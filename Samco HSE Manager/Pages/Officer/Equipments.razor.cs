using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using MudBlazor;
using Samco_HSE_Manager.Pages.Officer.EquipmentsModal;
using Syncfusion.Blazor.Grids;
using Action = Syncfusion.Blazor.Grids.Action;

namespace Samco_HSE_Manager.Pages.Officer;

public partial class Equipments : IDisposable
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;

    private Session Session1 { get; set; } = null!;
    private XPCollection<Equipment>? EquipmentsList { get; set; }
    private IEnumerable<Rig>? Rigs { get; set; }
    private IEnumerable<string>? RigRoles { get; set; }

    private readonly IEnumerable<string> _ppeKind = new List<string>
    {
        "تجهیزات حفاظت فردی", "تجهیزات اختصاصی کاری"
    };

    private SfGrid<Equipment>? EquipmentGrid { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
        //EquipmentsList = await Session1.Query<Equipment>().ToListAsync();
        EquipmentsList = new XPCollection<Equipment>(Session1);
        if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
        {
            var loggedUser = await
                Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
            Rigs = loggedUser.Rigs;
        }
        else
        {
            //Owner
            Rigs = Session1.Query<Rig>().ToList();
        }

        RigRoles = await File.ReadAllLinesAsync(Path.Combine(HostEnvironment.WebRootPath, "content", "RigRoles.txt"));
    }

    public void Dispose()
    {
        Session1.Dispose();
    }

    #region EquipmentGrid

    private string EquipmentGridUnbound(int rigOid, Equipment? equip)
    {
        if (equip == null) return string.Empty;
        using var tempSession = new Session(DataLayer);
        //var equipStock = (from itm in tempSession.Query<EquipmentStock>() where itm.RigNo.Oid == int.Parse(e.FieldName) && itm.EquipmentName.Oid == currentEquipment.Oid select itm).FirstOrDefault();
        var equipStock = tempSession.Query<EquipmentStock>().FirstOrDefault(itm =>
            itm.RigNo.Oid == rigOid && itm.EquipmentName.Oid == equip.Oid);
        return equipStock?.Counts.ToString() ?? "0";
    }

    private string[]? Consumers { get; set; }
    private async Task EquipGrid_Action(ActionEventArgs<Equipment> e)
    {
        switch (e.RequestType)
        {
            case Action.Add:
                if (SamcoSoftShared.CurrentUserRole > SamcoSoftShared.SiteRoles.Supervisor)
                {
                    Snackbar.Add("شما اجازه ویرایش تجهیزات را ندارید.", Severity.Warning);
                    e.Cancel = true;
                }

                e.Data = new Equipment(Session1);
                break;
            case Action.BeginEdit:
                if (SamcoSoftShared.CurrentUserRole > SamcoSoftShared.SiteRoles.Supervisor)
                {
                    Snackbar.Add("شما اجازه ویرایش تجهیزات را ندارید.", Severity.Warning);
                    e.Cancel = true;
                }

                e.Data = await Session1.GetObjectByKeyAsync<Equipment>(e.RowData.Oid);
                Consumers = e.Data.WhoNeed?.Split(";");
                break;
            case Action.Save:
                var editModel = e.Data;
                //Validation
                if (string.IsNullOrEmpty(editModel.Name) || string.IsNullOrEmpty(editModel.EquipType) ||
                    Consumers == null || !Consumers.Any())
                {
                    Snackbar.Add("لطفاً موارد الزامی را تکمیل کنید.", Severity.Error);
                    e.Cancel = true;
                    return;
                }

                //Check equipment not existed before
                var selEquip =
                    Session1.FindObject<Equipment>(new BinaryOperator(nameof(Equipment.Name), editModel.Name));
                if (editModel.Oid < 0)
                {
                    if (selEquip != null && selEquip.Model == editModel.Model)
                    {
                        //Equipment existed
                        Snackbar.Add(
                            "تجهیز با همین نام در سیستم وجود دارد. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.",
                            Severity.Error);
                        e.Cancel = true;
                        return;
                    }
                }
                else
                {
                    if (selEquip != null && selEquip.Oid != editModel.Oid && selEquip.Model == editModel.Model)
                    {
                        //Equipment existed
                        Snackbar.Add(
                            "تجهیز با همین نام در سیستم وجود دارد. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.",
                            Severity.Error);
                        e.Cancel = true;
                        return;
                    }
                }
                editModel.WhoNeed = string.Join(";", Consumers);
                editModel.Save();
                break;
            case Action.Delete:
                if (SamcoSoftShared.CurrentUserRole > SamcoSoftShared.SiteRoles.Supervisor)
                {
                    Snackbar.Add("شما اجازه ویرایش تجهیزات را ندارید.", Severity.Warning);
                    e.Cancel = true;
                    return;
                }
                e.RowData.Delete();
                break;
        }
    }

    private async Task EquipToolbarClickHandler(Syncfusion.Blazor.Navigations.ClickEventArgs args)
    {
        if (args.Item.Id == "equipGrid_Excel Export")
        {
            Snackbar.Add("سیستم در حال ایجاد فایل است. لطفاً تا دانلود گزارش شکیبا باشید...", Severity.Info);
            var exportProperties = new ExcelExportProperties
            {
                FileName = "EquipmentList.xlsx"
            };
            await EquipmentGrid!.ExportToExcelAsync(exportProperties);
        }
    }

    #endregion

    #region EquipmentCount

    private async Task OnSetNumberBtnClick()
    {
        if (EquipmentGrid!.SelectedRecords.Any() == false)
        {
            Snackbar.Add("لطفاً یک تجهیز را از لیست زیر انتخاب کنید.", Severity.Warning);
            return;
        }

        var dialog = await DialogService.ShowAsync<EquipmentNumberModal>("تنظیم تعداد تجهیزات",
            new DialogParameters { { "SelEquipmentId", EquipmentGrid.SelectedRecords.First().Oid } });
        var result = await dialog.Result;
        if (!result!.Canceled)
        {
            EquipmentsList!.Reload();
        }
    }

    #endregion

    #region DistributeEquipment

    private async Task OnDistributeBtnClick()
    {
        if (EquipmentGrid!.SelectedRecords.Any() == false)
        {
            Snackbar.Add("لطفاً یک تجهیز را از لیست زیر انتخاب کنید.", Severity.Warning);
            return;
        }

        var dialog = await DialogService.ShowAsync<EquipmentDistributeModal>("توزیع تجهیزات",
            new DialogParameters { { "SelEquipmentId", EquipmentGrid.SelectedRecords.First().Oid } });
        var result = await dialog.Result;
        if (!result!.Canceled)
        {
            EquipmentsList!.Reload();
        }

    }
    #endregion
}