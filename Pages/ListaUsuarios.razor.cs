using CRM.Dtos;
using CRM.Dtos.Request;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

// QuickGrid removed. Using MudTable instead in the Razor markup.
using MudBlazor;
using PWA.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace PWA.Pages
{
    public partial class ListaUsuarios : ComponentBase, IDisposable
    {
        [Inject] private IApiClient<UsuarioDto> usuarioService { get; set; }
        [Inject] private IApiClient<EmpresasDto> empresasService { get; set; }
        [Inject] private IApiClient<ConfiguracionDto> configuracionService { get; set; }

        [Inject] private IDialogService DialogService { get; set; }
        [Inject] private ISnackbar SnackbarService { get; set; }
        [Inject] private IHttpClientFactory http { get; set; } = null!;
        [Inject] private NavigationManager NavManager { get; set; } = default!;

        [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; }
        [Inject] private ICurrentUserService CurrentUserService { get; set; }
        [Inject] private IAuthService AuthService { get; set; }

        [Parameter] public List<UsuarioDto> Usuarios { get; set; } = new();

        [CascadingParameter] public IMudDialogInstance Modal { get; set; }

        private UsuarioDto usuario { get; set; } = new UsuarioDto();
        private string _FiltroUsuarios;

        private bool isLoading = true;
        private bool IsEditMode = false;
        private List<EmpresasDto> Empresas = new List<EmpresasDto>();
        private UsuarioDto selectedUsuario;
        private UsuarioDto? currentUser;

        private MudForm form = default!;
        private bool success;
        private string[] errors = { };
        private string? message;
        private bool isSuccess;
        private bool isLoaded = false;
        private bool _fixed = false;
        private string passCache;

        private OverflowBehavior _overflowBehavior = OverflowBehavior.FlipOnOpen;
        //private DropdownSettings _dropdownSettings => new DropdownSettings() { Fixed = _fixed, OverflowBehavior = _overflowBehavior, };

        private EmpresasDto _selectedEmpresaCache;
        // Pagination removed: using table and server/client side paging can be added later if needed

        private string _filtroNombreUsuario = string.Empty;

        private IEnumerable<UsuarioDto> FilteredUsuarios =>
            (Usuarios ?? Enumerable.Empty<UsuarioDto>())
            .Where(u => string.IsNullOrEmpty(_filtroNombreUsuario) ||
                        (u.NombreUsuario ?? string.Empty).Contains(_filtroNombreUsuario, StringComparison.OrdinalIgnoreCase))
            .ToList();
        private MudTable<UsuarioDto> usuariosTable;
        private IEnumerable<UsuarioDto> currentPageItems = Enumerable.Empty<UsuarioDto>();

        private async Task<TableData<UsuarioDto>> LoadServerData(TableState state)
        {
            // Simple client-side paging using the filtered collection
            var items = FilteredUsuarios.ToList();
            var total = items.Count;

            var pageItems = items.Skip(state.Page * state.PageSize).Take(state.PageSize).ToList();
            currentPageItems = pageItems;

            return await Task.FromResult(new TableData<UsuarioDto>() { TotalItems = total, Items = pageItems });
        }
        private EmpresasDto EmpresaSelecionada
        {
            get
            {
                if (_selectedEmpresaCache != null)
                {
                    return _selectedEmpresaCache;
                }

                if (!String.IsNullOrEmpty(usuario.CodigoEmpresa))
                {
                    _selectedEmpresaCache = Empresas.FirstOrDefault(e => e.CodigoEmpresa == usuario.CodigoEmpresa);
                    return _selectedEmpresaCache;
                }
                return null;
            }
            set
            {
                _selectedEmpresaCache = value;
                usuario.CodigoEmpresa = value?.CodigoEmpresa;
            }
        }

        protected override async Task OnInitializedAsync()
        {
            // Suscribirse a cambios de estado de autenticación para actualizar la UI
            AuthenticationStateProvider.AuthenticationStateChanged += AuthenticationStateChangedHandler;

            try
            {
                var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
                var user = authState.User;

                if (user.Identity != null && user.Identity.IsAuthenticated)
                {
                    currentUser = await CurrentUserService.GetCurrentUserAsync(user);
                    if (currentUser != null)
                    {
                    }
                }

                Console.WriteLine($"Usuario logueado: {currentUser?.NombreUsuario}");

                Usuarios = (await usuarioService.GetAllAsync("api/Usuarios")).ToList();
                Empresas = (await empresasService.GetAllAsync("api/Empresa")).ToList();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error cargando usuarios: {ex.Message}");
                Usuarios = new List<UsuarioDto>();
                Empresas = new List<EmpresasDto>();
            }

            isLoading = false;

            StateHasChanged();
        }

        private async void AuthenticationStateChangedHandler(Task<AuthenticationState> task)
        {
            try
            {
                var authState = await task;
                var user = authState.User;
                if (user.Identity != null && user.Identity.IsAuthenticated)
                {
                    currentUser = await CurrentUserService.GetCurrentUserAsync(user);
                }
                else
                {
                    currentUser = null;
                }

                await InvokeAsync(StateHasChanged);
            }
            catch
            {
                // Ignorar errores en el handler
            }
        }

        public void Dispose()
        {
            try
            {
                AuthenticationStateProvider.AuthenticationStateChanged -= AuthenticationStateChangedHandler;
            }
            catch
            {
                // Ignorar
            }
        }

        private bool IsCurrentUserAdministrador()
        {
            return currentUser != null && currentUser.TipoUsuario == CRM.Dtos.Enums.TipoUsuarioDto.Administrador;
        }

        private string GetInitials(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "?";
            }

            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
            }

            return name.Substring(0, Math.Min(2, name.Length)).ToUpperInvariant();
        }

        private Color GetColorPerfil(UsuarioDto usuario)
        {
            return usuario?.TipoUsuario switch
            {
                CRM.Dtos.Enums.TipoUsuarioDto.Administrador => Color.Error,
                CRM.Dtos.Enums.TipoUsuarioDto.Empresa => Color.Primary,
                CRM.Dtos.Enums.TipoUsuarioDto.Persona => Color.Success,
                _ => Color.Default
            };
        }

        protected bool FiltroUsuario(UsuarioDto usuario)
        {
            if (string.IsNullOrWhiteSpace(_FiltroUsuarios))
            {
                return true;
            }

            var textoBusquedaLower = _FiltroUsuarios.ToLowerInvariant();

            return usuario.NombreUsuario.ToLowerInvariant().Contains(textoBusquedaLower);
        }

        public async Task ConfirmarEliminar(UsuarioDto usuario)
        {
            bool? result = await DialogService.ShowMessageBoxAsync(
                "Confirmación de Eliminación",
                $"¿Estás seguro de que quieres eliminar la empresa '{usuario.NombreUsuario}'?",
                yesText: "Sí, eliminar",
                cancelText: "Cancelar",
                options: new DialogOptions { CloseButton = true, MaxWidth = MaxWidth.Small, FullWidth = true }
            );

            if (result == true)
            {
                await usuarioService.DeleteAsync("api/Usuarios", usuario.Id);
            }
            else
            {
                SnackbarService.Add("Eliminación cancelada.", Severity.Info);
            }
        }

        private void CrearNuevoUsuario()
        {
            NavManager.NavigateTo("usuario/nuevo");
        }

        private async void Guardar(Microsoft.AspNetCore.Components.Web.MouseEventArgs args)
        {
            if (IsEditMode)
            {
                //var pass = BCrypt.Net.BCrypt.HashPassword(usuario.PasswordHash);
                //usuario.PasswordHash = pass;

                await usuarioService.UpdateAsync("api/Usuarios", usuario, usuario.Id);
            }
            else
            {
                //var pass = BCrypt.Net.BCrypt.HashPassword(usuario.PasswordHash);
                //usuario.PasswordHash = pass;

                if (currentUser.TipoUsuario == CRM.Dtos.Enums.TipoUsuarioDto.Empresa)
                {
                    usuario.CodigoEmpresa = currentUser.CodigoEmpresa;
                    usuario.TipoUsuario = CRM.Dtos.Enums.TipoUsuarioDto.Empresa;
                }

                usuario.TokenActivacion = Guid.NewGuid();
                usuario.FechaExpiracionToken = DateTime.Now.AddHours(24);
                usuario.Id = Guid.NewGuid();
                usuario.Activo = false;

                await usuarioService.CreateAsync("api/Usuarios", usuario);

                await EnviarEmailActivacion(usuario);

            }

            StateHasChanged();
        }

        private void OnCancel_Click(Microsoft.AspNetCore.Components.Web.MouseEventArgs args)
        {
            throw new NotImplementedException();
        }

        private void VerDetalleUsuario(Guid id)
        {
            NavManager.NavigateTo($"usuario/{id}");
        }

        private async Task<IEnumerable<EmpresasDto>> BuscarEmpresa(string value, CancellationToken token)
        {
            await Task.Delay(5, token);

            if (string.IsNullOrEmpty(value))
                return Empresas;

            return Empresas.Where(x => x.CodigoEmpresa.Contains(value, StringComparison.InvariantCultureIgnoreCase) || x.NombreB.Contains(value, StringComparison.InvariantCultureIgnoreCase));
        }

        public async Task EnviarEmailUsuariosEmpresa(Guid? id)
        {

            List<UsuarioDto> usuariosEmpresa = new List<UsuarioDto>();

            try
            {
                if (id == null || id == Guid.Empty)
                {
                    usuariosEmpresa = Usuarios.Where(u => u.TipoUsuario == CRM.Dtos.Enums.TipoUsuarioDto.Empresa &&
                                                     u.Activo == false).ToList();
                }
                else
                {
                    usuariosEmpresa.Add (await usuarioService.GetByIdAsync("api/Usuarios", id));
                    //usuariosEmpresa = Usuarios.Where(u => u.TipoUsuario == CRM.Dtos.Enums.TipoUsuarioDto.Empresa && 
                    //                                     u.Id == id).ToList();
                }

                if (!usuariosEmpresa.Any())
                {
                    SnackbarService.Add("No hay usuarios de tipo Empresa para enviar emails.", Severity.Warning);
                    return;
                }

                foreach (var usuario in usuariosEmpresa)
                {
                    if (string.IsNullOrEmpty(usuario.EMail))
                    {
                        SnackbarService.Add($"El usuario '{usuario.NombreUsuario}' no tiene un email válido. Se omitirá el envío para este usuario.", Severity.Warning);
                        continue;
                    }

                    var configuracion = currentUser.Gestoria.Configuracion;
                    if (configuracion == null)
                    {
                        SnackbarService.Add("No se pudo obtener la configuración de envío de correo.", Severity.Error);
                        return;
                    }

                    //var email = "oscar.manas.garcia@gmail.com";

                    var asunto = "Acceso a la plataforma";
                    var cuerpo = $"Hola {usuario.NombreUsuario},<br/><br/>" +
                                 $"Puedes acceder a la plataforma usando las siguientes credenciales:<br/>" +
                                 $"<b>URL:</b> {configuracion.EmailEnvio}<br/>" +
                                 $"<b>Usuario:</b> {usuario.EMail}<br/>" +
                                 $"<b>Contraseña:</b> {usuario.PasswordHash}<br/><br/>" +
                                 "Saludos,<br/>El equipo de soporte.";

                    await EnviarEmailAsync(configuracion, usuario, asunto, cuerpo);
                }


                SnackbarService.Add("Emails enviados exitosamente.", Severity.Success);
            }
            catch (Exception ex)
            {
                SnackbarService.Add($"Error al enviar emails: {ex.Message}", Severity.Error);
            }
        }

        public async Task EnviarEmailActivacion(UsuarioDto usuario)
        {
            await EnviarEmailUsuariosEmpresa(usuario.Id);
        }

        private async Task EnviarEmailAsync(CRM.Dtos.ConfiguracionDto configuracion, UsuarioDto usuario, string asunto, string cuerpo)
        {

            // Empaquetamos todo lo que nos pediste para el controlador
            var requestDto = new EnviarEmailRequestDto
            {
                Configuracion = configuracion,
                Usuario = usuario,
                Asunto = asunto,
                Cuerpo = cuerpo
            };

            // Al igual que haces en ApiClient.cs, sacamos el cliente configurado para el CRM
            var httpClient = http.CreateClient("ApiClient");

            var respuesta = await httpClient.PostAsJsonAsync("api/Usuarios/enviar-invitaciones-pendientes", requestDto);

            if (respuesta.IsSuccessStatusCode)
            {
                //enviadosConExito++;
            }
            else
            {
                // Si el API responde con un error (ej: 500 del servidor SMTP), leemos el error
                var contenidoError = await respuesta.Content.ReadAsStringAsync();
                SnackbarService.Add($"Error al enviar a {usuario.NombreUsuario}: {respuesta.StatusCode}", Severity.Error);
            }
        }
    }
}
