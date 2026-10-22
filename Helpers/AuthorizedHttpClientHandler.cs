using System.Net.Http.Headers;
using Microsoft.JSInterop;

namespace PWA.Helpers;

public sealed class AuthorizedHttpClientHandler : DelegatingHandler
{
    private readonly IJSRuntime _jsRuntime;
    private readonly string _tenantId;

    //public AuthorizedHttpClientHandler(IJSRuntime jsRuntime)
    //{
    //    _jsRuntime = jsRuntime;
    //}

    public AuthorizedHttpClientHandler(IJSRuntime jsRuntime, IConfiguration configuration)
    {
        _jsRuntime = jsRuntime;
        // Obtenemos el TenantID de la configuración (o valor por defecto)
        _tenantId = configuration["TenantID"] ?? "NOTENANT";

        Console.WriteLine("[HANDLER] Constructor ejecutado");
    }

    protected override async Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken)
{
    Console.WriteLine(
        $"[HTTP Handler] {request.Method} {request.RequestUri}");

    Console.WriteLine($"[HTTP Handler] TenantID configurado: {_tenantId}");

    var token = await _jsRuntime.InvokeAsync<string?>(
        "localStorage.getItem",
        cancellationToken,
        "access_token");

    if (!string.IsNullOrWhiteSpace(token))
    {
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    request.Headers.Remove("X-Tenant-Id");
    request.Headers.TryAddWithoutValidation("X-Tenant-Id", _tenantId);

    Console.WriteLine(
        $"[HTTP Handler] X-Tenant-Id enviado: " +
        $"{request.Headers.GetValues("X-Tenant-Id").FirstOrDefault()}");

    return await base.SendAsync(request, cancellationToken);
}
}
