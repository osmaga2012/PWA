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
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _jsRuntime.InvokeAsync<string?>(
            "localStorage.getItem",
            cancellationToken,
            "access_token");

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // 2. Adjuntar Cabecera de Tenant
        if (!request.Headers.Contains("X-Tenant-Id"))
        {
            Console.WriteLine($"Tenant: {_tenantId}");
            request.Headers.Add("X-Tenant-Id", _tenantId);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
