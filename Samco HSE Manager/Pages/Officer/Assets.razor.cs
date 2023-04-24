using DevExpress.Blazor;
using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Samco_HSE.HSEData;

namespace Samco_HSE_Manager.Pages.Officer;

public partial class Assets : IDisposable
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;

    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    private Session Session1 { get; set; } = null!;
    private DxGrid AssetGrid { get; set; } = null!;
    private IEnumerable<Asset>? AssetsList { get; set; }
    private IEnumerable<Rig> Rigs { get; set; } = null!;
    private readonly IEnumerable<string> _owners = new List<string>
    {
        "واحد ایمنی","درمانگاه"
    };
    private readonly IEnumerable<string> _status = new List<string>
    {
        "سالم","نیاز به تعمیر","خارج از سرویس"
    };
    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
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

        await LoadInformation();
    }
    private async Task LoadInformation()
    {
        AssetsList = await Session1.Query<Asset>().Where(x => Rigs.Contains(x.RigNo)).ToListAsync();

        if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
        {
            AssetsList = await Session1.Query<Asset>().Where(x => Rigs.Contains(x.RigNo)).ToListAsync();
        }
        else
        {
            //Owner
            AssetsList = await Session1.Query<Asset>().ToListAsync();
        }
    }

    public void Dispose()
    {
        Session1.Dispose();
    }

    #region AssetGrid
    private void AssetGrid_CustomizeElement(GridCustomizeElementEventArgs e)
    {
        if (e.ElementType == GridElementType.DataRow)
        {
            var expDate = (DateTime?)e.Grid.GetRowValue(e.VisibleIndex, "ExpireDate");
            if (expDate != null)
            {
                if (expDate <= DateTime.Today)
                {
                    e.CssClass = "expired-item";
                }

                if (expDate > DateTime.Today && expDate < DateTime.Today.AddMonths(2))
                {
                    e.CssClass = "warning-item";
                }
            }

        }
        //if (e.ElementType == GridElementType.DataCell && e.Column.Name == "Total")
        //{
        //    e.Style = "font-weight: 800";
        //}
        //if (e.ElementType == GridElementType.GroupRow && e.Column.Name == "Country")
        //{
        //    var summaryItems = e.Grid.GetGroupSummaryItems().Select(i => e.Grid.GetGroupSummaryDisplayText(i, e.VisibleIndex));
        //    e.Attributes["title"] = string.Join(", ", summaryItems);
        //}
    }

    private void AssetEditModel(GridCustomizeEditModelEventArgs e)
    {
        var dataItem = (Asset?)e.DataItem ?? new Asset(Session1);
        e.EditModel = dataItem;
    }
    private async Task OnEditModelSaving(GridEditModelSavingEventArgs e)
    {
        var editModel = (Asset)e.EditModel;
        //Validation
        if (string.IsNullOrEmpty(editModel.Name) || string.IsNullOrEmpty(editModel.Owner) ||
            string.IsNullOrEmpty(editModel.Status) || editModel.RigNo == null)
        {
            Snackbar.Add("لطفاً موارد الزامی را تکمیل کنید.", Severity.Error);
            e.Cancel = true;
            return;
        }
        //Check equipment not existed before
        var selEquip = Session1.FindObject<Asset>(new BinaryOperator(nameof(Asset.Name), editModel.Name));
        if (e.IsNew)
        {
            if (selEquip != null && string.IsNullOrEmpty(selEquip.Serial) && selEquip.Serial == editModel.Serial)
            {
                //Equipment existed
            Snackbar.Add("تجهیز با همین مشخصات در سیستم وجود دارد. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.", Severity.Error);
            e.Cancel = true;
                return;
            }
        }
        else
        {
            if (selEquip != null && selEquip.Oid != editModel.Oid &&
                string.IsNullOrEmpty(selEquip.Serial) && selEquip.Serial == editModel.Serial)
            {
                //Equipment existed
                Snackbar.Add("تجهیز با همین مشخصات در سیستم وجود دارد. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.", Severity.Error);
                e.Cancel = true;
                return;
            }
        }
        editModel.Save();
        await LoadInformation();
    }

    private async Task OnDataItemDeleting(GridDataItemDeletingEventArgs e)
    {
        var dataItem =
            await Session1.FindObjectAsync<Asset>(new BinaryOperator("Oid", (e.DataItem as Asset)!.Oid));
        dataItem?.Delete();
        await LoadInformation();
    }

    private async Task OnPrintBtnClick()
    {
        //await ToastService.Information("دریافت گزارش", "سیستم در حال ایجاد فایل است. لطفاً شکیبا باشید...");
        Snackbar.Add("سیستم در حال ایجاد فایل است. لطفاً تا دانلود گزارش شکیبا باشید...", Severity.Info);

        await AssetGrid.ExportToXlsxAsync("StopCards", new GridXlExportOptions
        {
            CustomizeSheet = SamcoSoftShared.CustomizeSheet,
            CustomizeCell = SamcoSoftShared.CustomizeCell,
            CustomizeSheetFooter = SamcoSoftShared.CustomizeFooter
        })!;
    }
    #endregion
}