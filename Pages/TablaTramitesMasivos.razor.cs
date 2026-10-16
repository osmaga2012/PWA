using CRM.Dtos;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using PWA.Interfaces;

namespace PWA.Pages
{
    public partial class TablaTramitesMasivos : ComponentBase
    {
        [Parameter] public List<TramiteDto> Tramites { get; set; } = new();
        [Parameter] public bool IsLoading { get; set; }
        [Parameter] public EventCallback OnNuevoTramite { get; set; }
        [Parameter] public EventCallback<TramiteDto> OnTramiteSeleccionado { get; set; }
        [Parameter] public EventCallback OnTramitesChanged { get; set; }

        [Inject] private ISnackbar Snackbar { get; set; } = default!;
        [Inject] private IApiClient<TramiteDto> tramiteService { get; set; } = default!;
        [Inject] private IDialogService DialogService { get; set; } = default!;

        private string SearchString = string.Empty;

        private IEnumerable<TramiteDto> FilteredTramites => string.IsNullOrWhiteSpace(SearchString)
            ? Tramites
            : Tramites.Where(t =>
                Contains(t.Codigo.ToString()) ||
                Contains(t.NombreTramite) ||
                Contains(t.AsignacionDestino));

        private async Task CrearNuevo()
        {
            await OnNuevoTramite.InvokeAsync();
        }

        private async Task OnItemChanged(TramiteDto tramite)
        {
            if (tramite == null)
            {
                return;
            }

            await OnTramiteSeleccionado.InvokeAsync(tramite);
        }

        private bool Contains(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.Contains(SearchString, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatDate(DateTime? value)
        {
            return value?.ToString("dd/MM/yyyy") ?? "-";
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

        private async Task DeleteTramite(Guid id, string nombreTramite)
        {
            var dialogResult = await DialogService.ShowMessageBoxAsync(
                "Confirmar eliminación",
                $"¿Estás seguro de que quieres eliminar el trámite {nombreTramite}? Esta acción no se puede deshacer.",
                yesText: "Eliminar",
                cancelText: "Cancelar");

            if (dialogResult == true)
            {
                await Borrar(id);
            }
        }

        private async Task Borrar(Guid id)
        {
            try
            {
                var respuesta = await tramiteService.DeleteAsync("api/Tramites", id);

                if (respuesta.Success)
                {
                    Snackbar.Add("Trámite eliminado correctamente.", Severity.Success);
                    await OnTramitesChanged.InvokeAsync();
                }
                else
                {
                    Snackbar.Add(respuesta.Message ?? "No se pudo eliminar el trámite.", Severity.Error);
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Error al eliminar el trámite: {ex.Message}", Severity.Error);
            }
        }
    }
}
