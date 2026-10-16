using CRM.Dtos.Response;
using Microsoft.AspNetCore.Components.Forms;
using PWA.Interfaces;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CRM.App.Sharted.Services
{
    public class ApiClient<TDto> : IApiClient<TDto> where TDto : class
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpClientFactory httpClientFactory;

        public ApiClient(HttpClient httpClient,IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
            //_httpClient = httpClientFactory.CreateClient("ApiCRM");
            _httpClient = httpClientFactory.CreateClient("ApiClient");

        }

        public async Task<IEnumerable<TDto>> GetAllAsync(string endpoint,
            Dictionary<string, string>? queryParams = null, params string[] includes)
        {
            try
            {
                string requestUrl = endpoint;
                var allQueryParams = new Dictionary<string, string>();

                // Añade los queryParams existentes
                if (queryParams != null)
                {
                    foreach (var kvp in queryParams)
                    {
                        allQueryParams[kvp.Key] = kvp.Value;
                    }
                }

                // Añade el parámetro de includes si existe
                if (includes != null && includes.Any())
                {
                    // Convierte la lista de includes en una cadena separada por comas
                    // Por ejemplo: "Barco,Empresa"
                    allQueryParams["includes"] = string.Join(",", includes);
                }

                // Si se proporcionan parámetros (incluyendo los nuevos 'includes'), los añade a la URL como query string
                if (allQueryParams.Any())
                {
                    var queryString = string.Join("&", allQueryParams.Select(kvp =>
                        $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"
                    ));
                    requestUrl = $"{requestUrl}?{queryString}";
                }

                var response = await _httpClient.GetAsync(requestUrl);
                if (response.StatusCode == HttpStatusCode.NoContent ||
                    response.Content.Headers.ContentLength == 0)
                {
                    return Enumerable.Empty<TDto>();
                }
                try
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Respuesta cruda del servidor: {jsonString}");
                    return await response.Content.ReadFromJsonAsync<IEnumerable<TDto>>(); //?? Enumerable.Empty<TDto>();

                }
                catch (OperationCanceledException error)
                {
                    Console.WriteLine($"Error: {error.Message}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error fetching data from {endpoint}: {ex.Message}");
                throw;
            }


            //return await _httpClient.GetFromJsonAsync<IEnumerable<TDto>>(endpoint) ?? Enumerable.Empty<TDto>();
        }

        public async Task<TDto?> GetByIdAsync(string endpoint, object id)
        {
            var response = await _httpClient.GetAsync($"{endpoint}/{id}");

            if (response.StatusCode == HttpStatusCode.NoContent ||
                response.StatusCode == HttpStatusCode.NotFound ||
                response.Content.Headers.ContentLength == 0)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            return JsonSerializer.Deserialize<TDto>(
                content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public async Task<ResponseDto> CreateAsync(string endpoint, TDto dto)
        {
            var response = await _httpClient.PostAsJsonAsync(endpoint, dto);
            return await ReadResponseDtoAsync(response);
        }
        public async Task<ResponseDto> UpdateAsync(string endpoint, TDto dto)
        {
            var response = await _httpClient.PutAsJsonAsync($"{endpoint}", dto);
            return await ReadResponseDtoAsync(response);
        }

        public async Task<ResponseDto> UpdateAsync(string endpoint, TDto dto, object id)
        {
            var response = await _httpClient.PutAsJsonAsync($"{endpoint}/{id}", dto);
            return await ReadResponseDtoAsync(response);
        }

        public async Task<ResponseDto> DeleteAsync(string endpoint, object id)
        {
            var response = await _httpClient.DeleteAsync($"{endpoint}/{id}");
            return await ReadResponseDtoAsync(response);
        }

        public async Task<ResponseDto> UploadFileAsync(string endpoint, IBrowserFile file, long maxAllowedSize = 20 * 1024 * 1024, string formFieldName = "file")
        {
            ArgumentNullException.ThrowIfNull(file);

            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(file.OpenReadStream(maxAllowedSize));
            var mediaType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            content.Add(fileContent, formFieldName, file.Name);

            var response = await _httpClient.PostAsync(endpoint, content);
            return await ReadResponseDtoAsync(response) ?? new ResponseDto { Success = true };
        }

        private async Task<ResponseDto?> ReadResponseDtoAsync(HttpResponseMessage response)
        {
            if (response == null) return null;

            var content = string.Empty;
            try
            {
                content = await response.Content.ReadAsStringAsync();
            }
            catch
            {
                // ignore
            }

            ResponseDto? dto = null;
            if (!string.IsNullOrWhiteSpace(content))
            {
                try
                {
                    dto = JsonSerializer.Deserialize<ResponseDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch
                {
                    // ignore parse errors
                }
            }

            if (dto != null)
            {
                return dto;
            }

            // If server returned non-success, include reason and body to help diagnosis
            if (!response.IsSuccessStatusCode)
            {
                var message = !string.IsNullOrWhiteSpace(content) ? content : response.ReasonPhrase;
                return new ResponseDto { Success = false, Message = message };
            }

            // If success but no content or couldn't parse, return generic success
            return new ResponseDto { Success = true, Message = content };
        }

    }
}
