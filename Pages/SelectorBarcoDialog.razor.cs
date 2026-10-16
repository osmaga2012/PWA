using CRM.Dtos;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using PWA.Interfaces;
using System.ComponentModel;

namespace PWA.Pages
{
    public partial class SelectorBarcoDialog: ComponentBase
    {
        [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;
        [Inject] private IApiClient<BarcosDto> ServicioBarcos { get; set; } = default!;
        //[Inject] private IMudDialogInstance DialogService { get; set; } = default!;
        private BarcosDto? barcoSeleccionado;

        // Función de búsqueda para el Autocomplete
        private async Task<IEnumerable<BarcosDto>> BuscarBarcos(string value, CancellationToken ct)
        {
            // Si no hay texto, devolvemos todos o una lista vacía
            if (string.IsNullOrEmpty(value))
                return await ServicioBarcos.GetAllAsync("api/Barcos");

            // Filtramos llamando a tu API (ajusta el endpoint según tu backend)
            Dictionary<string, string> queryParams = new Dictionary<string, string>
            {
                { "nombreb", value }
            };
            return await ServicioBarcos.GetAllAsync($"api/Barcos", queryParams);
        }

        private void Cancel() => MudDialog.Cancel();

        private void Aceptar()
        {
            if (barcoSeleccionado != null)
            {
                // Devolvemos el objeto barco completo al dialog que lo abrió
                MudDialog.Close(DialogResult.Ok(barcoSeleccionado));
            }
        }
    }
}
