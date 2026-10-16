// CRM.Web/Services/CurrentUserService.cs

using CRM.Dtos;
using CRM.Dtos.Enums;
using System.Security.Claims;
using System.Net.Http.Json;
using PWA.Interfaces;


namespace PWA.Servicios
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly HttpClient _httpClient;
        private readonly UserSessionState _sessionState;
        private UsuarioDto _cachedUser;

        public CurrentUserService(HttpClient httpClient, UserSessionState sessionState)
        {
            _httpClient = httpClient;
            _sessionState = sessionState;
        }

        public async Task<UsuarioDto> GetCurrentUserAsync(ClaimsPrincipal userPrincipal)
        {
            try
            {
                if (_cachedUser != null)
                {
                    return _cachedUser;
                }

                if (userPrincipal?.Identity == null || !userPrincipal.Identity.IsAuthenticated)
                {
                    _cachedUser = null;
                    return null;
                }

                // Obtener el UserId del JWT (necesario para el fallback y para pasar a GetUsuarioByIdAsync en el backend si se necesitara)
                var userIdClaim = userPrincipal.FindFirst(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                Guid currentUserId;
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out currentUserId))
                {
                    Console.WriteLine("Warning: User ID claim not found or not a valid GUID.");
                    _cachedUser = null;
                    return null;
                }

                try
                {
                    // ************************************************************
                    // ** ¡Llamada al endpoint "perfil" de tu UsuariosController! **
                    // ************************************************************
                    // Asumo que tu HttpClient ya está configurado para añadir el token de autorización
                    var userProfileFromApi = await _httpClient.GetFromJsonAsync<UsuarioDto>("api/Usuarios/perfil");

                    var configuracion = userProfileFromApi.Gestoria.Configuracion;

                    if (userProfileFromApi != null)
                    {
                        _cachedUser = userProfileFromApi;
                        _sessionState.SetUser(_cachedUser);

                        // Opcional: Asegurarse de que el ID del token coincide con el ID del perfil si es crítico
                        if (_cachedUser.Id != currentUserId)
                        {
                            Console.WriteLine("Warning: User ID from token does not match user ID from API profile.");
                        }

                        // Asegúrate de que los roles también se sincronicen si el JWT es la fuente principal para ellos
                        // Aunque lo ideal es que el DTO devuelto por el backend ya incluya el Rol actualizado.
                        _cachedUser.Rol = userPrincipal.FindFirst(ClaimTypes.Role)?.Value ?? _cachedUser.Rol;
                    }
                    else
                    {
                        // Si el API devuelve null pero el usuario está autenticado, crea un DTO básico
                        _cachedUser = MapClaimsToUsuarioDto(userPrincipal);
                    }
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Console.WriteLine($"Acceso denegado al endpoint /api/Usuarios/perfil. Posiblemente token expirado o permisos insuficientes: {ex.Message}");
                    // Si la llamada falla por auth, aún podemos intentar construir un UsuarioDto con los claims básicos del JWT
                    _cachedUser = MapClaimsToUsuarioDto(userPrincipal);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error general al obtener datos de perfil adicionales desde /api/Usuarios/perfil: {ex.Message}");
                    // En caso de otros errores, crea el usuario con solo los claims básicos del JWT
                    _cachedUser = MapClaimsToUsuarioDto(userPrincipal);
                }
            }
            catch (Exception)
            {
                throw;
            }
            return _cachedUser;
        }

        public void ClearCachedUser()
        {
            _cachedUser = null;
            _sessionState.ClearSession();
        }

        // Método auxiliar para mapear Claims a UsuarioDto (para cuando no hay datos completos de la API o fallback)
        private UsuarioDto MapClaimsToUsuarioDto(ClaimsPrincipal userPrincipal)
        {
            Guid id;
            Guid.TryParse(userPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out id);

            return new UsuarioDto
            {
                Id = id,
                EMail = userPrincipal.FindFirst(ClaimTypes.Email)?.Value,
                NombreUsuario = userPrincipal.FindFirst(ClaimTypes.Name)?.Value, // A veces el 'Name' claim es el nombre de usuario de Supabase
                // = userPrincipal.FindFirst(ClaimTypes.GivenName)?.Value,
                //Apellido1 = userPrincipal.FindFirst(ClaimTypes.Surname)?.Value,
                Rol = userPrincipal.FindFirst(ClaimTypes.Role)?.Value ?? "Usuario", // Asigna un rol por defecto si no está en claims
                Activo = true, // Asume activo si está logueado
                // Otros campos se dejarán por defecto o null, ya que no están en el JWT
                TipoUsuario = GetTipoUsuarioFromClaims(userPrincipal),
                FechaUltimoAcceso = null
            };
        }

        private static TipoUsuarioDto GetTipoUsuarioFromClaims(ClaimsPrincipal userPrincipal)
        {
            var rawTipoUsuario = userPrincipal.FindFirst("tipo_usuario")?.Value
                ?? userPrincipal.FindFirst("TipoUsuario")?.Value;

            if (int.TryParse(rawTipoUsuario, out var numericValue)
                && Enum.IsDefined(typeof(TipoUsuarioDto), numericValue))
            {
                return (TipoUsuarioDto)numericValue;
            }

            return Enum.TryParse<TipoUsuarioDto>(rawTipoUsuario, true, out var tipoUsuario)
                ? tipoUsuario
                : TipoUsuarioDto.Persona;
        }
    }
}
