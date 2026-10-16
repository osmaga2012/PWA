using CRM.Dtos;
using CRM.Dtos.Enums;
using CRM.Dtos.Response;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MudBlazor.Components.Highlighter;
using PWA.Interfaces;
using System.Net.Mail;

namespace PWA.Pages
{
    public partial class FormularioTramite: ComponentBase
    {
        [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = default!;
        [CascadingParameter] private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

        [Parameter] public BarcosTramitesDto Model { get; set; } = new();
        [Parameter] public EventCallback OnCancelar { get; set; }
        [Parameter] public EventCallback<BarcosTramitesDto> OnGuardar { get; set; }
        [Parameter] public BarcosTramitesDto Tramite { get; set; } = new();
        [Parameter] public EventCallback<BarcosTramitesDto> OnValidSubmit { get; set; }
        [Parameter] public bool IsEditMode { get; set; }
        [Parameter] public string CodigoBarco { get; set; }
        [Parameter] public string CodigoEmpresa { get; set; }
        [Parameter] public BarcosDto barco { get; set; }
        [Parameter] public Guid IdBarco { get; set; }


        //Inyeccion de servicios
        [Inject] private ISnackbar SnackBarService { get; set; } = default!;
        [Inject] private IApiClient<BarcosTramitesDto> servicioTramites { get; set; }
        [Inject] private IApiClient<BarcosDto> servicioBarcos { get; set; }
        [Inject] private IApiClient<ConfiguracionDto> servicioConfiguracion { get; set; }
        [Inject] private IApiClient<TramiteAvisoConfigDto> servicioTramiteAvisoConfig { get; set; } = default!;
        [Inject] private IApiClient<AvisoDestinatariosSyncDto> servicioAvisoDestinatariosSync { get; set; } = default!;

        private ICollection<BarcosTramitesDto> tramites { get; set; } = default!;
        private BarcosTramitesDto tramite = new();
        private ConfiguracionDto configuracion = new();

        private MudForm form = default!;
        private bool success, ModoAlta;
        private string[] errors = { };

        // ---- Campos/estado ----
        private string _emailInput = string.Empty;
        private string _emailError = string.Empty;

        private List<string> EmailsAviso = new();
        private TramiteAvisoConfigDto? avisoConfig;
        private Guid? _destinatariosLoadedForTramiteId;

        // -- Subida de ficheros 
        private IBrowserFile? selectedFile;
        private string? selectedFileName;

        private List<IBrowserFile> selectedFiles = new();
        private List<string> existingFiles = new();

        private string? existingFileName;
        private const long MaxFileSize = 20 * 1024 * 1024;
        private const string UploadEndpoint = "api/BarcosTramites/upload";
        private string? publicFilesBaseUrl;
        private string? _selectedFileName;


        private MudForm _form = null!;
        private bool _formValido;

        private async Task Cancelar() => await OnCancelar.InvokeAsync();
        private async Task Guardar() => await OnGuardar.InvokeAsync(Model);

        private string? CodigoEmpresaEfectivo =>
            !string.IsNullOrWhiteSpace(CodigoEmpresa)
                ? CodigoEmpresa
                : barco?.CodigoEmpresa;

        protected override async Task OnParametersSetAsync()
        {
            tramite = Model ?? new BarcosTramitesDto();
            ModoAlta = !IsEditMode;

            EmailsAviso = ParseEmails(tramite?.ListaEmailsEnvioAviso).ToList();

            if (IsEditMode)
            {
                await CargarDestinatariosAvisoAsync();
            }
            else
            {
                avisoConfig = null;
                _destinatariosLoadedForTramiteId = null;
            }

            var rutaFichero = tramite?.RutaFichero;
            existingFiles = !string.IsNullOrEmpty(rutaFichero)
                ? rutaFichero.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList()
                : new List<string>();

        }

        private async Task GuardarDatos()
        {
            ResponseDto respuesta = new();
            if (!AddEmailsFromInput())
            {
                SnackBarService.Add(_emailError, Severity.Warning);
                return;
            }

            await form.Validate();
            if (!form.IsValid)
            {
                SnackBarService.Add("Por favor, corrige los errores del formulario.", Severity.Warning);
                return;
            }

            if (!await TryUploadAttachmentsAsync())
            {
                return;
            }

            try
            {
                //tramite = barco;
                //tramite.IdBarco = barco.Id;
                CompletarDatosDelBarco();
                tramite.FechaAviso = tramite.FechaFin.AddDays(tramite.DiasAvisoTramite * -1);
                tramite.ListaEmailsEnvioAviso = string.Join(';', EmailsAviso);
                if (IsEditMode && tramite.Id != Guid.Empty)
                {

                    respuesta = await servicioTramites.UpdateAsync("api/BarcosTramites", tramite, tramite.Id);
                }
                else
                {
                    CompletarDatosDelBarco();
                    //tramite.IdUsuarioCreacion = Guid.Parse(_userId);
                    //tramite.IdGestoria = barco.IdGestoria;

                    respuesta = await servicioTramites.CreateAsync("api/BarcosTramites", tramite);
                    //response = await Http.PostAsJsonAsync("api/tramite", tramiteFormModel);

                }

                if (respuesta.Success)
                {
                    var destinatariosSincronizados = await SincronizarDestinatariosAvisoAsync();

                    await form.ResetAsync();
                    SnackBarService.Add(
                        destinatariosSincronizados
                            ? (ModoAlta ? "Trámite creado correctamente." : "Trámite actualizado correctamente")
                            : "Trámite guardado, pero no se pudieron sincronizar los destinatarios de aviso.",
                        destinatariosSincronizados ? Severity.Success : Severity.Warning);
                    existingFileName = GetFileName(tramite.RutaFichero);
                    selectedFile = null;
                    selectedFileName = null;
                    await OnGuardar.InvokeAsync(tramite);
                    //MudDialog.Close(DialogResult.Ok(true)); // Cerrar diálogo con éxito
                    //StateHasChanged();
                    //EmailsAviso.Clear();
                    //EmailsAviso.Add(emailEmpresa);

                }

                //await CargarTramites();

            }
            catch (Exception ex)
            {
                SnackBarService.Add($"Ocurrió un error inesperado: {ex.Message}", Severity.Error);
            }
        }

        private void CompletarDatosDelBarco()
        {
            if (barco == null)
            {
                return;
            }

            if (tramite.CodigoBarco <= 0)
            {
                tramite.CodigoBarco = barco.CodigoBarco;
            }

            if (barco.Censo.HasValue && tramite.CensoBarco <= 0)
            {
                tramite.CensoBarco = barco.Censo.Value;
            }

            if (string.IsNullOrWhiteSpace(tramite.CodigoEmpresa))
            {
                tramite.CodigoEmpresa = CodigoEmpresaEfectivo;
            }

            if (string.IsNullOrWhiteSpace(tramite.IdGestoria))
            {
                tramite.IdGestoria = barco.IdGestoria;
            }
        }

        private async Task CargarTramites()
        {

            //Cargamos los datos del barco de la empresa seleccionada.
            barco = (await servicioBarcos.GetByIdAsync("api/Barcos/codigo", CodigoBarco));

            //cargamos la configuracion
            var configuraciones = await servicioConfiguracion.GetAllAsync("api/Configuracion")
                ?? Enumerable.Empty<ConfiguracionDto>();
            configuracion = configuraciones.FirstOrDefault() ?? new ConfiguracionDto();

            var filtro = new Dictionary<string, string>();
            filtro.Add("CodigoBarco", CodigoBarco);
            //filtro.Add("CodigoEmpresa", CodigoEmpresa);

            tramites = (await servicioTramites.GetAllAsync("api/BarcosTramites", filtro)).ToList();

            tramite = new();
            tramite.DiasAvisoTramite = configuracion == null ? 0 : configuracion.DiasAntelacionAviso;

            ModoAlta = true;
            selectedFile = null;
            selectedFileName = null;
            existingFileName = null;
        }

        private async Task HandleValidSubmit()
        {
            await OnValidSubmit.InvokeAsync(Tramite);
        }


        // ---- Manejo de teclado (Enter) en el TextField ----
        private void OnEmailKeyDown(KeyboardEventArgs e)
        {
            if (e.Key == "Enter")
            {
                AddEmailsFromInput();
                // No hace falta preventDefault aquí; evitamos efectos colaterales limpiando input.
            }
        }

        // ---- Alta/baja de chips ----
        private bool AddEmailsFromInput()
        {
            _emailError = string.Empty;

            if (string.IsNullOrWhiteSpace(_emailInput))
                return true;

            // Permite pegar varios: separadores ; , espacio o salto de línea
            var nuevos = ParseEmails(_emailInput);

            var added = false;
            var valid = true;
            foreach (var email in nuevos)
            {
                if (!IsValidEmail(email))
                {
                    _emailError = $"Email no válido: {email}";
                    valid = false;
                    continue;
                }

                // Evita duplicados (case-insensitive)
                if (!EmailsAviso.Contains(email, StringComparer.OrdinalIgnoreCase))
                {
                    EmailsAviso.Add(email);
                    added = true;
                }
            }

            if (added)
            {
                // ordenar opcionalmente
                EmailsAviso = EmailsAviso.OrderBy(x => x).ToList();
            }

            if (valid)
            {
                _emailInput = string.Empty;
            }

            StateHasChanged();
            return valid;
        }

        private void RemoveEmail(string email)
        {
            EmailsAviso.RemoveAll(x => string.Equals(x, email, StringComparison.OrdinalIgnoreCase));
            StateHasChanged();
        }

        private static IEnumerable<string> ParseEmails(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return Enumerable.Empty<string>();

            var tokens = raw
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t));

            // Distinct case-insensitive
            return tokens.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new MailAddress(email);
                return string.Equals(addr.Address, email, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private async Task CargarDestinatariosAvisoAsync()
        {
            avisoConfig = null;

            if (!IsEditMode || tramite == null || tramite.Id == Guid.Empty || _destinatariosLoadedForTramiteId == tramite.Id)
            {
                return;
            }

            _destinatariosLoadedForTramiteId = tramite.Id;

            try
            {
                avisoConfig = await servicioTramiteAvisoConfig.GetByIdAsync("api/TramiteAvisoConfigs/barco-tramite", tramite.Id);
                var emailsDestinatarios = avisoConfig?.Destinatarios?
                    .Where(d => d.Activo && d.CanalEmailActivo && d.TipoDestinatario == AvisoDestinatarioTipoDto.EmailManual)
                    .Select(d => d.Email)
                    .Where(email => !string.IsNullOrWhiteSpace(email))
                    .Select(email => email!.Trim())
                    .Where(IsValidEmail)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(email => email)
                    .ToList();

                if (emailsDestinatarios?.Any() == true)
                {
                    EmailsAviso = emailsDestinatarios;
                }
            }
            catch (Exception ex)
            {
                // Mantenemos el campo antiguo como fallback para no bloquear la edicion del tramite.
                Console.Error.WriteLine($"No se pudieron cargar destinatarios de aviso: {ex.Message}");
            }
        }

        private async Task<bool> SincronizarDestinatariosAvisoAsync()
        {
            if (tramite == null || tramite.Id == Guid.Empty)
            {
                return true;
            }

            try
            {
                var config = avisoConfig?.BarcoTramiteId == tramite.Id
                    ? avisoConfig
                    : await servicioTramiteAvisoConfig.GetByIdAsync("api/TramiteAvisoConfigs/barco-tramite", tramite.Id);

                if (config == null || config.Id == Guid.Empty)
                {
                    return false;
                }

                var emailsValidos = EmailsAviso
                    .Where(email => !string.IsNullOrWhiteSpace(email))
                    .Select(email => email.Trim())
                    .Where(IsValidEmail)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(email => email)
                    .ToList();

                var request = new AvisoDestinatariosSyncDto
                {
                    TramiteAvisoConfigId = config.Id,
                    Emails = emailsValidos
                };

                var respuesta = await servicioAvisoDestinatariosSync.UpdateAsync(
                    $"api/AvisoDestinatarios/tramite-config/{config.Id}/emails",
                    request);

                if (!respuesta.Success)
                {
                    return false;
                }

                EmailsAviso = emailsValidos;
                tramite.ListaEmailsEnvioAviso = string.Join(';', EmailsAviso);
                avisoConfig = await servicioTramiteAvisoConfig.GetByIdAsync("api/TramiteAvisoConfigs/barco-tramite", tramite.Id);
                _destinatariosLoadedForTramiteId = tramite.Id;
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"No se pudieron sincronizar destinatarios de aviso: {ex.Message}");
                return false;
            }
        }

        private async Task OnFileChanged(InputFileChangeEventArgs e)
        {
            if (e.FileCount == 0)
            {
                await ClearSelectedFile();
                return;
            }

            var file = e.File;
            if (file is null)
            {
                await ClearSelectedFile();
                return;
            }

            if (file.Size > MaxFileSize)
            {
                var maxSizeInMb = MaxFileSize / (1024 * 1024);
                SnackBarService.Add($"El archivo supera el tamaño máximo permitido de {maxSizeInMb} MB.", Severity.Warning);
                await ClearSelectedFile();
                return;
            }

            selectedFile = file;
            selectedFileName = file.Name;
            existingFileName = null;

            tramite.RutaFichero = string.Join(";", existingFiles);

            // Limpiamos si es necesario o permitimos múltiples
            foreach (var fichero in e.GetMultipleFiles(10))
            {
                if (fichero.Size > MaxFileSize)
                {
                    SnackBarService.Add($"El archivo {fichero.Name} supera el tamaño permitido.", Severity.Warning);
                    continue;
                }

                // Añadimos a la lista de pendientes para mostrarlo en la UI
                selectedFiles.Add(fichero);
            }


            StateHasChanged();
        }

        private Task ClearSelectedFile()
        {
            selectedFile = null;
            selectedFileName = null;
            existingFileName = null;
            tramite.RutaFichero = null;
            return Task.CompletedTask;
        }

        private async Task<bool> TryUploadAttachmentsAsync()
        {
            foreach (var file in selectedFiles)
            {
                // Llamada a tu servicio por cada archivo
                var response = await servicioTramites.UploadFileAsync(UploadEndpoint, file, MaxFileSize);

                if (response.Success)
                {
                    // Agregamos la ruta devuelta por el servidor a tu modelo
                    // (Asumiendo que tramite.RutaFichero ahora es una lista o string concatenado)
                    tramite.RutaFichero += (string.IsNullOrEmpty(tramite.RutaFichero) ? "" : ";") + response.Data.ToString();
                }
                else
                {
                    SnackBarService.Add($"Error al subir {file.Name}: {response.Message}", Severity.Error);
                }
            }
            selectedFiles.Clear();
            return true;
        }

        private async Task<bool> TryUploadAttachmentAsync_COPIA()
        {
            if (selectedFile is null)
            {
                return true;
            }

            try
            {
                var response = await servicioTramites.UploadFileAsync(UploadEndpoint, selectedFile, MaxFileSize);
                if (response is null || !response.Success)
                {
                    var message = response?.Message;
                    SnackBarService.Add(string.IsNullOrWhiteSpace(message) ? "No se pudo subir el archivo adjunto." : message, Severity.Error);
                    return false;
                }

                tramite.RutaFichero = response.Data?.ToString();
                existingFileName = GetFileName(tramite.RutaFichero) ?? selectedFile.Name;
                selectedFile = null;
                selectedFileName = null;
                return true;
            }
            catch (Exception ex)
            {
                SnackBarService.Add($"Error al subir el archivo: {ex.Message}", Severity.Error);
                return false;
            }
        }

        private static string? GetFileName(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                return Path.GetFileName(path);
            }
            catch
            {
                return path;
            }
        }

        // Quitar un archivo que el usuario seleccionó pero aún no se ha subido
        private void RemoveSelected(IBrowserFile file)
        {
            selectedFiles.Remove(file);
            StateHasChanged();
        }

        // Marcar un archivo ya existente para borrarlo cuando se pulse Guardar
        private void MarkForDeletion(string path)
        {
            if (existingFiles.Contains(path))
            {
                existingFiles.Remove(path);
                // Opcional: si necesitas notificar a la API que borre el archivo físicamente:
                // filesToDeleteOnSave.Add(path); 
            }
            StateHasChanged();
        }

        private string GetFileUrl(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "#";

            // Si la ruta ya es una URL completa (ej. de un Storage de Supabase), devuélvela
            if (path.StartsWith("http")) return path;

            // Si es una ruta relativa, construye la URL hacia tu API o servidor
            // Ajusta 'https://tu-api.com/' a la base de tus archivos estáticos
            return $"https://tu-api.com/{path.Trim()}";
        }
    }
}
