using CRM.Dtos.Login;
using CRM.Dtos.Request;
using CRM.Dtos.Response;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using PWA.Interfaces;
using PWA.Providers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace PWA.Servicios
{
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly CustomAuthStateProvider _authStateProvider;
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly IJSRuntime _jsRuntime;

        public string? LastErrorMessage { get; private set; }

        public event Action? OnLoginSuccess;

        public AuthService(
            HttpClient httpClient,
            AuthenticationStateProvider authenticationStateProvider,
            IJSRuntime jsRuntime)
        {
            _httpClient = httpClient;
            _authenticationStateProvider = authenticationStateProvider;
            _authStateProvider = (CustomAuthStateProvider)authenticationStateProvider;
            _jsRuntime = jsRuntime;
        }

        public async Task<LoginResultDto> LoginAsync(string email, string password)
        {
            LastErrorMessage = null;

            try
            {
                var loginRequest = new { Email = email, Password = password };
                var response = await _httpClient.PostAsJsonAsync("/api/Auth/login", loginRequest);

                if (response.IsSuccessStatusCode)
                {
                    var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();

                    if (loginResponse?.access_token != null)
                    {
                        await _authStateProvider.MarkUserAsAuthenticatedAsync(loginResponse.access_token);
                        OnLoginSuccess?.Invoke();

                        return new LoginResultDto { IsSuccess = true };
                    }
                }

                var errorMessage = await response.Content.ReadAsStringAsync();
                LastErrorMessage = errorMessage ?? "Error al iniciar sesion";

                return new LoginResultDto
                {
                    IsSuccess = false,
                    Message = LastErrorMessage
                };
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"Error de conexion: {ex.Message}";

                return new LoginResultDto
                {
                    IsSuccess = false,
                    Message = LastErrorMessage
                };
            }
        }

        public async Task<ResponseDto> RequestPasswordRecoveryAsync(string email)
        {
            LastErrorMessage = null;

            try
            {
                var request = new PasswordRecoveryRequestDto { Email = email };
                var response = await _httpClient.PostAsJsonAsync("/api/Usuarios/solicitar-recuperacion-contrasena", request);
                return await ReadResponseDtoAsync(response);
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"Error de conexion: {ex.Message}";
                return new ResponseDto
                {
                    Success = false,
                    Message = LastErrorMessage
                };
            }
        }

        public async Task<ResponseDto> ConfirmPasswordResetAsync(Guid token, string newPassword)
        {
            LastErrorMessage = null;

            try
            {
                var request = new ActivationRequestDto
                {
                    Token = token,
                    NewPassword = newPassword
                };

                var response = await _httpClient.PostAsJsonAsync("/api/Usuarios/ConfirmarActivacion", request);
                return await ReadResponseDtoAsync(response);
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"Error de conexion: {ex.Message}";
                return new ResponseDto
                {
                    Success = false,
                    Message = LastErrorMessage
                };
            }
        }

        public async Task LogoutAsync()
        {
            LastErrorMessage = null;
            await _authStateProvider.MarkUserAsLoggedOutAsync();
        }

        public async Task<string?> GetAccessTokenAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "access_token");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetAccessTokenAsync] Error: {ex.Message}");
                return null;
            }
        }

        public void LoginOk()
        {
            OnLoginSuccess?.Invoke();
        }

        public async Task<UserInfo?> GetCurrentUserAsync()
        {
            var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (user.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return new UserInfo
            {
                UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
                Name = user.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty,
                Email = user.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
                TipoUsuario = user.FindFirst("tipo_usuario")?.Value ?? string.Empty,
                Roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList()
            };
        }

        private static async Task<ResponseDto> ReadResponseDtoAsync(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();

            if (!string.IsNullOrWhiteSpace(content))
            {
                try
                {
                    var dto = JsonSerializer.Deserialize<ResponseDto>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (dto != null)
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            dto.Success = false;
                        }

                        return dto;
                    }
                }
                catch
                {
                }
            }

            if (response.IsSuccessStatusCode)
            {
                return new ResponseDto
                {
                    Success = true,
                    Message = content
                };
            }

            return new ResponseDto
            {
                Success = false,
                Message = string.IsNullOrWhiteSpace(content)
                    ? response.ReasonPhrase ?? "Error en la solicitud."
                    : content
            };
        }
    }

    public class UserInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string TipoUsuario { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
    }
}
