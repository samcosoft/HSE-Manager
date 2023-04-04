using BootstrapBlazor.Components;
using Microsoft.AspNetCore.Components;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Samco_HSE_Manager.Pages;

public partial class About
{
    [Inject][NotNull] private ToastService? ToastService { get; set; }

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
    private async Task Activate()
    {
        //Validation
        if (string.IsNullOrEmpty(_activationKey))
        {
            await ToastService.Error("خطا در ثبت مجوز", "لطفاً تمام اطلاعات خواسته شده را وارد کنید.");
            return;
        }

        //await ToastService.Show(new ToastOption()
        //{
        //    Animation = true,
        //    Category = ToastCategory.Information,
        //    Delay = 3000,
        //    Title = "دریافت مجوز",
        //    Content = "در حال دریافت مجوز از سرور. لطفاً شکیبا باشید..."
        //});

        try
        {
            var ret = SamcoSoftShared.ActivateLicense(_activationKey, out _returnMsg);
            if (ret)
            {
                await ToastService.Success("ثبت مجوز", _returnMsg);
            }
            else
            {
                await ToastService.Error("خطا در ثبت مجوز", "اطلاعات وارد شده صحیح نیست.");
            }

        }
        catch (Exception e)
        {
            await ToastService.Error("خطا در ثبت مجوز", "در سیستم فعالسازی مشکلی رخ داده است. لطفاً بعد از مدتی دوباره تلاش کنید.");
            await ToastService.Error("کد خطا", e.Message);
        }
        LoadLicenseInfo();
    }
    private void Deactivate()
    {
        ToastService.Show(new ToastOption
        {
            Animation = true,
            Category = ToastCategory.Information,
            Delay = 3000,
            Title = "لغو مجوز",
            Content = "در حال غیر فعالسازی مجوز. لطفاً شکیبا باشید..."
        });

        SamcoSoftShared.Lic.Deactivate();
        LoadLicenseInfo();
    }

    #endregion
}