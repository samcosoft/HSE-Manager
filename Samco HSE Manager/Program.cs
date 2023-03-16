using Blazored.LocalStorage;
using DevExpress.Blazor;
using DevExpress.Blazor.Reporting;
using DevExpress.DashboardAspNetCore;
using DevExpress.DashboardWeb;
using DevExpress.Xpo.DB;
using DevExpress.XtraReports.Web.Extensions;
using Microsoft.AspNetCore.Components.Authorization;
using Samco_HSE_Manager.Authentication;
using Samco_HSE_Manager.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddTransient<ITokenManager, TokenManager>(_ => new TokenManager(builder.Configuration["Jwt:SecretKey"]!,
    builder.Configuration["Jwt:Issuer"]!, builder.Configuration["Jwt:Audience"]!));

builder.Services.AddXpoDefaultDataLayer(ServiceLifetime.Singleton, dl => dl
    .UseConnectionString(builder.Environment.IsDevelopment()
        ? builder.Configuration.GetConnectionString("LocalDatabase")
        : builder.Configuration.GetConnectionString("MainDatabase"))
    .UseThreadSafeDataLayer(true)
    .UseAutoCreationOption(AutoCreateOption.DatabaseAndSchema) // Remove this line if the database already exists
    .UseCustomDictionaryFactory(Samco_HSE_Manager.SamcoSoftShared.GetDatabaseAssemblies));

builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthorization();
builder.Services.AddLocalization();
builder.Services.AddCors(options => options.AddPolicy("AllowOrigin", builders => builders.AllowAnyOrigin()));
builder.Services.AddBootstrapBlazor();
builder.Services.AddDevExpressBlazorReporting();
builder.Services.AddDevExpressBlazor(configure => configure.BootstrapVersion = BootstrapVersion.v5);
builder.Services.AddScoped(_ =>
{
    var configurator = new DashboardConfigurator();
    var fileProvider = builder.Environment.ContentRootFileProvider;
    configurator.SetDashboardStorage(
        new DashboardFileStorage(fileProvider.GetFileInfo("Data/Dashboards").PhysicalPath));
    var dataSourceStorage = new DataSourceInMemoryStorage();
    configurator.SetDataSourceStorage(dataSourceStorage);
    configurator.SetConnectionStringsProvider(new DashboardConnectionStringsProvider(builder.Configuration));
    return configurator;
});

builder.Services.AddScoped<ReportStorageWebExtension, CustomReportStorageWebExtension>();
builder.WebHost.UseWebRoot("wwwroot");
builder.WebHost.UseStaticWebAssets();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseDevExpressBlazorReporting();

app.UseRequestLocalization(new RequestLocalizationOptions()
    .AddSupportedCultures("en-US")
    .AddSupportedUICultures("en-US"));

app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

app.UseAuthentication();

app.UseRouting();
app.MapDashboardRoute("api/dashboard", "DefaultDashboard");

app.UseAuthorization();
//TODO: Change this line on deployment
//app.UsePathBase("/hse");
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

#pragma warning disable ASP0014 // Suggest using top level route registrations
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});
#pragma warning restore ASP0014 // Suggest using top level route registrations

app.Run();
