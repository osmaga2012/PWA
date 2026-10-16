using CRM.Dtos;
using CRM.Dtos.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PWA.Interfaces;
using System.Security.Claims;

namespace PWA.Pages
{
    public partial class Tramites : ComponentBase
    {
        [Inject] private ISnackbar Snackbar { get; set; } = default!;
        [Inject] private IApiClient<TramiteDto> tramiteService { get; set; } = default!;
        [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
        [Inject] private ICurrentUserService CurrentUserService { get; set; } = default!;
        [Inject] private NavigationManager NavigationManager { get; set; } = default!;

        private List<TramiteDto> tramites = new();
        private TramiteDto? _tramiteSeleccionado;
        private Guid? _selectedTramiteId;
        private Guid _editorKey = Guid.NewGuid();
        private bool _mobileEditorOpen;
        private bool _isLoading;

        protected override async Task OnInitializedAsync()
        {
            if (!await PuedeAccederATramitesAsync())
            {
                Snackbar.Add("La gestion de tramites solo esta disponible para gestores de la cofradia.", Severity.Warning);
                NavigationManager.NavigateTo("/", replace: true);
                return;
            }

            await CargarTramites();
        }

        private async Task<bool> PuedeAccederATramitesAsync()
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (user.Identity?.IsAuthenticated != true)
                return false;

            var currentUser = await CurrentUserService.GetCurrentUserAsync(user);
            return currentUser?.TipoUsuario == TipoUsuarioDto.Administrador || IsAdministradorClaim(user);
        }

        private static bool IsAdministradorClaim(ClaimsPrincipal user)
        {
            var rawTipoUsuario = user.FindFirst("tipo_usuario")?.Value
                ?? user.FindFirst("TipoUsuario")?.Value;

            return int.TryParse(rawTipoUsuario, out var numericValue)
                ? numericValue == (int)TipoUsuarioDto.Administrador
                : Enum.TryParse<TipoUsuarioDto>(rawTipoUsuario, true, out var tipoUsuario)
                    && tipoUsuario == TipoUsuarioDto.Administrador;
        }

        private async Task CargarTramites()
        {
            _isLoading = true;

            try
            {
                var resultado = await tramiteService.GetAllAsync("api/Tramites");
                tramites = resultado
                    .OrderBy(t => t.NombreTramite)
                    .ThenBy(t => t.Codigo)
                    .ToList();
            }
            catch (Exception ex)
            {
                tramites = new List<TramiteDto>();
                Snackbar.Add($"No se pudieron cargar los tramites: {ex.Message}", Severity.Error);
            }
            finally
            {
                _isLoading = false;
                StateHasChanged();
            }
        }

        private void SeleccionarTramite(TramiteDto tramiteSeleccionado)
        {
            _tramiteSeleccionado = tramiteSeleccionado;
            _selectedTramiteId = tramiteSeleccionado.Id == Guid.Empty ? null : tramiteSeleccionado.Id;
            _editorKey = Guid.NewGuid();
            _mobileEditorOpen = true;
        }

        private void IniciarCreacionTramite()
        {
            _tramiteSeleccionado = new TramiteDto();
            _selectedTramiteId = null;
            _editorKey = Guid.NewGuid();
            _mobileEditorOpen = true;
        }

        private void CerrarEditorMovil()
        {
            _mobileEditorOpen = false;
        }

        private void LimpiarFormulario()
        {
            _tramiteSeleccionado = new TramiteDto();
            _selectedTramiteId = null;
            _editorKey = Guid.NewGuid();
            _mobileEditorOpen = false;
        }

        private async Task ProcesarGuardado()
        {
            _mobileEditorOpen = false;
            await CargarTramites();
        }
    }
}
