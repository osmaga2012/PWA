using CRM.Dtos;
using CRM.Dtos.Enums;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using PWA.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace PWA.Layout.Menus
{
    public partial class MainNavMenu : ComponentBase
    {
        [Parameter] public TipoUsuarioDto TipoUsuario { get; set; }
        [Parameter] public EventCallback ToggleDarkMode { get; set; }
        [Parameter] public bool IsDarkMode { get; set; }

        [Inject] private ICurrentUserService currentUserService { get; set; } // Servicio para obtener el usuario actual

        [CascadingParameter] private Task<AuthenticationState> authenticationStateTask { get; set; } // Acceso al estado de autenticación


        private bool collapseNavMenu = true;
        private bool ocultarItem = false;
        private UsuarioDto? _loggedInUser; // El perfil del usuario, puede ser null
        private bool PuedeVerTramites => _loggedInUser?.TipoUsuario == TipoUsuarioDto.Administrador;

        private string? NavMenuCssClass => collapseNavMenu ? "collapse" : null;

        protected override async Task OnInitializedAsync()
        {
            // Primero, verifica si el usuario está realmente autenticado según AuthStateProvider
            var authState = await authenticationStateTask;
            var user = authState.User;

            await Task.Delay(500); // Pequeña espera para asegurar que todo esté listo
            _loggedInUser = await currentUserService.GetCurrentUserAsync(user);

            StateHasChanged();
        }

        private void ToggleNavMenu()
        {
            collapseNavMenu = !collapseNavMenu;
        }

        private async Task OnToggleDarkModeClicked()
        {
            if (ToggleDarkMode.HasDelegate)
            {
                await ToggleDarkMode.InvokeAsync();
            }
        }

    }
}
