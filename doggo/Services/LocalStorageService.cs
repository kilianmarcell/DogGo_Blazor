using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;

namespace doggo.Services
{
    public interface ILocalStorageService
    {
        Task<string?> GetItemAsync(string key);
        Task SetItemAsync(string key, string value);
        Task RemoveItemAsync(string key);
    }

    public class LocalStorageService : ILocalStorageService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<LocalStorageService> _logger;

        public LocalStorageService(IJSRuntime jsRuntime, ILogger<LocalStorageService> logger)
        {
            _jsRuntime = jsRuntime;
            _logger = logger;
        }

        public async Task<string?> GetItemAsync(string key)
        {
            try
            {
                // Check if JavaScript interop is available (not during prerendering)
                if (_jsRuntime is IJSInProcessRuntime)
                {
                    var result = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
                    _logger.LogDebug("Retrieved item from localStorage: {Key}", key);
                    return result;
                }
                else
                {
                    // During prerendering, JS interop is not available
                    _logger.LogDebug("JavaScript interop not available (prerendering): {Key}", key);
                    return null;
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("JavaScript interop calls cannot be issued"))
            {
                _logger.LogDebug("JavaScript interop not available during prerendering for key: {Key}", key);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving item from localStorage: {Key}", key);
                return null;
            }
        }

        public async Task SetItemAsync(string key, string value)
        {
            try
            {
                // Check if JavaScript interop is available (not during prerendering)
                if (_jsRuntime is IJSInProcessRuntime)
                {
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
                    _logger.LogDebug("Saved item to localStorage: {Key}", key);
                }
                else
                {
                    _logger.LogDebug("JavaScript interop not available (prerendering), skipping localStorage set: {Key}", key);
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("JavaScript interop calls cannot be issued"))
            {
                _logger.LogDebug("JavaScript interop not available during prerendering, skipping localStorage set: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving item to localStorage: {Key}", key);
            }
        }

        public async Task RemoveItemAsync(string key)
        {
            try
            {
                // Check if JavaScript interop is available (not during prerendering)
                if (_jsRuntime is IJSInProcessRuntime)
                {
                    await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
                    _logger.LogDebug("Removed item from localStorage: {Key}", key);
                }
                else
                {
                    _logger.LogDebug("JavaScript interop not available (prerendering), skipping localStorage remove: {Key}", key);
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("JavaScript interop calls cannot be issued"))
            {
                _logger.LogDebug("JavaScript interop not available during prerendering, skipping localStorage remove: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing item from localStorage: {Key}", key);
            }
        }
    }
}