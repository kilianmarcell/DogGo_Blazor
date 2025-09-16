using doggo.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace doggo.Services
{
    public interface IApiService
    {
        Task<User?> GetCurrentUserAsync();
        Task<LoginResponse?> LoginAsync(LoginRequest request);
        Task<bool> RegisterAsync(RegisterRequest request);
        Task<bool> LogoutAsync();
        event Action<User?> UserChanged;
    }

    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        private User? _currentUser;

        public event Action<User?> UserChanged = delegate { };

        public ApiService(HttpClient httpClient, ILocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _httpClient.BaseAddress = new Uri("http://127.0.0.1:8000/");
        }

        public async Task<User?> GetCurrentUserAsync()
        {
            try
            {
                var token = await GetTokenFromStorage();
                if (string.IsNullOrEmpty(token))
                    return null;

                SetAuthorizationHeader(token);
                var response = await _httpClient.GetAsync("api/user");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    _currentUser = JsonSerializer.Deserialize<User>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    UserChanged.Invoke(_currentUser);
                    return _currentUser;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    await RemoveTokenFromStorage();
                    _currentUser = null;
                    UserChanged.Invoke(_currentUser);
                }
            }
            catch (Exception)
            {
                // Log error if needed
            }

            return null;
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            try
            {
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync("api/login", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (loginResponse != null)
                    {
                        await SaveTokenToStorage(loginResponse.Token);
                        _currentUser = loginResponse.User;
                        UserChanged.Invoke(_currentUser);
                        return loginResponse;
                    }
                }
            }
            catch (Exception)
            {
                // Log error if needed
            }

            return null;
        }

        public async Task<bool> RegisterAsync(RegisterRequest request)
        {
            try
            {
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync("api/register", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                // Log error if needed
                return false;
            }
        }

        public async Task<bool> LogoutAsync()
        {
            try
            {
                var token = await GetTokenFromStorage();
                if (!string.IsNullOrEmpty(token))
                {
                    SetAuthorizationHeader(token);
                    await _httpClient.PostAsync("api/logout", null);
                }
            }
            catch (Exception)
            {
                // Log error if needed
            }
            finally
            {
                await RemoveTokenFromStorage();
                _currentUser = null;
                UserChanged.Invoke(_currentUser);
            }

            return true;
        }

        private void SetAuthorizationHeader(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        private async Task<string?> GetTokenFromStorage()
        {
            return await _localStorage.GetItemAsync("token");
        }

        private async Task SaveTokenToStorage(string token)
        {
            await _localStorage.SetItemAsync("token", token);
        }

        private async Task RemoveTokenFromStorage()
        {
            await _localStorage.RemoveItemAsync("token");
        }
    }
}