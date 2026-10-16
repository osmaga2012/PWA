

using CRM.Dtos.Response;
using Microsoft.AspNetCore.Components.Forms;

namespace PWA.Interfaces
{
    public interface IApiClient<TDto> where TDto : class
    {
        Task<IEnumerable<TDto>> GetAllAsync(string endpoint, 
            Dictionary<string, string>? queryParams = null, params string[] includes);
        Task<TDto?> GetByIdAsync(string endpoint, object id);
        //Task<TDto?> CreateAsync(string endpoint, TDto dto);
        Task<ResponseDto> CreateAsync(string endpoint, TDto dto);
        Task<ResponseDto> UpdateAsync(string endpoint, TDto dto);
        Task<ResponseDto> UpdateAsync(string endpoint, TDto dto, object id);
        Task<ResponseDto> DeleteAsync(string endpoint, object id);
        Task<ResponseDto> UploadFileAsync(string endpoint, IBrowserFile file, long maxAllowedSize = 20 * 1024 * 1024, string formFieldName = "file");

    }

}
