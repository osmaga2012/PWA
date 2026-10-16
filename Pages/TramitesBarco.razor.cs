using CRM.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;
using PWA.Interfaces;
using System.Security.Claims;

namespace PWA.Pages
{
    public partial class TramitesBarco : ComponentBase
    {
        // En TramitesBarco.cs
        //[SupplyParameterFromQuery(Name = "abrirNuevo")]
        //[CascadingParameter] private IDialogService MudDialog1 { get; set; } = default!;
        [CascadingParameter] private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

        [Inject] private IApiClient<BarcosTramitesDto> servicioTramites { get; set; }
        [Inject] private IApiClient<ConfiguracionDto> servicioConfiguracion { get; set; }
        [Inject] private IApiClient<BarcosDto> servicioBarcos { get; set; }
        [Inject] private IApiClient<EmpresasDto> servicioEmpresa { get; set; }
        [Inject] private ISnackbar Snackbar { get; set; }
        [Inject] private IDialogService DialogService { get; set; }
        [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;
        [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
        [Inject] private NavigationManager NavigationManager { get; set; } = default!;
        [Inject] private IConfiguration Configuration { get; set; } = default!;
        [Inject] private ICurrentUserService currentUserService { get; set; } = default!;

        [Parameter] public string CodigoBarco { get; set; }
        [Parameter] public string CodigoEmpresa { get; set; }
        //[Parameter] public Guid? id { get; set; }
        [Parameter] public BarcosDto barco { get; set; }
        [Parameter] public Guid IdBarco { get; set; }

        public bool AbrirNuevo { get; set; }
       
        private BarcosTramitesDto _tramiteActivo = new();
        private bool _mobileEditorOpen;

        private ClaimsPrincipal? _user;
        private string? _userId;
        private string? _userEmail;

        private List<BarcosTramitesDto> tramites { get; set; } = new List<BarcosTramitesDto>();
        private BarcosTramitesDto tramite = new();
        private ConfiguracionDto configuracion = new();
        private string _filtroTramites = string.Empty;
        private MudForm form = default!;
        private bool success, ModoAlta;
        private string[] errors = { };
        private IBrowserFile? selectedFile;
        private string? selectedFileName;
        private string? existingFileName;
        private const long MaxFileSize = 20 * 1024 * 1024;
        private const string UploadEndpoint = "api/BarcosTramites/upload";
        private string? publicFilesBaseUrl;
        private string? _selectedFileName;
        private bool _confirmVisible;
        private BarcosTramitesDto _toDelete;
        private string emailEmpresa = "";

        private Breakpoint _bp = Breakpoint.Xs;
        private bool IsMobile = true;
        private bool IsEditMode => !ModoAlta;


        // ---- Campos/estado ----
        private string _emailInput = string.Empty;
        private string _emailError = string.Empty;
        private List<string> EmailsAviso = new();


        protected override async Task OnParametersSetAsync()
        {

            if (AbrirNuevo && configuracion != null)
            {
                IniciarCreacionTramite();
            }

        }

        protected override async Task OnInitializedAsync()
        {

            // Asegúrate de que los objetos iniciales no sean nulos
            tramite ??= new BarcosTramitesDto();

            publicFilesBaseUrl = Configuration["FileSettings:PublicBaseUrl"];
            IsMobile = _bp < Breakpoint.Md;

            var query = new Dictionary<string, string>();
            query.Add("CodigoEmpresa", CodigoEmpresa);

            //emailEmpresa = (await servicioEmpresa.GetAllAsync("api/Empresa/codigo")).FirstOrDefault().EMail1;
            await CargarTramites();
            _ = CargarUsuarioAsync();

            ////EmailsAviso.Clear();
            ////EmailsAviso.Add(emailEmpresa);

            StateHasChanged();
        }

        private void LimpiarFormulario()
        {
            _tramiteActivo = new BarcosTramitesDto();
            tramite = new BarcosTramitesDto
            {
                DiasAvisoTramite = configuracion?.DiasAntelacionAviso ?? 0
            };
            ModoAlta = true;
            _mobileEditorOpen = false;
        }

        private void CerrarEditorMovil()
        {
            _mobileEditorOpen = false;
        }

        private void SeleccionarTramite(BarcosTramitesDto tramiteSeleccionado)
        {
            tramite = tramiteSeleccionado;
            ModoAlta = false;
            _mobileEditorOpen = true;
        }
        private async Task ProcesarGuardado(BarcosTramitesDto tramite)
        {
            // Tu lógica para guardar en la base de datos...
            _mobileEditorOpen = false;
            await CargarTramites();
        }

        private async Task CargarUsuarioAsync()
        {
            var authState = await AuthStateTask;
            var user = authState.User;

            if (user?.Identity?.IsAuthenticated == true)
            {
                _user = user;

                // Intenta varios tipos de claim habituales
                _userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? user.FindFirst("sub")?.Value
                       ?? user.FindFirst("user_id")?.Value;

                _userEmail = user.FindFirst(ClaimTypes.Email)?.Value
                          ?? user.Identity?.Name;
            }
            else
            {
                _user = null;
                _userId = null;
                _userEmail = null;
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

        private void IniciarCreacionTramite()
        {
            // Reseteamos el objeto para que esté vacío (limpio)
            tramite = new BarcosTramitesDto();
            tramite.DiasAvisoTramite = configuracion?.DiasAntelacionAviso ?? 0;
            ModoAlta = true;

            // Abrimos el editor
            _mobileEditorOpen = true;

            // Fuerza el renderizado
            StateHasChanged();
        }

    }
}
