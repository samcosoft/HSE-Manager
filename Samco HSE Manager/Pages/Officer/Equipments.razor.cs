using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using MudBlazor;
using Syncfusion.Blazor.Grids;
using Syncfusion.Blazor.Popups;
using Action = Syncfusion.Blazor.Grids.Action;

namespace Samco_HSE_Manager.Pages.Officer;

public partial class Equipments : IDisposable
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private Session Session1 { get; set; } = null!;
    private IEnumerable<Equipment>? EquipmentsList { get; set; }
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
        EquipmentsList = await Session1.Query<Equipment>().ToListAsync();
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

    private string EquipmentGridUnbound(int rigOid, Equipment equip)
    {
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
                editModel.WhoNeed = string.Join(";",Consumers);
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

    private SfDialog? SetNumberModal { get; set; }
    private EquipmentStock? _selEquipmentStock;
    private Rig? _selRig;

    private async Task OnSetNumberBtnClick()
    {
        if (EquipmentGrid!.SelectedRecords.Any() == false)
        {
            Snackbar.Add("لطفاً یک تجهیز را از لیست زیر انتخاب کنید.", Severity.Warning);
            return;
        }

        _selEquipmentStock = new EquipmentStock(Session1)
            { EquipmentName = EquipmentGrid!.SelectedRecords.First() };
        //auto select rig
        if (Rigs?.Count() == 1) _selRig = Rigs.First();
        await SetNumberModal!.ShowAsync();
    }

    private void RigSelectionChanged(Rig itm)
    {
        //Change data source if needed
        var selEquipment = EquipmentGrid!.SelectedRecords.First();
        //Get stock items
        var stockItm = Session1.Query<EquipmentStock>().Where(x => x.RigNo.Oid == (itm).Oid &&
                                                                   x.EquipmentName.Oid == selEquipment.Oid);
        if (stockItm.Any())
        {
            _selEquipmentStock = stockItm.First();
        }
        else
        {
            _selEquipmentStock = new EquipmentStock(Session1)
            {
                EquipmentName = EquipmentGrid!.SelectedRecords.First(),
                RigNo = itm
            };
        }
    }

    private async Task SetCountOkBtnClick()
    {
        if (_selEquipmentStock?.RigNo == null)
        {
            Snackbar.Add("لطفاً دکل را انتخاب کنید.", Severity.Error);
            return;
        }

        _selEquipmentStock?.Save();
        Snackbar.Add("اطلاعات با موفقیت ثبت شد.", Severity.Success);
        await SetNumberModal!.HideAsync();
    }

    #endregion

    #region DistributeEquipment

    private IEnumerable<Samco_HSE.HSEData.Personnel>? Personnel { get; set; }
    private SfGrid<Samco_HSE.HSEData.Personnel>? PersonnelGrid { get; set; }
    private SfDialog? DistModal { get; set; }
    private string? _alertMessage;
    private bool _alertVisible;

    private async Task OnDistributeBtnClick()
    {
        if (EquipmentGrid!.SelectedRecords.Any() == false)
        {
            Snackbar.Add("لطفاً یک تجهیز را از لیست زیر انتخاب کنید.", Severity.Warning);
            return;
        }

        if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
        {
            var loggedUser =
                await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
            Personnel = await Session1.Query<Samco_HSE.HSEData.Personnel>()
                .Where(x => loggedUser.Rigs.Contains(x.ActiveRig)).ToListAsync();
        }
        else
        {
            Personnel = await Session1.Query<Samco_HSE.HSEData.Personnel>().ToListAsync();
        }

        await DistModal!.ShowAsync();
    }

    private void PersonnelGrid_SelectionChanged()
    {
        var itmList = PersonnelGrid!.SelectedRecords;
        var selEquipment = EquipmentGrid!.SelectedRecords.First();
        _alertMessage = string.Empty;
        _alertVisible = false;
        foreach (var personnel in itmList.Where(personnel => personnel.PPEs.Any(x =>
                     x.EquipmentName.Oid == selEquipment.Oid && x.DeliverDate > DateTime.Today.AddMonths(-6))))
        {
            _alertMessage += $"{personnel.PersonnelName} به تازگی این تجهیز را دریافت کرده است." + Environment.NewLine;
            _alertVisible = true;
        }
        
        var personnelRigGroup = itmList.GroupBy(x => x.ActiveRig)
            .Select(x => new { RigName = x.Key, PersonnelCount = x.Count() }).ToList();
        foreach (var itm in from itm in personnelRigGroup
                 let availableStock = selEquipment.EquipmentStocks.FirstOrDefault(x => x.RigNo?.Oid == itm.RigName.Oid)
                 where availableStock == null || itm.PersonnelCount > availableStock.Counts
                 select itm)
        {
            _alertMessage +=
                $"تعداد افراد انتخاب شده در {itm.RigName.Name} از موجودی این تجهیز در آنجا بیشتر است." +
                Environment.NewLine;
            _alertVisible = true;
        }
    }
    #endregion

    private async Task DistributeOkBtnClick()
    {
        //Validation
        if (!PersonnelGrid!.SelectedRecords.Any())
        {
            Snackbar.Add("لطفاً پرسنل مورد نظر را از لیست زیر انتخاب کنید.", Severity.Error);
            return;
        }

        var selPersonnel = PersonnelGrid.SelectedRecords;
        var selEquipment = EquipmentGrid!.SelectedRecords.First();
        var personnelRigGroup = selPersonnel.GroupBy(x => x.ActiveRig)
            .Select(x => new { RigName = x.Key, PersonnelCount = x.Count() }).ToList();
        foreach (var itm in from itm in personnelRigGroup
                 let availableStock = selEquipment.EquipmentStocks.FirstOrDefault(x => x.RigNo.Oid == itm.RigName.Oid)
                 where availableStock == null || itm.PersonnelCount > availableStock.Counts
                 select itm)
        {
            Snackbar.Add($"تعداد افراد انتخاب شده در {itm.RigName.Name} از موجودی این تجهیز در آنجا بیشتر است.",
                Severity.Error);
            return;
        }

        //Adding equipment
        var loggedUser =
            await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));

        foreach (var personnel in selPersonnel)
        {
            personnel.PPEs.Add(new PPE(Session1)
            {
                Agent = loggedUser,
                DeliverDate = DateTime.Today,
                EquipmentName = selEquipment,
            });
            personnel.Save();
        }

        //set stacks
        foreach (var itm in personnelRigGroup)
        {
            var equipStack = Session1
                .Query<EquipmentStock>()
                .First(x => x.RigNo.Oid == itm.RigName.Oid && x.EquipmentName.Oid == selEquipment.Oid);
            equipStack.Counts -= itm.PersonnelCount;
            equipStack.Save();
        }

        Snackbar.Add("اطلاعات با موفقیت ذخیره شد.", Severity.Success);
        await DistModal!.HideAsync();
    }
}