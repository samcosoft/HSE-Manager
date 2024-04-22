using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Samco_HSE.HSEData;
using Syncfusion.Blazor.Grids;
using Action = Syncfusion.Blazor.Grids.Action;

namespace Samco_HSE_Manager.Pages.Officer;

public partial class Assets : IDisposable
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;

    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private Session Session1 { get; set; } = null!;

    //private DxGrid AssetGrid { get; set; } = null!;
    private SfGrid<Asset> AssetGrid { get; set; } = null!;
    private IEnumerable<Asset>? AssetsList { get; set; }
    private IEnumerable<Rig> Rigs { get; set; } = null!;

    private readonly IEnumerable<string> _owners = new List<string>
    {
        "واحد ایمنی", "درمانگاه"
    };

    private readonly IEnumerable<string> _status = new List<string>
    {
        "سالم", "نیاز به تعمیر", "خارج از سرویس"
    };

    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
        if (SamcoSoftShared.CurrentUserRole != SamcoSoftShared.SiteRoles.Owner)
        {
            var loggedUser = await
                Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
            Rigs = loggedUser.Rigs;
            var rigsOid = Rigs.Select(x => x.Oid).ToList();
            AssetsList = await Session1.Query<Asset>()
                .Where(x => rigsOid.Contains(x.Oid)).ToListAsync();
        }
        else
        {
            //Owner
            Rigs = await Session1.Query<Rig>().ToListAsync();
            AssetsList = await Session1.Query<Asset>().ToListAsync();
        }
    }

    public void Dispose()
    {
        Session1.Dispose();
    }

    #region AssetGrid

    private void Customize_Row(RowDataBoundEventArgs<Asset> args)
    {
        if (args.Data.ExpireDate <= DateTime.Today)
        {
            args.Row.AddClass(new[] { "expired-item" });
        }

        if (args.Data.ExpireDate > DateTime.Today && args.Data.ExpireDate < DateTime.Today.AddMonths(2))
        {
            args.Row.AddClass(new[] { "warning-item" });
        }
    }

    private async Task AssetGrid_Action(ActionEventArgs<Asset> e)
    {
        switch (e.RequestType)
        {
            case Action.BeforeBeginEdit:
                
            case Action.Add:
                e.Data = new Asset(Session1);
                break;
            case Action.BeginEdit:
                e.Data = await Session1.FindObjectAsync<Asset>(new BinaryOperator("Oid", e.RowData.Oid));
                break;
            case Action.Save:
                var editModel = e.Data;
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
                if (editModel.Oid < 0)
                {
                    if (selEquip != null && string.IsNullOrEmpty(selEquip.Serial) &&
                        selEquip.Serial == editModel.Serial)
                    {
                        //Equipment existed
                        Snackbar.Add(
                            "تجهیز با همین مشخصات در سیستم وجود دارد. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.",
                            Severity.Error);
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
                        Snackbar.Add(
                            "تجهیز با همین مشخصات در سیستم وجود دارد. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.",
                            Severity.Error);
                        e.Cancel = true;
                        return;
                    }
                }

                editModel.Save();
                break;
            case Action.Delete:
                e.RowData.Delete();
                break;
        }
    }

    private async Task ToolbarClickHandler(Syncfusion.Blazor.Navigations.ClickEventArgs args)
    {
        if (args.Item.Id == "assetGrid_Excel Export")
        {
            Snackbar.Add("سیستم در حال ایجاد فایل است. لطفاً تا دانلود گزارش شکیبا باشید...", Severity.Info);
            var exportProperties = new ExcelExportProperties
            {
                FileName = "AssetList.xlsx"
            };
            await AssetGrid.ExportToExcelAsync(exportProperties);
        }
    }

    #endregion
}