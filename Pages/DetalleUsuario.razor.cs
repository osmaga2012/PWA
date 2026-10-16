using CRM.Dtos;
using CRM.Dtos.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PWA.Interfaces;

namespace PWA.Pages;

public partial class DetalleUsuario : ComponentBase
{
    [Parameter] public Guid UsuarioId { get; set; }

    [Inject] private IApiClient<UsuarioDto> servicioUsuarios { get; set; } = default!;
    [Inject] private IApiClient<EmpresasDto> servicioEmpresas { get; set; } = default!;
    [Inject] private ICurrentUserService currentUserService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private MudForm form = default!;
    private bool isLoading = true;
    private bool guardando = false;
    private string errorMessage = string.Empty;
    private UsuarioDto usuario = new();
    private List<EmpresasDto> empresas = new();
    private UsuarioDto? currentUser;
    private string passwordHashOriginal = string.Empty;

    private bool EsNuevo => EsRutaNuevo();
    private bool EsEmpresaLimitada => currentUser?.TipoUsuario == TipoUsuarioDto.Empresa;
    private string NombrePantalla => ObtenerNombrePantalla();

    protected override async Task OnParametersSetAsync()
    {
        await CargarUsuarioAsync();
    }

    private async Task CargarUsuarioAsync()
    {
        try
        {
            isLoading = true;
            errorMessage = string.Empty;
            passwordHashOriginal = string.Empty;

            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            if (authState.User.Identity?.IsAuthenticated == true)
            {
                currentUser = await currentUserService.GetCurrentUserAsync(authState.User);
            }

            var empresasResult = await servicioEmpresas.GetAllAsync("api/Empresa");
            empresas = empresasResult?.ToList() ?? new List<EmpresasDto>();

            if (EsNuevo)
            {
                usuario = new UsuarioDto
                {
                    Activo = true,
                    TipoUsuario = TipoUsuarioDto.Empresa,
                    Rol = "Cliente",
                    FechaRegistro = DateTime.UtcNow
                };

                if (currentUser?.TipoUsuario == TipoUsuarioDto.Empresa)
                {
                    //test
                    usuario.CodigoEmpresa = currentUser.CodigoEmpresa;
                    usuario.TipoUsuario = TipoUsuarioDto.Empresa;
                }
            }
            else
            {
                if (UsuarioId == Guid.Empty)
                {
                    errorMessage = "El identificador del usuario no es valido.";
                    usuario = new UsuarioDto();
                    return;
                }

                var existing = await ObtenerUsuarioAsync(UsuarioId);
                if (existing == null)
                {
                    errorMessage = "No se ha encontrado el usuario solicitado.";
                    usuario = new UsuarioDto();
                }
                else
                {
                    usuario = existing;
                    passwordHashOriginal = existing.PasswordHash;
                }
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"No se ha podido cargar el usuario: {ex.Message}";
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task<UsuarioDto?> ObtenerUsuarioAsync(Guid id)
    {
        try
        {
            var usuarioPorId = await servicioUsuarios.GetByIdAsync("api/Usuarios", id);
            if (usuarioPorId != null)
            {
                return usuarioPorId;
            }
        }
        catch
        {
        }

        var usuarios = await servicioUsuarios.GetAllAsync(
            "api/Usuarios",
            new Dictionary<string, string> { ["Id"] = id.ToString() });

        return usuarios?.FirstOrDefault();
    }

    private async Task GuardarUsuario()
    {
        errorMessage = string.Empty;
        await form.ValidateAsync();

        if (string.IsNullOrWhiteSpace(usuario.NIFAcceso))
        {
            errorMessage = "El NIF es obligatorio.";
            return;
        }

        if (string.IsNullOrWhiteSpace(usuario.EMail))
        {
            errorMessage = "El email es obligatorio.";
            return;
        }

        if (string.IsNullOrWhiteSpace(usuario.EMailAvisos))
        {
            usuario.EMailAvisos = usuario.EMail;
        }

        if (string.IsNullOrWhiteSpace(usuario.NombreUsuario) && !string.IsNullOrWhiteSpace(usuario.EMail))
        {
            usuario.NombreUsuario = usuario.EMail.Split('@')[0];
        }

        if (currentUser?.TipoUsuario == TipoUsuarioDto.Empresa)
        {
            usuario.CodigoEmpresa = currentUser.CodigoEmpresa;
            usuario.TipoUsuario = TipoUsuarioDto.Empresa;
        }

        try
        {
            guardando = true;

            if (EsNuevo)
            {
                usuario.Id = Guid.NewGuid();
                usuario.TokenActivacion = Guid.NewGuid();
                usuario.FechaExpiracionToken = DateTime.Now.AddHours(24);
                usuario.PasswordHash = string.Empty;

                var resultado = await servicioUsuarios.CreateAsync("api/Usuarios", usuario);
                if (resultado?.Success == true)
                {
                    Snackbar.Add("Usuario creado correctamente.", Severity.Success);
                    NavigationManager.NavigateTo("/gestoria/usuarios");
                }
                else
                {
                    errorMessage = resultado?.Message ?? "No se ha podido crear el usuario.";
                }
            }
            else
            {
                usuario.PasswordHash = passwordHashOriginal;

                var resultado = await servicioUsuarios.UpdateAsync("api/Usuarios", usuario, usuario.Id);
                if (resultado?.Success == true)
                {
                    Snackbar.Add("Usuario actualizado correctamente.", Severity.Success);
                    NavigationManager.NavigateTo("/gestoria/usuarios");
                }
                else
                {
                    errorMessage = resultado?.Message ?? "No se ha podido actualizar el usuario.";
                }
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Error al guardar el usuario: {ex.Message}";
        }
        finally
        {
            guardando = false;
        }
    }

    private void Volver()
    {
        NavigationManager.NavigateTo("gestoria/usuarios");
    }

    private bool EsRutaNuevo()
    {
        var relativePath = NavigationManager.ToBaseRelativePath(NavigationManager.Uri)
            .Split('?', '#')[0]
            .TrimEnd('/');

        return string.Equals(relativePath, "usuario/nuevo", StringComparison.OrdinalIgnoreCase);
    }

    private string ObtenerNombrePantalla()
    {
        var nombreCompleto = $"{usuario.Nombre} {usuario.Apellidos}".Trim();

        if (!string.IsNullOrWhiteSpace(nombreCompleto))
        {
            return nombreCompleto;
        }

        if (!string.IsNullOrWhiteSpace(usuario.NombreUsuario))
        {
            return usuario.NombreUsuario;
        }

        if (!string.IsNullOrWhiteSpace(usuario.EMail))
        {
            return usuario.EMail;
        }

        return "Editar usuario";
    }
}
