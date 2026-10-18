using CRM.Dtos;
using CRM.Dtos.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using PWA.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using static MudBlazor.CategoryTypes;

namespace PWA.Pages
{
    public partial class ListadoBarcosNuevo : Microsoft.AspNetCore.Components.ComponentBase 
    {
        [CascadingParameter] private Task<AuthenticationState> authenticationStateTask { get; set; }
        [Inject] private IAuthService _authService { get; set; }
        [Inject] private ICurrentUserService currentUserService { get; set; }
        [Inject] private IApiClient<BarcosDto> servicioBarco { get; set; }
        [Inject] private IApiClient<EmpresasDto> servicioEmpresa { get; set; }
        [Inject] private NavigationManager NavigationManager { get; set; }

        private ICollection<BarcosDto> barcos = new List<BarcosDto>();
        private ICollection<EmpresasDto> empresas = new List<EmpresasDto>();

        // Supongamos que traes el usuario desde un State o DTO
        private UsuarioDto? _loggedInUser;
        private bool _isLoading = false;

        //private bool EsCofradia;
        private bool EsCofradia => SessionState.LoggedInUser != null
                    && SessionState.LoggedInUser.TipoUsuario == TipoUsuarioDto.Administrador; // (O el enum que uses)
        private string _filtroBarco = "";

        private Func<BarcosDto, bool> filtroBarco => x =>
        {
            if (string.IsNullOrWhiteSpace(_filtroBarco))
            {
                return true; // Si no hay texto de búsqueda, mostrar todas las empresas
            }

            var textoBusquedaLower = _filtroBarco.ToLowerInvariant();

            // Revisa nulos para evitar NullReferenceException
            if (x.CodigoBarco.ToString().Contains(textoBusquedaLower, StringComparison.OrdinalIgnoreCase))
                return true;


            if (!string.IsNullOrEmpty(x.Empresa?.NombreArmador) && x.Empresa.NombreArmador.Contains(textoBusquedaLower, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(x.NombreB) && x.NombreB.Contains(textoBusquedaLower, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(x.NombreA) && x.NombreA.Contains(textoBusquedaLower, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(x.CapitanNombre) && x.CapitanNombre.Contains(textoBusquedaLower, StringComparison.OrdinalIgnoreCase))
                return true;

            if (x.CodigoEmpresa != null && !string.IsNullOrEmpty(x.CodigoEmpresa) && x.CodigoEmpresa.Contains(textoBusquedaLower, StringComparison.OrdinalIgnoreCase))
                return true;

            if ($"{x.CodigoBanco} {x.NombreB} {x.Empresa?.NombreArmador} {x.CapitanNombre} {x.CodigoEmpresa} ".Contains(textoBusquedaLower, StringComparison.OrdinalIgnoreCase))
                return true;



            return false;
        };

        protected override async Task OnInitializedAsync()
        {
            _isLoading = true;

            await LoadUserProfile();
            await CargarBarcos();

            _isLoading = false;

            StateHasChanged();
        }

        protected IQueryable<BarcosDto> FiltroBarcos
        {
            get
            {
                var source = barcos ?? Enumerable.Empty<BarcosDto>();
                if (string.IsNullOrWhiteSpace(_filtroBarco))
                {
                    return source.AsQueryable();
                }
                return source.Where(filtroBarco).AsQueryable();
            }
        }

        private async Task CargarBarcos()
        {
            //string[] includesEmpresas = new string[] { "Barco" };
            //var result = await servicioBarco.GetAllAsync("api/Barcos", null, null);
            //barcos = result?.ToList() ?? new List<BarcosDto>();


            string [] includeBarcos = new string[] { "Empresa" };
            List<EmpresasDto> empresas = (await servicioEmpresa.GetAllAsync("api/Empresa", null, null))?.ToList() ?? new List<EmpresasDto>();

            barcos = empresas.Select(x=>x.Barco).ToList();
        }

        private async Task LoadUserProfile()
        {
            var authState = await authenticationStateTask;
            var user = authState.User;

            if (user.Identity?.IsAuthenticated == true)
            {
                _loggedInUser = await currentUserService.GetCurrentUserAsync(user);
            }
        }

        private async Task ExportExcel()
        {
            // Build CSV from currently visible items (best-effort using _pageSize and paginacion state)
            //try
            //{
            //    var items = FiltroBarcos ?? Enumerable.Empty<BarcosDto>();

            //    // Determine current page index from paginacion object if available
            //    int pageIndex = 0;
            //    try
            //    {
            //        if (paginacion != null)
            //        {
            //            var t = paginacion.GetType();
            //            var pi = t.GetProperty("PageIndex") ?? t.GetProperty("Page") ?? t.GetProperty("CurrentPage") ?? t.GetProperty("PageNumber");
            //            if (pi != null)
            //            {
            //                var val = pi.GetValue(paginacion);
            //                if (val != null && int.TryParse(val.ToString(), out var vi)) pageIndex = vi;
            //            }
            //        }
            //    }
            //    catch { /* ignore */ }

            //    var pageItems = items.Skip(pageIndex * _pageSize).Take(_pageSize).ToList();

            //    var sb = new System.Text.StringBuilder();
            //    sb.AppendLine("CodigoBarco,NombreBarco,CodigoEmpresa,Empresa,Armador,Matriz");
            //    foreach (var it in pageItems)
            //    {
            //        var codigo = it.Barco != null ? it.Barco.CodigoBarco.ToString() : "";
            //        var nombre = it.Barco != null ? (it.Barco.NombreB ?? it.Barco.NombreA) ?? "" : "";
            //        var codEmp = it.CodigoEmpresa.ToString();
            //        var empresa = it.Empresa ?? "";
            //        var armador = it.NombreArmador ?? "";
            //        var matriz = it.EmpresaMatriz ?? "";
            //        // Escape quotes and commas by wrapping fields in quotes
            //        string esc(string v) => '"' + (v?.Replace("\"", "\"\"") ?? "") + '"';
            //        sb.AppendLine($"{esc(codigo)},{esc(nombre)},{esc(codEmp)},{esc(empresa)},{esc(armador)},{esc(matriz)}");
            //    }

            //    var fileName = $"barcos_page_{pageIndex + 1}.csv";
            //    await JS.InvokeVoidAsync("crmDownloadFile", fileName, sb.ToString());
            //}
            //catch (Exception ex)
            //{
            //    Console.Error.WriteLine(ex.Message);
            //}
        }

    //                if (PlatformService != null && PlatformService.IsNativeApp && NavigationService != null)
    //        {
    //            await NavigationService.NavigateToAsync($"barco/empresa/{codigoEmpresa}/tramites/{barco.CodigoBarco}");
    //}
    //        else
    //        {
    //            NavigationManager.NavigateTo($"barco/empresa/{codigoEmpresa}/tramites/{barco.CodigoBarco}");
    //        }

private void NavigarATramites(int codigoBarco) =>
            NavigationManager.NavigateTo($"barcos/{codigoBarco}/tramites");

        private void NavigarAEditar(int codigoBarco) =>
            NavigationManager.NavigateTo($"barcos/{codigoBarco}/editar");

        private void NavigarAUsuarios(int codigoBarco) =>
            NavigationManager.NavigateTo($"barcos/{codigoBarco}/usuarios");

    }
}
