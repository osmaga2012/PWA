using CRM.Dtos.Login;
using CRM.Dtos.Response;

namespace PWA.Interfaces
{
    public interface IAuthService
    {
        event Action OnLoginSuccess;
        void LoginOk();
        string? LastErrorMessage { get; }
        Task<LoginResultDto> LoginAsync(string email, string password);
        Task<ResponseDto> RequestPasswordRecoveryAsync(string email);
        Task<ResponseDto> ConfirmPasswordResetAsync(Guid token, string newPassword);
        Task<string?> GetAccessTokenAsync();
        Task LogoutAsync();
    }
}
