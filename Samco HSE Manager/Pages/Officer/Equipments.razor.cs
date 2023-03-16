using BootstrapBlazor.Components;
using DevExpress.Blazor;
using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Samco_HSE.HSEData;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components.Rendering;

namespace Samco_HSE_Manager.Pages.Officer;

public partial class Equipments : IDisposable
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = null!;

    [Inject]
    [NotNull]
    private ToastService? ToastService { get; set; }

    [Inject]
    [NotNull]
    private MessageService? MessageService { get; set; }
    private Session Session1 { get; set; } = null!;
    private IEnumerable<Equipment>? EquipmentsList { get; set; }
    private IEnumerable<Rig> Rigs { get; set; } = null!;
    private IEnumerable<string>? RigRoles { get; set; }

    private readonly IEnumerable<string> _ppeKind = new List<string>
    {
        "تجهیزات حفاظت فردی","تجهیزات اختصاصی کاری"
    };
    private DxGrid? EquipmentGrid { get; set; }
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
    private RenderFragment BuildColumnsGrid()
    {
        using var tempSession = new Session(DataLayer);
        IEnumerable<Rig> rigs;
        if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
        {
            var loggedUser =
                tempSession.FindObject<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
            rigs = loggedUser.Rigs.ToList();
        }
        else
        {
            //Owner
            rigs = tempSession.Query<Rig>().ToList();
        }

        void NewColumns(RenderTreeBuilder b)
        {
            //b.OpenComponent(0, typeof(DxGridDataColumn));
            //b.AddAttribute(0, "FieldName", "1");
            //b.AddAttribute(0, "Caption", "تعداد در دکل");
            //b.AddAttribute(0, "MinWidth", 200);
            //b.AddAttribute(0, "UnboundType", "GridUnboundColumnType.Integer");
            //b.CloseComponent();

            foreach (var rig in rigs)
            {
                b.OpenComponent(0, typeof(DxGridDataColumn));
                b.AddAttribute(0, "FieldName", rig.Oid.ToString());
                b.AddAttribute(0, "Caption", $"تعداد در {rig.Name}");
                b.AddAttribute(0, "MinWidth", 200);
                b.AddAttribute(0, "UnboundType", GridUnboundColumnType.Integer);
                b.CloseComponent();
            }
        }

        return NewColumns;
    }
    private void EquipmentGridUnbound(GridUnboundColumnDataEventArgs e)
    {
        using var tempSession = new Session(DataLayer);
        var currentEquipment =
            tempSession.FindObject<Equipment>(new BinaryOperator("Oid", ((Equipment)e.DataItem).Oid));
        //var equipStock = (from itm in tempSession.Query<EquipmentStock>() where itm.RigNo.Oid == int.Parse(e.FieldName) && itm.EquipmentName.Oid == currentEquipment.Oid select itm).FirstOrDefault();
        var equipStock = tempSession.Query<EquipmentStock>().FirstOrDefault(itm => itm.RigNo.Oid == int.Parse(e.FieldName) && itm.EquipmentName.Oid == currentEquipment.Oid);
        e.Value = equipStock?.Counts ?? 0;
    }
    private IEnumerable<string>? Consumers { get; set; }
    private void EquipmentGrid_EditStart(GridEditStartEventArgs e)
    {
        if (SamcoSoftShared.CurrentUserRole < SamcoSoftShared.SiteRoles.Supervisor) return;
        MessageService.Show(new MessageOption
        {
            Color = Color.Warning,
            Content = "شما اجازه ویرایش تجهیزات را ندارید.",
            IsAutoHide = true
        });
        e.Cancel = true;
    }
    private void EquipmentEditModel(GridCustomizeEditModelEventArgs e)
    {
        var dataItem = (Equipment?)e.DataItem ?? new Equipment(Session1);
        Consumers = dataItem.WhoNeed?.Split(";");
        e.EditModel = dataItem;
    }
    private void SelAllClick()
    {
        Consumers = RigRoles;
    }
    private async Task OnEditModelSaving(GridEditModelSavingEventArgs e)
    {
        var editModel = (Equipment)e.EditModel;
        //Validation
        if (string.IsNullOrEmpty(editModel.Name) || string.IsNullOrEmpty(editModel.EquipType) ||
            Consumers == null)
        {
            await ToastService.Error("خطا در افزودن تجهیز", "لطفاً موارد الزامی را تکمیل کنید.");
            e.Cancel = true;
            return;
        }
        //Check equipment not existed before
        var selEquip = Session1.FindObject<Equipment>(new BinaryOperator(nameof(Equipment.Name), editModel.Name));
        if (e.IsNew)
        {
            if (selEquip != null && selEquip.Model == editModel.Model)
            {
                //Equipment existed
                await ToastService.Error("خطا در ثبت اطلاعات",
                    "تجهیز با همین نام در سیستم وجود دارد. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.");
                e.Cancel = true;
                return;
            }
        }
        else
        {
            if (selEquip != null && selEquip.Oid != editModel.Oid && selEquip.Model == editModel.Model)
            {
                //Equipment existed
                await ToastService.Error("خطا در ثبت اطلاعات",
                    "تجهیز با همین نام در سیستم وجود دارد. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.");
                e.Cancel = true;
                return;
            }
        }
        editModel.Save();
        EquipmentsList = await Session1.Query<Equipment>().ToListAsync();
    }

    private async Task OnDataItemDeleting(GridDataItemDeletingEventArgs e)
    {
        if (SamcoSoftShared.CurrentUserRole > SamcoSoftShared.SiteRoles.Supervisor)
        {
            await MessageService.Show(new MessageOption
            {
                Color = Color.Warning,
                Content = "شما اجازه ویرایش تجهیزات را ندارید.",
                IsAutoHide = true
            });
            e.Cancel = true;
            return;
        }

        var dataItem =
            await Session1.FindObjectAsync<Equipment>(new BinaryOperator("Oid", (e.DataItem as Equipment)!.Oid));
        dataItem?.Delete();
        EquipmentsList = await Session1.Query<Equipment>().ToListAsync();
    }

    #endregion

    #region EquipmentCount

    private DxPopup? SetNumberModal { get; set; }
    private DxComboBox<Rig, Rig>? RigBx { get; set; }
    private EquipmentStock? _selEquipmentStock;
    private async Task OnSetNumberBtnClick()
    {
        if (EquipmentGrid!.SelectedDataItems.Any() == false)
        {
            await ToastService.Warning("خطا در تعیین تعداد", "لطفاً یک تجهیز را از لیست زیر انتخاب کنید.");
            return;
        }

        _selEquipmentStock = new EquipmentStock(Session1) { EquipmentName = (Equipment)EquipmentGrid!.SelectedDataItem };
        //auto select rig
        await SetNumberModal!.ShowAsync();
#pragma warning disable BL0005
        if (Rigs.Count() == 1) RigBx!.Text = Rigs.First().Name;
#pragma warning restore BL0005
    }

    private void RigSelectionChanged(object itm)
    {
        //Change data source if needed
        var selEquipment = (Equipment)EquipmentGrid!.SelectedDataItem;
        //Get stock items
        var stockItm = Session1.Query<EquipmentStock>().Where(x => x.RigNo.Oid == ((Rig)itm).Oid &&
                                                                   x.EquipmentName.Oid == selEquipment.Oid);
        if (stockItm.Any())
        {
            _selEquipmentStock = stockItm.First();
        }
        else
        {
            _selEquipmentStock = new EquipmentStock(Session1)
            {
                EquipmentName = (Equipment)EquipmentGrid!.SelectedDataItem,
                RigNo = (Rig)itm
            };
        }
    }

    private async Task SetCountOkBtnClick()
    {
        if (_selEquipmentStock?.RigNo == null)
        {
            await MessageService.Show(new MessageOption
            {
                Color = Color.Danger,
                Content = "لطفاً دکل را انتخاب کنید."
            });
            return;
        }
        _selEquipmentStock?.Save();
        await MessageService.Show(new MessageOption
        {
            Color = Color.Success,
            Content = "اطلاعات با موفقیت ثبت شد."
        });
        EquipmentGrid!.Reload();
        await SetNumberModal!.CloseAsync();
    }
    #endregion

    #region DistributeEquipment

    private IEnumerable<Samco_HSE.HSEData.Personnel>? Personnel { get; set; }
    private DxGrid? PersonnelGrid { get; set; }
    private DxPopup? DistModal { get; set; }
    private string? _alertMessage;
    private bool _alertVisible;

    private async Task OnDistributeBtnClick()
    {
        if (EquipmentGrid!.SelectedDataItems.Any() == false)
        {
            await ToastService.Warning("خطا در توزیع تجهیزات", "لطفاً یک تجهیز را از لیست زیر انتخاب کنید.");
            return;
        }

        if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
        {
            var loggedUser =
                await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
            Personnel = await Session1.Query<Samco_HSE.HSEData.Personnel>().Where(x => loggedUser.Rigs.Contains(x.ActiveRig)).ToListAsync();
        }
        else
        {
            Personnel = await Session1.Query<Samco_HSE.HSEData.Personnel>().ToListAsync();
        }

        await DistModal!.ShowAsync();
    }

    private void PersonnelGrid_SelectionChanged(IReadOnlyList<object> obj)
    {
        var itmList = obj.Cast<Samco_HSE.HSEData.Personnel>().ToList();
        var selEquipment = (Equipment)EquipmentGrid!.SelectedDataItem;
        _alertMessage = string.Empty;
        _alertVisible = false;
        foreach (var personnel in itmList.Where(personnel => personnel.PPEs.Any(x =>
                     x.EquipmentName.Oid == selEquipment.Oid && x.DeliverDate > DateTime.Today.AddMonths(-6))))
        {
            _alertMessage += $"{personnel.PersonnelName} به تازگی این تجهیز را دریافت کرده است." + Environment.NewLine;
            _alertVisible = true;
        }
        var personnelRigGroup = itmList.GroupBy(x => x.ActiveRig).Select(x => new { RigName = x.Key, PersonnelCount = x.Count() }).ToList();
        foreach (var itm in from itm in personnelRigGroup let availableStock = selEquipment.EquipmentStocks.FirstOrDefault(x => x.RigNo.Oid == itm.RigName.Oid) where availableStock == null || itm.PersonnelCount > availableStock.Counts select itm)
        {
            _alertMessage +=
                $"تعداد افراد انتخاب شده در {itm.RigName.Name} از موجودی این تجهیز در آنجا بیشتر است." + Environment.NewLine;
            _alertVisible = true;
        }
    }
    #endregion

    private async Task DistributeOkBtnClick()
    {
        //Validation
        if (!PersonnelGrid!.SelectedDataItems.Any())
        {
            await MessageService.Show(new MessageOption 
            {
                Color = Color.Danger,
                Content = "لطفاً پرسنل مورد نظر را از لیست زیر انتخاب کنید."
            });
            return;
        }
        var selPersonnel = PersonnelGrid.SelectedDataItems.Cast<Samco_HSE.HSEData.Personnel>().ToList();
        var selEquipment = (Equipment)EquipmentGrid!.SelectedDataItem;
        var personnelRigGroup = selPersonnel.GroupBy(x => x.ActiveRig).Select(x => new { RigName = x.Key, PersonnelCount = x.Count() }).ToList();
        foreach (var itm in from itm in personnelRigGroup let availableStock = selEquipment.EquipmentStocks.FirstOrDefault(x => x.RigNo.Oid == itm.RigName.Oid) where availableStock == null || itm.PersonnelCount > availableStock.Counts select itm)
        {
            await MessageService.Show(new MessageOption
            {
                Color = Color.Danger,
                Content = $"تعداد افراد انتخاب شده در {itm.RigName.Name} از موجودی این تجهیز در آنجا بیشتر است."
            });
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
                .Query<EquipmentStock>().First(x => x.RigNo.Oid == itm.RigName.Oid && x.EquipmentName.Oid == selEquipment.Oid);
            equipStack.Counts -= itm.PersonnelCount;
            equipStack.Save();
        }

        await MessageService.Show(new MessageOption
        {
            Color = Color.Success,
            Content = "اطلاعات با موفقیت ذخیره شد."
        });

        await DistModal!.CloseAsync();
    }
}