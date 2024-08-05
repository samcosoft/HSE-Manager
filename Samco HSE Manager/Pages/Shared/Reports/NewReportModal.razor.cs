using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Samco_HSE.HSEData;

namespace Samco_HSE_Manager.Pages.Shared.Reports;

public partial class NewReportModal
{
    [CascadingParameter] MudDialogInstance MudDialog { get; set; } = null!;
    public int SelFormId { get; set; }
    private Session Session1 { get; set; } = null!;
    private IEnumerable<HSEForm>? FormCollection { get; set; }
    private IEnumerable<Rig>? Rigs { get; set; }
    private Rig? _selRig;

    protected override async Task OnInitializedAsync()
    {
        Session1 = new Session(DataLayer);
        if (SamcoSoftShared.CurrentUserRole > SamcoSoftShared.SiteRoles.Admin)
        {
            var loggedUser =
                await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId));
            Rigs = loggedUser.Rigs;
            FormCollection = new XPCollection<HSEForm>(Session1,
                CriteriaOperator.Parse("Contains([AccessGroup], ?) And [Disabled] == ? ", loggedUser.SiteRole, false));
        }
        else
        {
            //Owner
            FormCollection = new XPCollection<HSEForm>(Session1);
            Rigs = await Session1.Query<Rig>().ToListAsync();
            FormCollection = new XPCollection<HSEForm>(Session1, new BinaryOperator("Disabled", false));
        }
    }

    private async void Submit()
    {
        //Validation
        if (_selRig == null)
        {
            Snackbar.Add("لطفاً یک محل را انتخاب کنید.", Severity.Error);
            return;
        }

        if (_selRig.WellWorks.FirstOrDefault(x => x.IsActive) == null)
        {
            Snackbar.Add("محل انتخاب شده در هیچ پروژه‌ای فعال نیست.", Severity.Error);
            return;
        }

        //Check for file
        var selForm = await Session1.GetObjectByKeyAsync<HSEForm>(SelFormId);
        if (selForm == null)
        {
            Snackbar.Add("فرم انتخاب شده یافت نشد.", Severity.Error);
            return;
        }
        var origPath = Path.Combine(HostEnvironment.ContentRootPath, "Data", "Forms", $"{selForm.Oid}.{selForm.FormType}");
        if (!File.Exists(origPath))
        {
            Snackbar.Add("فایل فرم یافت نشد.", Severity.Error);
            return;
        }

        var newReport = new Report(Session1)
        {
            Form = selForm,
            SubDate = DateTime.Now,
            UserName = await Session1.FindObjectAsync<User>(new BinaryOperator("Oid", SamcoSoftShared.CurrentUserId)),
            WorkID = _selRig.WellWorks.FirstOrDefault(x => x.IsActive)
        };
        newReport.Save();
        Snackbar.Add("اطلاعات با موفقیت ثبت شد.", Severity.Success);

        //Copy report file to user report
        var destPath = Path.Combine(HostEnvironment.WebRootPath, "upload", "UserReports", SamcoSoftShared.CurrentUserId.ToString());
        if (!Directory.Exists(destPath))
            Directory.CreateDirectory(destPath);
        destPath = Path.Combine(destPath, $"{newReport.Oid}.{selForm.FormType}");
        File.Copy(origPath, destPath);

        //Open report for editing
        switch (selForm.FormType.ToLower())
        {
            case "pdf":
                var parameter1 = new DialogParameters<PDFViewer>
                {
                    { x => x.DocumentPath, destPath },
                    { x => x.ReportId, newReport.Oid }
                };
                await DialogService.ShowAsync<PDFViewer>($"گزارش {newReport.Form.Title}", parameter1, new DialogOptions { FullScreen = true });
                break;
            case "doc":
            case "docx":
                var parameter2 = new DialogParameters<WordViewer>
                {
                    { x => x.DocumentPath, destPath },
                    { x => x.ReportId, newReport.Oid }
                };
                await DialogService.ShowAsync<PDFViewer>($"گزارش {newReport.Form.Title}", parameter2, new DialogOptions { FullScreen = true });
                break;
        }

        MudDialog.Close(DialogResult.Ok(true));
    }


    void Cancel() => MudDialog.Cancel();
}