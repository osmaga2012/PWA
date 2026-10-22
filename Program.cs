using CRM.App.Sharted.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;
using PWA;
using PWA.Branding;
using PWA.Helpers;
using PWA.Interfaces;
using PWA.Providers;
using PWA.Servicios;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configuración pública de la aplicación y de la marca del tenant.
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"];
var timeout = builder.Configuration.GetValue<int>("ApiSettings:Timeout", 30);
var appName = builder.Configuration["ApplicationName"] ?? "CRM PWA";
var appVersion = builder.Configuration["Version"] ?? "1.0.0";
var tenantId = builder.Configuration["TenantID"]?.Trim();

if (string.IsNullOrWhiteSpace(tenantId))
{
    throw new InvalidOperationException("TenantID no está configurado en wwwroot/appsettings.json.");
}

if (string.IsNullOrWhiteSpace(apiBaseUrl))
{
    if (builder.HostEnvironment.IsProduction())
    {
        throw new InvalidOperationException("ApiSettings:BaseUrl no está configurada en producción.");
    }

    apiBaseUrl = "https://localhost:7254";
}

var branding = new BrandingOptions();
builder.Configuration.GetSection("Branding").Bind(branding);
builder.Services.AddSingleton(branding);

Console.WriteLine($"🌍 Entorno: {builder.HostEnvironment.Environment}");
Console.WriteLine($"📦 Aplicación: {appName}");
Console.WriteLine($"🔢 Versión: {appVersion}");
Console.WriteLine($"🏢 Tenant: {tenantId}");

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 5000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});

builder.Services.AddTransient<AuthorizedHttpClientHandler>();

builder.Services.AddHttpClient("ApiClient", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl!);
    client.Timeout = TimeSpan.FromSeconds(timeout);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

    if (!string.IsNullOrWhiteSpace(appVersion))
    {
        client.DefaultRequestHeaders.Add("User-Agent", $"CRM-PWA/{appVersion}");
    }
})
.AddHttpMessageHandler<AuthorizedHttpClientHandler>();

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("ApiClient"));

builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider =>
    provider.GetRequiredService<CustomAuthStateProvider>());
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("EsEmpresa", policy => policy.RequireRole("Empresa"));
    options.AddPolicy("EsAdmin", policy => policy.RequireRole("Administrador"));
});

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<UserSessionState>();
builder.Services.AddScoped(typeof(IApiClient<>), typeof(ApiClient<>));

await builder.Build().RunAsync();
