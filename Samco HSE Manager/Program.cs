using Blazored.LocalStorage;
using DevExpress.Blazor.Reporting;
using DevExpress.DashboardAspNetCore;
using DevExpress.DashboardCommon;
using DevExpress.DashboardWeb;
using DevExpress.Xpo.DB;
using DevExpress.XtraReports.Web.Extensions;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using MudBlazor.Services;
using Samco_HSE.HSEData;
using Samco_HSE_Manager;
using Samco_HSE_Manager.Authentication;
using Samco_HSE_Manager.Models;
using Syncfusion.Blazor;
using Syncfusion.Blazor.Popups;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor().AddHubOptions(o => { o.MaximumReceiveMessageSize = 102400000; });
builder.Services.AddTransient<ITokenManager, TokenManager>(_ => new TokenManager(builder.Configuration["Jwt:SecretKey"]!,
    builder.Configuration["Jwt:Issuer"]!, builder.Configuration["Jwt:Audience"]!));

builder.Services.AddXpoDefaultDataLayer(ServiceLifetime.Singleton, dl => dl
    .UseConnectionString(builder.Environment.IsDevelopment()
        ? builder.Configuration.GetConnectionString("LocalDatabase")
        : builder.Configuration.GetConnectionString("MainDatabase"))
    .UseThreadSafeDataLayer(true)
    .UseAutoCreationOption(AutoCreateOption.DatabaseAndSchema) // Remove this line if the database already exists
    .UseCustomDictionaryFactory(SamcoSoftShared.GetDatabaseAssemblies));
builder.Services.AddXpoDefaultUnitOfWork();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthorization();
builder.Services.AddLocalization();
builder.Services.AddCors(options => options.AddPolicy("AllowOrigin", builders => builders.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopRight;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = false;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});
builder.Services.AddDevExpressBlazorReporting();
builder.Services.AddScoped(_ =>
{
    var configurator = new DashboardConfigurator();
    var fileProvider = builder.Environment.ContentRootFileProvider;
    configurator.SetDashboardStorage(
        new DashboardFileStorage(fileProvider.GetFileInfo("Data/Dashboards").PhysicalPath));

    var dataSourceStorage = new DataSourceInMemoryStorage();

    //var xpoDataSource = new DashboardXpoDataSource("XPO Data Source");
    //xpoDataSource.ConnectionStringName = builder.Environment.IsDevelopment()
    //    ? "LocalDatabase" : "MainDatabase";
    //xpoDataSource.SetEntityType(typeof(Rig));
    //dataSourceStorage.RegisterDataSource("xpoDataSource", xpoDataSource.SaveToXml());

    configurator.SetDataSourceStorage(dataSourceStorage);
    configurator.SetConnectionStringsProvider(new DashboardConnectionStringsProvider(builder.Configuration));
    return configurator;
});
builder.Services.AddScoped<SfDialogService>();
builder.Services.AddSyncfusionBlazor(options =>
{
    options.EnableRtl = true;
    options.Animation = GlobalAnimationMode.Enable;
    options.EnableRippleEffect = true;
});
SamcoSoftShared.Lic = SamcoSoftShared.CreateLicense(Path.Combine(builder.Environment.WebRootPath, "Samco_HSE.lic"));
builder.Services.AddScoped<ReportStorageWebExtension, CustomReportStorageWebExtension>();
builder.WebHost.UseWebRoot("wwwroot");
builder.WebHost.UseStaticWebAssets();

var app = builder.Build();

//Register Syncfusion license
//Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("MTMxM0AzMjM1MkUzMTJFMzlKK2QyalU0d1EyVUgxN0FFdUVENGdDYmY4UWEyZ2poeEhoSWlUcmFSd2JjPQ==");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();
app.UseRequestLocalization(new RequestLocalizationOptions()
    .AddSupportedCultures("en-US")
    .AddSupportedUICultures("en-US"));

app.UseDevExpressBlazorReporting();
app.MapDashboardRoute("api/dashboard", "DefaultDashboard");
app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

app.UseAuthentication();

app.UseAuthorization();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.MapControllers();

app.Run();
