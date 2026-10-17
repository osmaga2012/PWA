using CRM.App.Sharted.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;
using PWA;
using PWA.Helpers;
using PWA.Interfaces;
using PWA.Providers;
using PWA.Servicios;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ============================================
// CONFIGURACIÓN
// ============================================
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"];
var timeout = builder.Configuration.GetValue<int>("ApiSettings:Timeout", 30);
var appName = builder.Configuration["ApplicationName"] ?? "CRM PWA";
var appVersion = builder.Configuration["Version"] ?? "1.0.0";

if (string.IsNullOrWhiteSpace(apiBaseUrl))
{
    if (builder.HostEnvironment.IsProduction())
    {
        throw new InvalidOperationException(
            "ApiSettings:BaseUrl no está configurada en producción."
        );
    }

    apiBaseUrl = "https://localhost:7254";
}


//// Fallback si no está configurado
//if (string.IsNullOrEmpty(apiBaseUrl))
//{
//    apiBaseUrl = builder.HostEnvironment.BaseAddress;
//    Console.WriteLine($"⚠️ ApiBaseUrl no configurado, usando BaseAddress: {apiBaseUrl}");
//}
//else
//{
//    Console.WriteLine($"✅ Usando ApiBaseUrl configurado: {apiBaseUrl}");
//}

// Log del ambiente
Console.WriteLine($"🌍 Entorno: {builder.HostEnvironment.Environment}");
Console.WriteLine($"📦 Aplicación: {appName}");
Console.WriteLine($"🔢 Versión: {appVersion}");

// ============================================
// CONFIGURACIÓN DE MudBlazor
// ============================================
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

// ============================================
// CONFIGURACIÓN DE HttpClient
// ============================================
builder.Services.AddScoped<AuthorizedHttpClientHandler>();

builder.Services.AddHttpClient("ApiClient", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl!);
    client.Timeout = TimeSpan.FromSeconds(timeout);
    client.DefaultRequestHeaders.Add("Accept", "application/json");

    if (!string.IsNullOrWhiteSpace(appVersion))
    {
        var userAgent = $"CRM-PWA/{appVersion}";
        client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        Console.WriteLine($"🌐 User-Agent: {userAgent}");
    }
})
.AddHttpMessageHandler<AuthorizedHttpClientHandler>();

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("ApiClient"));

// ============================================
// CONFIGURACIÓN DE AUTENTICACIÓN
// ============================================
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider =>
    provider.GetRequiredService<CustomAuthStateProvider>());
//builder.Services.AddAuthorizationCore();
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("EsEmpresa", policy => policy.RequireRole("Empresa"));
    options.AddPolicy("EsAdmin", policy => policy.RequireRole("Administrador"));
});

// ============================================
// SERVICIOS DE LA APLICACIÓN
// ============================================
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<UserSessionState>();

builder.Services.AddScoped(typeof(IApiClient<>), typeof(ApiClient<>));

await builder.Build().RunAsync();
