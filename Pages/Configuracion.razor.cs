using CRM.Dtos;
using CRM.Dtos.Enums;
using CRM.Dtos.Response;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using PWA.Interfaces;

using System.Net.Http.Json;

namespace PWA.Pages
{
    public partial class Configuracion : ComponentBase
    {
        [Inject] public IApiClient<ConfiguracionDto> servicioConfiguracion { get; set; } = default!;
        [Inject] public IHttpClientFactory HttpClientFactory { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] public ISnackbar Snackbar { get; set; } = default!;

        private ConfiguracionDto configuracion = new();
        private MudForm _form = default!;
        private bool _success;
        private string[] _errors = { };
        private int _intervaloEnvioCantidad = 1;
        private bool _suspendIntervalUpdate;
        private bool _isTestingEmail;

        private int IntervaloEnvioCantidad
        {
            get => _intervaloEnvioCantidad;
            set
            {
                var valorNormalizado = Math.Max(1, value);
                if (_intervaloEnvioCantidad == valorNormalizado)
                {
                    return;
                }

                _intervaloEnvioCantidad = valorNormalizado;
                ActualizarIntervaloEnMinutos();
            }
        }

        private IntervaloTiempoUnidadDto IntervaloEnvioUnidad
        {
            get => configuracion.IntervaloEnvioUnidad;
            set
            {
                if (configuracion.IntervaloEnvioUnidad == value)
                {
                    return;
                }

                configuracion.IntervaloEnvioUnidad = value;
                ActualizarIntervaloEnMinutos();
            }
        }

        protected override async Task OnInitializedAsync()
        {
            await CargarConfiguracion();
            StateHasChanged();
        }
        private async Task CargarConfiguracion()
        {
            var configuraciones = await servicioConfiguracion.GetAllAsync("api/Configuracion")
                ?? Enumerable.Empty<ConfiguracionDto>();
            configuracion = configuraciones.FirstOrDefault();

            if (configuracion == null)
            {
                configuracion = new();
            }

            InicializarIntervalo();
        }

        private async Task Guardar()
        {
            await _form.Validate();
            if (!_form.IsValid)
            {
                Snackbar.Add("Por favor, corrige los errores del formulario.", Severity.Warning);
                return;
            }

            try
            {
                ResponseDto response = new();
                if (configuracion.Id != Guid.Empty)
                {
                    
                    response = await servicioConfiguracion.UpdateAsync("api/Configuracion", configuracion, configuracion.Id);
                }
                else
                {
                    response = await servicioConfiguracion.CreateAsync("api/Configuracion", configuracion);
                }
                // Como solo hay una fila de configuración y la actualizamos, siempre es un UPDATE

                if (response != null) // Asume que UpdateAsync devuelve el DTO actualizado o null en caso de error
                {
                    Snackbar.Add("Configuración guardada correctamente.", Severity.Success);
                    configuracion = (ConfiguracionDto)response.Data; // Actualiza el modelo local con la respuesta de la API
                    InicializarIntervalo();
                }
                else
                {
                    Snackbar.Add($"Error al guardar la configuración. {response.Message}", Severity.Error);
                }
            }
            catch (HttpRequestException httpEx)
            {
                Snackbar.Add($"Error de la API al guardar: {httpEx.Message}", Severity.Error);
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Ocurrió un error inesperado al guardar la configuración: {ex.Message}", Severity.Error);
            }
        }

        private void InicializarIntervalo()
        {
            _suspendIntervalUpdate = true;
            try
            {
                var minutos = Math.Max(1, configuracion.IntervaloEnvioMinutos);
                var unidad = configuracion.IntervaloEnvioUnidad;

                _intervaloEnvioCantidad = ConvertirDesdeMinutos(minutos, unidad);
                if (_intervaloEnvioCantidad < 1)
                {
                    _intervaloEnvioCantidad = 1;
                }

                configuracion.IntervaloEnvioMinutos = ConvertirAMinutos(_intervaloEnvioCantidad, unidad);
            }
            finally
            {
                _suspendIntervalUpdate = false;
            }

            ActualizarIntervaloEnMinutos();
        }

        private void ActualizarIntervaloEnMinutos()
        {
            if (_suspendIntervalUpdate)
            {
                return;
            }

            configuracion.IntervaloEnvioMinutos = ConvertirAMinutos(_intervaloEnvioCantidad, configuracion.IntervaloEnvioUnidad);
        }

        private static int ConvertirAMinutos(int cantidad, IntervaloTiempoUnidadDto unidad)
        {
            var valor = Math.Max(1, cantidad);

            return unidad switch
            {
                IntervaloTiempoUnidadDto.Horas => valor * 60,
                IntervaloTiempoUnidadDto.Dias => valor * 60 * 24,
                IntervaloTiempoUnidadDto.Semanas => valor * 60 * 24 * 7,
                _ => valor
            };
        }

        private static int ConvertirDesdeMinutos(int minutos, IntervaloTiempoUnidadDto unidad)
        {
            var valor = Math.Max(1, minutos);

            return unidad switch
            {
                IntervaloTiempoUnidadDto.Horas => Math.Max(1, valor / 60),
                IntervaloTiempoUnidadDto.Dias => Math.Max(1, valor / (60 * 24)),
                IntervaloTiempoUnidadDto.Semanas => Math.Max(1, valor / (60 * 24 * 7)),
                _ => valor
            };
        }

        private static string ObtenerAbreviaturaUnidad(IntervaloTiempoUnidadDto unidad)
        {
            return unidad switch
            {
                IntervaloTiempoUnidadDto.Horas => "h",
                IntervaloTiempoUnidadDto.Dias => "d",
                IntervaloTiempoUnidadDto.Semanas => "sem",
                _ => "min"
            };
        }

        private async Task CopyToClipboard(string text)
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
            Snackbar.Add($"'{text}' copiado al portapapeles.", Severity.Info);
        }

        private async Task ProbarCorreo()
        {
            await _form.Validate();
            if (!_form.IsValid)
            {
                Snackbar.Add("Por favor, corrige los errores del formulario antes de enviar la prueba.", Severity.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(configuracion.EmailEnvio))
            {
                Snackbar.Add("Debes indicar un email de envío para realizar la prueba.", Severity.Warning);
                return;
            }

            try
            {
                _isTestingEmail = true;
                var client = HttpClientFactory.CreateClient("ApiClient");
                var response = await client.PostAsJsonAsync("api/Configuracion/test-email", configuracion);
                var resultado = await response.Content.ReadFromJsonAsync<ResponseDto>();

                if (response.IsSuccessStatusCode && resultado?.Success == true)
                {
                    var mensaje = string.IsNullOrWhiteSpace(resultado.Message)
                        ? "Correo de prueba enviado correctamente."
                        : resultado.Message;
                    Snackbar.Add(mensaje, Severity.Success);
                    return;
                }

                var mensajeError = resultado?.Message;
                if (string.IsNullOrWhiteSpace(mensajeError))
                {
                    mensajeError = response.IsSuccessStatusCode
                        ? "No se pudo confirmar el envío del correo de prueba."
                        : $"Error al enviar el correo de prueba: {response.ReasonPhrase}";
                }

                Snackbar.Add(mensajeError, Severity.Error);
            }
            catch (HttpRequestException httpEx)
            {
                Snackbar.Add($"Error de comunicación al enviar la prueba: {httpEx.Message}", Severity.Error);
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Error al enviar el correo de prueba: {ex.Message}", Severity.Error);
            }
            finally
            {
                _isTestingEmail = false;
            }
        }
    }
}
