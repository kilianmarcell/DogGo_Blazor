using doggo.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
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
        Task<List<Location>?> GetLocationsAsync();
        Task<List<Location>?> GetLocationsWithRatingsAsync();
        Task<Location?> GetLocationAsync(int id);
        Task<Location?> GetBestRatingAsync();
        Task<List<LocationRating>?> GetLocationRatingsAsync(int locationId);
        Task<bool> AddLocationRatingAsync(LocationRating rating);
        event Action<User?> UserChanged;
    }

    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        private readonly ILogger<ApiService> _logger;
        private readonly IConfiguration _configuration;
        private User? _currentUser;

        public event Action<User?> UserChanged = delegate { };

        public ApiService(HttpClient httpClient, ILocalStorageService localStorage, ILogger<ApiService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _logger = logger;
            _configuration = configuration;
            
            // Set base address if not already set
            if (_httpClient.BaseAddress == null)
            {
                var baseUrl = _configuration.GetValue<string>("ApiSettings:BaseUrl") ?? "http://127.0.0.1:8000/";
                _httpClient.BaseAddress = new Uri(baseUrl);
            }
        }

        public async Task<User?> GetCurrentUserAsync()
        {
            try
            {
                var token = await GetTokenFromStorage();
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("No authentication token found");
                    return null;
                }

                SetAuthorizationHeader(token);
                var response = await _httpClient.GetAsync("api/user");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    _currentUser = JsonSerializer.Deserialize<User>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    UserChanged.Invoke(_currentUser);
                    _logger.LogInformation("User successfully retrieved");
                    return _currentUser;
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized access, removing token");
                    await RemoveTokenFromStorage();
                    _currentUser = null;
                    UserChanged.Invoke(_currentUser);
                }
                else
                {
                    _logger.LogError("Failed to get current user. Status code: {StatusCode}", response.StatusCode);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request exception when getting current user");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timeout when getting current user");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when getting current user");
            }

            return null;
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                {
                    _logger.LogWarning("Login request with empty username or password");
                    return null;
                }

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync("api/login", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.Token))
                    {
                        await SaveTokenToStorage(loginResponse.Token);
                        _currentUser = loginResponse.User;
                        UserChanged.Invoke(_currentUser);
                        _logger.LogInformation("User successfully logged in: {Username}", request.Username);
                        return loginResponse;
                    }
                    else
                    {
                        _logger.LogWarning("Login response is null or missing token");
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Login failed. Status code: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request exception during login for user: {Username}", request.Username);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timeout during login for user: {Username}", request.Username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login for user: {Username}", request.Username);
            }

            return null;
        }

        public async Task<bool> RegisterAsync(RegisterRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
                {
                    _logger.LogWarning("Registration request with missing required fields");
                    return false;
                }

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync("api/register", content);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("User successfully registered: {Username}", request.Username);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Registration failed. Status code: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request exception during registration for user: {Username}", request.Username);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timeout during registration for user: {Username}", request.Username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during registration for user: {Username}", request.Username);
            }

            return false;
        }

        public async Task<bool> LogoutAsync()
        {
            try
            {
                var token = await GetTokenFromStorage();
                if (!string.IsNullOrEmpty(token))
                {
                    SetAuthorizationHeader(token);
                    var response = await _httpClient.PostAsync("api/logout", null);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("User successfully logged out");
                    }
                    else
                    {
                        _logger.LogWarning("Logout request failed. Status code: {StatusCode}", response.StatusCode);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request exception during logout");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timeout during logout");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during logout");
            }
            finally
            {
                await RemoveTokenFromStorage();
                _currentUser = null;
                UserChanged.Invoke(_currentUser);
                _logger.LogInformation("User session cleared");
            }

            return true;
        }

        public async Task<List<Location>?> GetLocationsAsync()
        {
            try
            {
                var token = await GetTokenFromStorage();
                if (!string.IsNullOrEmpty(token))
                {
                    SetAuthorizationHeader(token);
                }

                var response = await _httpClient.GetAsync("api/locations");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var locations = JsonSerializer.Deserialize<List<Location>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    _logger.LogInformation("Successfully retrieved {Count} locations", locations?.Count ?? 0);
                    return locations;
                }
                else
                {
                    _logger.LogWarning("Failed to get locations. Status code: {StatusCode}", response.StatusCode);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request exception when getting locations");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timeout when getting locations");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when getting locations");
            }

            return null;
        }

        public async Task<List<Location>?> GetLocationsWithRatingsAsync()
        {
            try
            {
                var locations = await GetLocationsAsync();
                if (locations == null) return null;

                // Get ratings for each location and calculate average
                var tasks = locations.Select(async location =>
                {
                    try
                    {
                        var ratings = await GetLocationRatingsAsync(location.Id);
                        if (ratings?.Count > 0)
                        {
                            location.AverageRating = ratings.Average(r => r.Rating);
                        }
                        return location;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get ratings for location {LocationId}", location.Id);
                        return location; // Return location without rating
                    }
                });

                var locationsWithRatings = await Task.WhenAll(tasks);
                _logger.LogInformation("Successfully retrieved {Count} locations with ratings", locationsWithRatings.Length);
                return locationsWithRatings.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting locations with ratings");
                return null;
            }
        }

        public async Task<Location?> GetLocationAsync(int id)
        {
            try
            {
                var token = await GetTokenFromStorage();
                if (!string.IsNullOrEmpty(token))
                {
                    SetAuthorizationHeader(token);
                }

                var response = await _httpClient.GetAsync($"api/locations/{id}");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var location = JsonSerializer.Deserialize<Location>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    _logger.LogInformation("Successfully retrieved location with ID: {Id}", id);
                    return location;
                }
                else
                {
                    _logger.LogWarning("Failed to get location with ID: {Id}. Status code: {StatusCode}", id, response.StatusCode);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request exception when getting location with ID: {Id}", id);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timeout when getting location with ID: {Id}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when getting location with ID: {Id}", id);
            }

            return null;
        }

        public async Task<Location?> GetBestRatingAsync()
        {
            try
            {
                var token = await GetTokenFromStorage();
                if (!string.IsNullOrEmpty(token))
                {
                    SetAuthorizationHeader(token);
                }

                var response = await _httpClient.GetAsync("api/locations/best");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var location = JsonSerializer.Deserialize<Location>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    _logger.LogInformation("Successfully retrieved best rated location");
                    return location;
                }
                else
                {
                    _logger.LogWarning("Failed to get best rated location. Status code: {StatusCode}", response.StatusCode);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request exception when getting best rated location");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timeout when getting best rated location");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when getting best rated location");
            }

            return null;
        }

        public async Task<List<LocationRating>?> GetLocationRatingsAsync(int locationId)
        {
            try
            {
                var response = await _httpClient.GetAsync("api/ratings");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var allRatings = JsonSerializer.Deserialize<List<Rating>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (allRatings != null)
                    {
                        // Filter ratings for specific location and convert to LocationRating format
                        var locationRatings = allRatings
                            .Where(r => r.LocationId == locationId)
                            .Select(r => new LocationRating
                            {
                                Id = r.Id,
                                LocationId = r.LocationId,
                                Rating = r.Stars,
                                Comment = r.Description,
                                Username = "User", // We don't have username in the rating object
                                CreatedAt = DateTime.Now // We don't have created_at in the rating object
                            })
                            .ToList();
                        
                        _logger.LogInformation("Successfully retrieved {Count} ratings for location ID: {LocationId}", locationRatings.Count, locationId);
                        return locationRatings;
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to get ratings. Status code: {StatusCode}", response.StatusCode);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request exception when getting ratings for location ID: {LocationId}", locationId);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timeout when getting ratings for location ID: {LocationId}", locationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when getting ratings for location ID: {LocationId}", locationId);
            }

            return null;
        }

        public async Task<bool> AddLocationRatingAsync(LocationRating rating)
        {
            try
            {
                var token = await GetTokenFromStorage();
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Cannot add rating: user not authenticated");
                    return false;
                }

                SetAuthorizationHeader(token);

                var json = JsonSerializer.Serialize(rating);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync("api/ratings", content);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully added rating for location ID: {LocationId}", rating.LocationId);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to add rating for location ID: {LocationId}. Status code: {StatusCode}, Error: {Error}", rating.LocationId, response.StatusCode, errorContent);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request exception when adding rating for location ID: {LocationId}", rating.LocationId);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timeout when adding rating for location ID: {LocationId}", rating.LocationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when adding rating for location ID: {LocationId}", rating.LocationId);
            }

            return false;
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

        private async Task<string> GetErrorMessageFromResponse(HttpResponseMessage response)
        {
            try
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            catch
            {
                return $"HTTP {(int)response.StatusCode} - {response.ReasonPhrase}";
            }
        }
    }
}