using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace PWA.Providers
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly HttpClient _httpClient;
        private readonly AuthenticationState _anonymous;

        private static readonly string[] _requiredClaims = new[]
        {
            ClaimTypes.NameIdentifier,
            ClaimTypes.Name
        };

        public CustomAuthStateProvider(IJSRuntime jsRuntime, HttpClient httpClient)
        {
            _jsRuntime = jsRuntime;
            _httpClient = httpClient;
            _anonymous = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                var token = await GetTokenFromStorageAsync();

                if (string.IsNullOrWhiteSpace(token))
                {
                    Console.WriteLine("[Auth] No hay token - retornando anónimo");
                    return _anonymous;
                }

                // Validar y decodificar el token
                var handler = new JwtSecurityTokenHandler();
                if (!handler.CanReadToken(token))
                {
                    Console.WriteLine("[Auth] Token inválido - removiendo");
                    await RemoveTokenAsync();
                    return _anonymous;
                }

                var jwtToken = handler.ReadJwtToken(token);

                // Verificar si el token ha expirado
                if (jwtToken.ValidTo < DateTime.UtcNow)
                {
                    Console.WriteLine("[Auth] Token expirado - removiendo");
                    await RemoveTokenAsync();
                    return _anonymous;
                }

                // Construir los claims desde el JWT
                var claims = BuildClaimsFromToken(jwtToken);

                if (!HasRequiredClaims(claims))
                {
                    Console.WriteLine("[Auth] Claims requeridos faltantes - removiendo token");
                    await RemoveTokenAsync();
                    return _anonymous;
                }

                var identity = new ClaimsIdentity(claims, "jwt");
                var user = new ClaimsPrincipal(identity);

                Console.WriteLine($"[Auth] Usuario autenticado: {user.Identity?.Name}");
                return new AuthenticationState(user);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Auth] Error: {ex.Message}");
                await RemoveTokenAsync();
                return _anonymous;
            }
        }

        /// <summary>
        /// Notifica que el usuario se ha autenticado exitosamente
        /// </summary>
        public async Task MarkUserAsAuthenticatedAsync(string token)
        {
            try
            {
                // Guardar el token
                await SetTokenInStorageAsync(token);

                // Decodificar el JWT
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                // Obtener el ID del usuario
                var userId = jwtToken.Claims
                    .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    Console.WriteLine("[Auth] No se pudo extraer el ID de usuario del JWT");
                    await MarkUserAsLoggedOutAsync();
                    return;
                }

                Console.WriteLine($"[Auth] Token configurado en HttpClient. Intentando obtener perfil...");

                // Obtener información adicional del perfil del usuario
                try
                {
                    var profileResponse = await _httpClient.GetFromJsonAsync<CRM.Dtos.UsuarioDto>("/api/Usuarios/perfil");

                    if (profileResponse != null)
                    {
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.NameIdentifier, userId),
                            new Claim(ClaimTypes.Name, profileResponse.Nombre ?? ""),
                            new Claim(ClaimTypes.Email, profileResponse.EMail ?? ""),
                            new Claim("tipo_usuario", profileResponse.TipoUsuario.ToString()),
                            new Claim("usuario_id", userId)
                        };

                        // Agregar claims adicionales del JWT
                        foreach (var claim in jwtToken.Claims.Where(c => 
                            c.Type == ClaimTypes.Role || c.Type == "role"))
                        {
                            claims.Add(new Claim(ClaimTypes.Role, claim.Value));
                        }

                        var identity = new ClaimsIdentity(claims, "jwt");
                        var user = new ClaimsPrincipal(identity);

                        Console.WriteLine($"[Auth] Usuario marcado como autenticado: {user.Identity?.Name}");
                        NotifyAuthenticationStateChanged(
                            Task.FromResult(new AuthenticationState(user)));
                    }
                    else
                    {
                        Console.WriteLine("[Auth] No se pudo obtener el perfil del usuario - respuesta nula");
                        await MarkUserAsLoggedOutAsync();
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    Console.WriteLine($"[Auth] Error HTTP obteniendo perfil: {httpEx.Message}");
                    Console.WriteLine($"[Auth] StatusCode: {httpEx.StatusCode}");

                    // Si es 401, el token no es válido según la API
                    if (httpEx.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        Console.WriteLine("[Auth] Token rechazado por la API (401). Verificar configuración de JWT en el servidor.");
                        await MarkUserAsLoggedOutAsync();
                    }
                    else
                    {
                        // Para otros errores, intentar construir claims básicos desde el JWT
                        Console.WriteLine("[Auth] Usando claims básicos del JWT debido a error de red");
                        await CreateAuthenticationFromJwtOnly(jwtToken, userId);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Auth] Error en MarkUserAsAuthenticated: {ex.Message}");
                await MarkUserAsLoggedOutAsync();
            }
        }

        /// <summary>
        /// Notifica que el usuario ha cerrado sesión
        /// </summary>
        public async Task MarkUserAsLoggedOutAsync()
        {
            await RemoveTokenAsync();
            
            Console.WriteLine("[Auth] Usuario cerró sesión");
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymous)));
        }

        /// <summary>
        /// Obtiene el token JWT del localStorage
        /// </summary>
        private async Task<string?> GetTokenFromStorageAsync()
        {
            try
            {
                var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "access_token");
                Console.WriteLine($"[Auth] Token obtenido del storage: {(string.IsNullOrEmpty(token) ? "Vacío" : "Presente")}");
                return token;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Auth] Error obteniendo token: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Guarda el token JWT en el localStorage
        /// </summary>
        private async Task SetTokenInStorageAsync(string token)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "access_token", token);
                Console.WriteLine("[Auth] Token guardado en storage");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Auth] Error guardando token: {ex.Message}");
            }
        }

        /// <summary>
        /// Elimina el token JWT del localStorage
        /// </summary>
        private async Task RemoveTokenAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "access_token");
                Console.WriteLine("[Auth] Token removido del storage");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Auth] Error removiendo token: {ex.Message}");
            }
        }

        /// <summary>
        /// Construye la lista de claims desde un token JWT
        /// </summary>
        private List<Claim> BuildClaimsFromToken(JwtSecurityToken jwtToken)
        {
            var claims = new List<Claim>();

            var userId = jwtToken.Claims.FirstOrDefault(c => 
                c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value;
            var email = jwtToken.Claims.FirstOrDefault(c => 
                c.Type == ClaimTypes.Email || c.Type == "email")?.Value;
            var name = jwtToken.Claims.FirstOrDefault(c => 
                c.Type == ClaimTypes.Name || c.Type == "name")?.Value;
            var roles = jwtToken.Claims.Where(c => 
                c.Type == ClaimTypes.Role || c.Type == "role").Select(c => c.Value);

            if (!string.IsNullOrWhiteSpace(userId))
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));

            if (!string.IsNullOrWhiteSpace(email))
                claims.Add(new Claim(ClaimTypes.Email, email));

            if (!string.IsNullOrWhiteSpace(name))
                claims.Add(new Claim(ClaimTypes.Name, name));

            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            return claims;
        }

        /// <summary>
        /// Verifica que el token tenga los claims requeridos
        /// </summary>
        private bool HasRequiredClaims(IEnumerable<Claim> claims)
        {
            return _requiredClaims.All(required => 
                claims.Any(c => c.Type == required && !string.IsNullOrWhiteSpace(c.Value)));
        }

        /// <summary>
        /// Crea la autenticación usando solo los claims del JWT (sin consultar el perfil)
        /// </summary>
        private Task CreateAuthenticationFromJwtOnly(JwtSecurityToken jwtToken, string userId)
        {
            try
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim("usuario_id", userId)
                };

                // Obtener claims adicionales del JWT
                var email = jwtToken.Claims.FirstOrDefault(c => 
                    c.Type == ClaimTypes.Email || c.Type == "email")?.Value;
                var name = jwtToken.Claims.FirstOrDefault(c => 
                    c.Type == ClaimTypes.Name || c.Type == "name")?.Value;

                if (!string.IsNullOrWhiteSpace(email))
                    claims.Add(new Claim(ClaimTypes.Email, email));

                if (!string.IsNullOrWhiteSpace(name))
                    claims.Add(new Claim(ClaimTypes.Name, name));

                // Agregar roles
                foreach (var claim in jwtToken.Claims.Where(c => 
                    c.Type == ClaimTypes.Role || c.Type == "role"))
                {
                    claims.Add(new Claim(ClaimTypes.Role, claim.Value));
                }

                var identity = new ClaimsIdentity(claims, "jwt");
                var user = new ClaimsPrincipal(identity);

                Console.WriteLine($"[Auth] Usuario autenticado desde JWT: {user.Identity?.Name}");
                NotifyAuthenticationStateChanged(
                    Task.FromResult(new AuthenticationState(user)));

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Auth] Error creando autenticación desde JWT: {ex.Message}");
                return MarkUserAsLoggedOutAsync();
            }
        }
    }
}
