using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;
using Samco_HSE.HSEData;
using System.Reflection;

namespace Samco_HSE_Manager.Shared;

public partial class MainLayout : IDisposable
{
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;
    [Inject] private IHostEnvironment HostEnvironment { get; set; } = null!;
    [Inject] private IConfiguration Configuration { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private IDataLayer DataLayer { get; set; } = null!;

    private bool IsSidebarExpanded { get; set; } = true;

    private string DisplayProfile => "align-self: center; min-height: 170px; display:" + (IsSidebarExpanded ? "block" : "none");

    private bool _isValid;
    protected override async Task OnInitializedAsync()
    {
        var session1 = new Session(DataLayer);
        //SamcoSoftShared.CurrentUser = new User(session1);
        try
        {
            var authStat = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            if (authStat.User.Identity == null) return;

            var loggedUser = await session1.FindObjectAsync<User>(new BinaryOperator("Username",
                authStat.User.Identity?.Name));
            if (loggedUser != null)
            {
                SamcoSoftShared.CurrentUser = loggedUser;
                SamcoSoftShared.CurrentUserId = loggedUser.Oid;
                SamcoSoftShared.CurrentUserRole = Enum.Parse<SamcoSoftShared.SiteRoles>(SamcoSoftShared.CurrentUser.SiteRole);
            }
            else
            {
                return;
            }

            //Set Theme
            _isDarkMode = await _mudThemeProvider.GetSystemPreference();
            SetDevexpressTheme();

            _location = SamcoSoftShared.CurrentUser?.ActiveRig?.Name;
            // _avatarSrc = Path.Combine(HostEnvironment.WebRootPath, "upload", "profile", $"{SamcoSoftShared.CurrentUser?.Oid.ToString() ?? string.Empty}.png");
            _avatarSrc = SamcoSoftShared.CurrentUser?.PersonnelName?[..1];
            _userName = SamcoSoftShared.CurrentUser?.PersonnelName ?? "مهمان";
            _userRole =SamcoSoftShared.GetPersianRoleName(SamcoSoftShared.CurrentUserRole);
        }
        catch (Exception)
        {
            //_loggedUser = new User(session1);
        }
        NavigationManager.LocationChanged += OnLocationChanged;

        //License validation
        _isValid = SamcoSoftShared.CheckLicense(out _, HostEnvironment.IsDevelopment());

        //Routing for personnel

        if (SamcoSoftShared.CurrentUserRole == SamcoSoftShared.SiteRoles.Personnel)
        {
            NavigationManager.NavigateTo("personnelHome");
        }
    }

    #region Theme

    private bool _isDarkMode;
    private readonly List<string> _devexpressTheme = new();
    private MudThemeProvider _mudThemeProvider = null!;

    private readonly MudTheme _samcoTheme = new()
    {
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = new[] { "IranSansX", "Tahoma", "Arial" } },
            H1 = new H1Typography { FontFamily = new[] { "Dana", "Tahoma", "Arial" }, FontWeight = "600" },
            H2 = new H2Typography { FontFamily = new[] { "Dana", "Tahoma", "Arial" }, FontWeight = "600" },
            H3 = new H3Typography { FontFamily = new[] { "Dana", "Tahoma", "Arial" }, FontWeight = "600" },
            H4 = new H4Typography { FontFamily = new[] { "Dana", "Tahoma", "Arial" }, FontWeight = "600" },
            H5 = new H5Typography { FontFamily = new[] { "Dana", "Tahoma", "Arial" }, FontWeight = "600" },
            H6 = new H6Typography { FontFamily = new[] { "Dana", "Tahoma", "Arial" }, FontWeight = "600" }
        }
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await _mudThemeProvider.WatchSystemPreference(OnSystemPreferenceChanged);
            SetDevexpressTheme();
            StateHasChanged();
        }
    }

    private Task OnSystemPreferenceChanged(bool newValue)
    {
        _isDarkMode = newValue;
        //Set devexpress theme
        SetDevexpressTheme();
        StateHasChanged();
        return Task.CompletedTask;
    }
    private void SetTheme(bool isDarkMode)
    {
        _isDarkMode = isDarkMode;
        SetDevexpressTheme();
    }

    private void SetDevexpressTheme()
    {
        if (!_isDarkMode)
        {
            _devexpressTheme.Add("_content/DevExpress.Blazor.Dashboard/dx.light.css");
            _devexpressTheme.Add("_content/DevExpress.Blazor.Dashboard/dx-analytics.light.css");
            _devexpressTheme.Add("_content/DevExpress.Blazor.Dashboard/dx-dashboard.light.min.css");
            _devexpressTheme.Add("css/tailwind.css");
        }
        else
        {
            _devexpressTheme.Add("_content/DevExpress.Blazor.Dashboard/dx.dark.css");
            _devexpressTheme.Add("_content/DevExpress.Blazor.Dashboard/dx-analytics.dark.css");
            _devexpressTheme.Add("_content/DevExpress.Blazor.Dashboard/dx-dashboard.dark.min.css");
            _devexpressTheme.Add("css/tailwind-dark.css");
        }
    }
    #endregion

    #region Information

    private string? _avatarSrc;
    private string? _userName;
    private string? _userRole;
    private string? _location;

    #endregion

    private string? GetApplicationVersion()
    {
        return Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
    }

    void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        //TODO: Reroute for activation
        if (_isValid || args.Location.Contains("about") || args.Location.Contains("expired")) return;
        if (!_isValid && HostEnvironment.IsDevelopment()) return;
        if (SamcoSoftShared.CurrentUser == null) return;
        NavigationManager.NavigateTo(SamcoSoftShared.CurrentUserRole == SamcoSoftShared.SiteRoles.Owner
            ? "about"
            : "expired");
    }

    public void Dispose()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
    }
}