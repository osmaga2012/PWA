using CRM.Dtos.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PWA.Interfaces;
using System.Security.Claims;

namespace PWA.Layout.Menus
{
    public partial class LiquidBottomNav : ComponentBase, IDisposable
    {
        [Parameter] public EventCallback<int> OnTabChanged { get; set; }
        [Parameter] public int ActiveTab { get; set; }
        [Parameter] public EventCallback<int> OnSelectTab { get; set; }
        [Parameter] public int ActiveIndex { get; set; }
        [Parameter] public EventCallback OnAbrirTramites { get; set; }

        [Inject] private NavigationManager NavManager { get; set; } = default!;
        [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
        [Inject] private ICurrentUserService CurrentUserService { get; set; } = default!;

        private bool _canSeeTramites;

        private IReadOnlyList<NavItem> VisibleItems => _canSeeTramites
            ? AdminNavItems
            : BaseNavItems;

        private static readonly IReadOnlyList<NavItem> AdminNavItems = new List<NavItem>
        {
            new(0, "/new", "Inicio", Icons.Material.Filled.Home),
            new(1, "barcos", "Flota", Icons.Material.Filled.DirectionsBoat),
            new(2, "tramites", "Trámites", Icons.Material.Filled.ReceiptLong, true),
            new(3, "gestoria/usuarios", "Usuarios", Icons.Material.Filled.Person)
        };

        private static readonly IReadOnlyList<NavItem> BaseNavItems = new List<NavItem>
        {
            new(0, "/new", "Inicio", Icons.Material.Filled.Home),
            new(1, "barcos", "Flota", Icons.Material.Filled.DirectionsBoat),
            new(3, "gestoria/usuarios", "Usuarios", Icons.Material.Filled.Person)
        };

        protected override async Task OnInitializedAsync()
        {
            NavManager.LocationChanged += OnLocationChanged;
            await LoadNavigationVisibilityAsync();
            SetActiveFromUrl(NavManager.Uri);
        }

        private void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
        {
            SetActiveFromUrl(e.Location);
            InvokeAsync(StateHasChanged);
        }

        private void SetActiveFromUrl(string uri)
        {
            var path = new Uri(uri).AbsolutePath.TrimStart('/').ToLowerInvariant();

            var tabIndex = path switch
            {
                "" or "home" => 0,
                "barcos" => 1,
                _ when path.StartsWith("barco") => 1,
                "tramites" => 2,
                _ when path.StartsWith("tramite") => 2,
                "usuarios" => 3,
                _ when path.StartsWith("usuarios") => 3,
                _ => (int?)null
            };

            if (tabIndex.HasValue)
                ActiveIndex = GetVisibleIndex(tabIndex.Value);
        }

        private async Task SetActive(int index)
        {
            ActiveIndex = index;

            if (OnSelectTab.HasDelegate)
                await OnSelectTab.InvokeAsync(VisibleItems[index].TabIndex);
        }

        private async Task AbrirTramitesAsync()
        {
            if (!_canSeeTramites)
                return;

            ActiveIndex = GetVisibleIndex(2);

            if (OnAbrirTramites.HasDelegate)
                await OnAbrirTramites.InvokeAsync();
            else
                NavManager.NavigateTo("/tramites");
        }

        private int GetVisibleIndex(int tabIndex)
        {
            var index = VisibleItems
                .Select((item, position) => new { item.TabIndex, position })
                .FirstOrDefault(item => item.TabIndex == tabIndex)?.position;

            return index ?? 0;
        }

        private async Task LoadNavigationVisibilityAsync()
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (user.Identity?.IsAuthenticated != true)
            {
                _canSeeTramites = false;
                return;
            }

            var currentUser = await CurrentUserService.GetCurrentUserAsync(user);
            _canSeeTramites = currentUser?.TipoUsuario == TipoUsuarioDto.Administrador || IsAdministradorClaim(user);
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

        protected override void OnParametersSet()
        {
            // Solo sincronizar si la URL no sobrescribió ya el estado.
        }

        public string IndicatorLeft => $"calc({ActiveIndex * 25}% + 12.5% - 33px)";

        public void Dispose()
        {
            NavManager.LocationChanged -= OnLocationChanged;
        }

        private sealed record NavItem(int TabIndex, string Url, string Text, string Icon, bool IsTramites = false);
    }
}
