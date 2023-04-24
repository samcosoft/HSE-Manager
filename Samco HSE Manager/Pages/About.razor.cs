using Microsoft.AspNetCore.Components;
using System.Reflection;
using MudBlazor;

namespace Samco_HSE_Manager.Pages;

public partial class About
{
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private string? GetApplicationVersion()
    {
        return Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
    }

    private string GetReleaseNote()
    {
        var path = Path.Combine(HostEnvironment.ContentRootPath, "ReleaseNote.txt");
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    #region Activation

    private string? _licenseInfo;
    private string? _activationKey;
    private bool _isValid;
    private bool _isTrial;
    private string _returnMsg = string.Empty;

    protected override void OnInitialized()
    {
        LoadLicenseInfo();
    }

    private void LoadLicenseInfo()
    {
        _isValid = SamcoSoftShared.CheckLicense(out var status);
        _isTrial = SamcoSoftShared.Lic.IsEvaluationLicense();
        _licenseInfo = string.Join(Environment.NewLine, "Serial: " + SamcoSoftShared.Lic.GetUserDataFieldValue("<serial>", "#"),
            "Organization: " + SamcoSoftShared.Lic.GetUserDataFieldValue("Company", "#"),
            "Phone: " + SamcoSoftShared.Lic.GetUserDataFieldValue("Phone", "#"));

        if (_isValid)
        {
            if (_isTrial)
            {
                _returnMsg = $"مجوز شما تا {SamcoSoftShared.Lic.RemainingUsageDays} روز دیگر معتبر است.";
            }
        }
        else
        {
            _returnMsg = SamcoSoftShared.LicenseStatusMessage(status);
        }
    }
    private void Activate()
    {
        //Validation
        if (string.IsNullOrEmpty(_activationKey))
        {
            Snackbar.Add("لطفاً تمام اطلاعات خواسته شده را وارد کنید.", Severity.Error);
            return;
        }

        try
        {
            var ret = SamcoSoftShared.ActivateLicense(_activationKey, out _returnMsg);
            if (ret)
            {
                Snackbar.Add(_returnMsg, Severity.Success);
            }
            else
            {
                Snackbar.Add("اطلاعات وارد شده صحیح نیست.", Severity.Error);
            }

        }
        catch (Exception e)
        {
            Snackbar.Add("در سیستم فعالسازی مشکلی رخ داده است. لطفاً بعد از مدتی دوباره تلاش کنید.", Severity.Error);
            Snackbar.Add(e.Message, Severity.Error);
        }
        LoadLicenseInfo();
    }
    private void Deactivate()
    {
        Snackbar.Add("در حال غیر فعالسازی مجوز. لطفاً شکیبا باشید...", Severity.Info);
        SamcoSoftShared.Lic.Deactivate();
        LoadLicenseInfo();
    }

    #endregion
}