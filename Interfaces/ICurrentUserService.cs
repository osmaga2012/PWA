using CRM.Dtos;
using System.Security.Claims;

namespace PWA.Interfaces
{
    public interface ICurrentUserService
    {
        Task<UsuarioDto> GetCurrentUserAsync(ClaimsPrincipal userPrincipal); // ¡Ahora devuelve UsuarioDto!
        void ClearCachedUser();
    }
}
