using CRM.Dtos;
using CRM.Dtos.Enums;
using CRM.Dtos.Response;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using PWA.Interfaces;

using System.Net.Http.Json;
using System.Text.Json;

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
            var configuraciones =
               await servicioConfiguracion.GetAllAsync("api/Configuracion");

            configuracion = configuraciones?.FirstOrDefault()
                            ?? new ConfiguracionDto();

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

                    configuracion = ((JsonElement)response.Data).Deserialize<ConfiguracionDto>();
                                    //?? new ConfiguracionDto();

                    //configuracion = (ConfiguracionDto)response.Data; // Actualiza el modelo local con la respuesta de la API
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
            if (configuracion == null)
                throw new InvalidOperationException(
                    "InicializarIntervalo: configuracion es null.");

            _suspendIntervalUpdate = true;

            try
            {
                var minutos = Math.Max(1, configuracion.IntervaloEnvioMinutos);
                var unidad = configuracion.IntervaloEnvioUnidad;

                var factor = ObtenerFactorEnMinutos(unidad);

                if (factor <= 0)
                    throw new InvalidOperationException(
                        $"Factor de conversión no válido: {factor}.");

                var unidadCompatible = ObtenerUnidadCompatible(minutos, unidad);
                _intervaloEnvioCantidad = minutos / ObtenerFactorEnMinutos(unidadCompatible);
                configuracion.IntervaloEnvioUnidad = unidadCompatible;
            }
            finally
            {
                _suspendIntervalUpdate = false;
            }
        }

        //private void InicializarIntervalo()
        //{
        //    _suspendIntervalUpdate = true;
        //    try
        //    {
        //        if (configuracion == null)
        //        {
        //            configuracion = new();
        //        }

        //        var minutos = Math.Max(1, configuracion.IntervaloEnvioMinutos);
        //        var unidad = configuracion.IntervaloEnvioUnidad;

        //        _intervaloEnvioCantidad = ConvertirDesdeMinutos(minutos, unidad);
        //        if (_intervaloEnvioCantidad < 1)
        //        {
        //            _intervaloEnvioCantidad = 1;
        //        }

        //        configuracion.IntervaloEnvioMinutos = ConvertirAMinutos(_intervaloEnvioCantidad, unidad);
        //    }
        //    finally
        //    {
        //        _suspendIntervalUpdate = false;
        //    }

        //    ActualizarIntervaloEnMinutos();
        //}

        private void ActualizarIntervaloEnMinutos()
        {
            if (_suspendIntervalUpdate)
            {
                return;
            }

            configuracion.IntervaloEnvioMinutos = ConvertirAMinutos(_intervaloEnvioCantidad, configuracion.IntervaloEnvioUnidad);
        }

        //private static int ConvertirAMinutos(int cantidad, IntervaloTiempoUnidadDto unidad)
        //{
        //    var valor = Math.Max(1, cantidad);

        //    return unidad switch
        //    {
        //        IntervaloTiempoUnidadDto.Horas => valor * 60,
        //        IntervaloTiempoUnidadDto.Dias => valor * 60 * 24,
        //        IntervaloTiempoUnidadDto.Semanas => valor * 60 * 24 * 7,
        //        _ => valor
        //    };
        //}

        private static int ConvertirAMinutos(
            int cantidad,
            IntervaloTiempoUnidadDto unidad)
        {
            var valor = Math.Max(1, cantidad);

            return checked(valor * ObtenerFactorEnMinutos(unidad));
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

        private static int ObtenerFactorEnMinutos(
                    IntervaloTiempoUnidadDto unidad)
        {
            return unidad switch
            {
                IntervaloTiempoUnidadDto.Horas => 60,
                IntervaloTiempoUnidadDto.Dias => 60 * 24,
                IntervaloTiempoUnidadDto.Semanas => 60 * 24 * 7,
                _ => 1
            };
        }

        private static IntervaloTiempoUnidadDto ObtenerUnidadCompatible(
            int minutos,
            IntervaloTiempoUnidadDto unidadPreferida)
        {
            // Conservar la unidad actual si representa exactamente el intervalo.
            if (minutos % ObtenerFactorEnMinutos(unidadPreferida) == 0)
            {
                return unidadPreferida;
            }

            // Si no es compatible, buscar la unidad más grande
            // que permita representar los minutos sin perder precisión.
            IntervaloTiempoUnidadDto[] unidades =
            {
                IntervaloTiempoUnidadDto.Semanas,
                IntervaloTiempoUnidadDto.Dias,
                IntervaloTiempoUnidadDto.Horas,
                IntervaloTiempoUnidadDto.Minutos
            };

            foreach (var unidad in unidades)
            {
                if (minutos % ObtenerFactorEnMinutos(unidad) == 0)
                {
                    return unidad;
                }
            }

            return IntervaloTiempoUnidadDto.Minutos;
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
