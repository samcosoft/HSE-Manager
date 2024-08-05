using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Samco_HSE.HSEData;
using Samco_HSE_Manager.Pages.Admin.FormModals;
using Syncfusion.Blazor.Grids;
using Syncfusion.Blazor.Popups;
using Action = Syncfusion.Blazor.Grids.Action;

namespace Samco_HSE_Manager.Pages.Admin;

public partial class FormDesign
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private SfDialogService DialogService { get; set; } = null!;
    [Inject] private IDialogService MudDialogService { get; set; } = null!;

    private Session Session1 { get; set; } = null!;
    private XPCollection<HSEForm>? HseForms { get; set; }
    private IEnumerable<string>? RigRoles { get; set; }
    private IEnumerable<string>? Keywords { get; set; }

    private string[]? _accessGroup, _formKeywords;

    protected override void OnInitialized()
    {
        Session1 = new Session(DataLayer);
        RigRoles = SamcoSoftShared.Roles.Values;
        HseForms = new XPCollection<HSEForm>(Session1);

        var keywords = HseForms.Select(x => x.Keywords).ToList();
        if (keywords.Any())
        {
            Keywords = keywords.Where(x => x != null).SelectMany(x => x.Split("|")).Distinct().ToList();
        }
        base.OnInitialized();
    }

    private SfGrid<HSEForm>? FormGrid { get; set; }


    private async Task FormGrid_Action(ActionEventArgs<HSEForm> e)
    {
        switch (e.RequestType)
        {
            case Action.Add:
                _accessGroup = null;
                e.Data = new HSEForm(Session1) { RevDate = DateTime.Today };
                break;
            case Action.BeginEdit:
                _accessGroup = null;
                e.Data = await Session1.GetObjectByKeyAsync<HSEForm>(e.RowData.Oid);
                if (e.Data.AccessGroup != null) _accessGroup = e.Data.AccessGroup.Split("|");
                break;
            case Action.Save:
                {
                    var editModel = e.Data;
                    //Validation
                    if (string.IsNullOrEmpty(editModel.Title) || string.IsNullOrEmpty(editModel.Code) ||
                        editModel.Revision == 0 || _accessGroup == null)
                    {
                        Snackbar.Add("لطفاً موارد الزامی را تکمیل کنید.", Severity.Error);
                        e.Cancel = true;
                        return;
                    }

                    //check for previous code
                    var prevForm = Session1.FindObject<HSEForm>(new BinaryOperator(nameof(HSEForm.Code), editModel.Code));
                    if (prevForm != null && prevForm.Revision == editModel.Revision && (editModel.Oid < 0 || prevForm.Oid != editModel.Oid))
                    {
                        Snackbar.Add(
                            "یک فرم با همین کد و شماره نسخه در سیستم ثبت شده است. لطفاً اطلاعات را بررسی کرده و دوباره تلاش کنید.",
                            Severity.Error);
                        e.Cancel = true;
                        return;
                    }

                    if (_accessGroup != null) editModel.AccessGroup = string.Join('|', _accessGroup);
                    editModel.Save();
                    break;
                }
            case Action.Delete:
                {
                    var dataItem = await Session1.GetObjectByKeyAsync<HSEForm>(e.RowData.Oid);
                    var isConfirm = await DialogService.ConfirmAsync($"آیا از حذف فرم {dataItem.Code} مطمئنید؟", "حذف فرم");
                    if (!isConfirm)
                    {
                        e.Cancel = true;
                        return;
                    }
                    dataItem.Delete();
                    break;
                }
        }
    }

    private async Task FormToolbarClickHandler(Syncfusion.Blazor.Navigations.ClickEventArgs args)
    {
        if (args.Item.Id == "formGrid_Excel Export")
        {
            Snackbar.Add("سیستم در حال ایجاد فایل است. لطفاً تا دانلود گزارش شکیبا باشید...", Severity.Info);
            var exportProperties = new ExcelExportProperties
            {
                FileName = "FormList.xlsx"
            };
            await FormGrid!.ExportToExcelAsync(exportProperties);
        }
    }

    private async Task AttachFile()
    {
        if (FormGrid!.SelectedRecords.Count == 0)
        {
            Snackbar.Add("لطفاً یک فرم را از لیست زیر انتخاب کنید.", Severity.Warning);
            return;
        }
        await MudDialogService.ShowAsync<FormFileModal>("افزودن فایل فرم",
            new DialogParameters { { "SelFormId", FormGrid.SelectedRecords.First().Oid } });
    }
}