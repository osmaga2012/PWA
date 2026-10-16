using CRM.Dtos;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Net.Http.Headers;

namespace PWA.Pages
{
    public partial class TablaTramites: ComponentBase
    {
        [Parameter] public List<BarcosTramitesDto> Tramites { get; set; } = new();
        [Parameter] public EventCallback<BarcosTramitesDto> OnTramiteSeleccionado { get; set; }
        [Parameter] public BarcosDto Barco { get; set; } 

        private async Task OnItemChanged(BarcosTramitesDto tramite)
        {
            await OnTramiteSeleccionado.InvokeAsync(tramite);
        }

        private Color GetColorEstado(int? estado) => estado switch
        {
            0 => Color.Info,
            1 => Color.Warning,
            2 => Color.Success,
            _ => Color.Default
        };

        private static string GetTextoEstado(int? estado) => estado switch
        {
            0 => "Pendiente",
            1 => "Aviso",
            2 => "Correcto",
            _ => "Sin estado"
        };

        private static string FormatDate(DateTime? value)
        {
            return value?.ToString("dd/MM/yyyy") ?? "-";
        }

        private static string GetNombreCertificado(BarcosTramitesDto tramite)
        {
            return string.IsNullOrWhiteSpace(tramite.Certificado)
                ? "Sin certificado"
                : tramite.Certificado;
        }


        // Asegúrate de definir el evento o método que dispara la creación
        private async Task AbrirNuevoTramite()
        {
            // Si estás en TramitesBarco.razor, llama a tu método de alta
            // o dispara un evento que el padre pueda escuchar.
            var tramite = new BarcosTramitesDto();

            tramite.Id = Guid.Empty; // Nuevo trámite, sin ID aún
            tramite.CodigoBarco = Barco.CodigoBarco;
            tramite.CodigoEmpresa = string.IsNullOrEmpty(Barco.CodigoEmpresa) ? "" : Barco.CodigoEmpresa;    
            await OnTramiteSeleccionado.InvokeAsync(tramite);
        }
    }
}
