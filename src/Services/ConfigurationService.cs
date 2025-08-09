using BanYodo.Models;
using Newtonsoft.Json;

namespace BanYodo.Services
{
    public class ConfigurationService : IDisposable
    {
        private readonly string _configFilePath;
        private Configuration _currentConfiguration;
        private readonly SemaphoreSlim _saveSemaphore;
        private readonly System.Threading.Timer _saveDelayTimer;
        private bool _pendingSave = false;
        private bool _disposed = false;

        public ConfigurationService()
        {
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            _currentConfiguration = new Configuration();
            _saveSemaphore = new SemaphoreSlim(1, 1);
            _saveDelayTimer = new System.Threading.Timer(SaveDelayCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        public Configuration GetConfiguration()
        {
            return _currentConfiguration;
        }

        public async Task LoadConfigurationAsync()
        {
            const int maxRetries = 3;
            const int delayMs = 100;
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    if (File.Exists(_configFilePath))
                    {
                        // Use FileStream with sharing options to avoid conflicts
                        using var fileStream = new FileStream(_configFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var reader = new StreamReader(fileStream);
                        var json = await reader.ReadToEndAsync();
                        _currentConfiguration = Configuration.FromJson(json);
                    }
                    return; // Success
                }
                catch (IOException) when (attempt < maxRetries - 1)
                {
                    // File is being used by another process, wait and retry
                    await Task.Delay(delayMs * (attempt + 1));
                }
                catch (Exception)
                {
                    // Log error here if logging service is implemented
                    _currentConfiguration = new Configuration();
                    return;
                }
            }
            
            // If all retries failed, use default configuration
            _currentConfiguration = new Configuration();
        }

        public async Task SaveConfigurationAsync()
        {
            await _saveSemaphore.WaitAsync();
            try
            {
                const int maxRetries = 5;
                const int delayMs = 50;
                
                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    try
                    {
                        var json = _currentConfiguration.ToJson();
                        var tempFile = _configFilePath + ".tmp";
                        
                        // Write to temp file first
                        await File.WriteAllTextAsync(tempFile, json);
                        
                        // Then atomically replace the original file
                        if (File.Exists(_configFilePath))
                        {
                            File.Delete(_configFilePath);
                        }
                        File.Move(tempFile, _configFilePath);
                        
                        return; // Success
                    }
                    catch (IOException) when (attempt < maxRetries - 1)
                    {
                        // File is being used by another process, wait and retry
                        await Task.Delay(delayMs * (attempt + 1));
                    }
                    catch (UnauthorizedAccessException) when (attempt < maxRetries - 1)
                    {
                        // File access denied, wait and retry
                        await Task.Delay(delayMs * (attempt + 1));
                    }
                    catch (Exception ex)
                    {
                        // Log error here if logging service is implemented
                        throw new InvalidOperationException($"Failed to save configuration: {ex.Message}", ex);
                    }
                }
                
                throw new InvalidOperationException($"Failed to save configuration after {maxRetries} attempts: File access conflict");
            }
            finally
            {
                _saveSemaphore.Release();
            }
        }

        public async Task SaveConfigurationAsync(Configuration configuration)
        {
            _currentConfiguration = configuration ?? new Configuration();
            await SaveConfigurationAsync();
        }

        public void UpdateConfiguration(Configuration configuration)
        {
            _currentConfiguration = configuration ?? new Configuration();
        }

        public async Task AddAccountAsync(Account account)
        {
            _currentConfiguration.AddAccount(account);
            await SaveConfigurationAsync();
        }

        public async Task RemoveAccountAsync(Account account)
        {
            _currentConfiguration.RemoveAccount(account);
            await SaveConfigurationAsync();
        }

        public async Task AddProductIdAsync(string productId)
        {
            _currentConfiguration.AddProductId(productId);
            await SaveConfigurationAsync();
        }

        public async Task RemoveProductIdAsync(string productId)
        {
            _currentConfiguration.RemoveProductId(productId);
            await SaveConfigurationAsync();
        }

        public bool ValidateProxy(string proxy)
        {
            if (string.IsNullOrWhiteSpace(proxy))
                return true; // Empty proxy is valid (no proxy)

            // Basic validation for proxy format: ip:port or ip:port:username:password
            var parts = proxy.Split(':');
            
            if (parts.Length < 2 || parts.Length > 4)
                return false;

            // Validate IP
            if (!System.Net.IPAddress.TryParse(parts[0], out _))
                return false;

            // Validate port
            if (!int.TryParse(parts[1], out int port) || port <= 0 || port > 65535)
                return false;

            return true;
        }

        public bool ValidateAccountFormat(string accountInfo)
        {
            if (string.IsNullOrWhiteSpace(accountInfo))
                return false;

            var parts = accountInfo.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2;
        }

        public bool ValidateCardInfo(string card, string month, string year, string cvv)
        {
            // Basic card validation
            if (string.IsNullOrWhiteSpace(card) || 
                string.IsNullOrWhiteSpace(month) || 
                string.IsNullOrWhiteSpace(year) || 
                string.IsNullOrWhiteSpace(cvv))
                return false;

            // Validate card number (basic length check)
            if (card.Length < 13 || card.Length > 19)
                return false;

            // Validate month
            if (!int.TryParse(month, out int monthInt) || monthInt < 1 || monthInt > 12)
                return false;

            // Validate year
            if (!int.TryParse(year, out int yearInt) || yearInt < DateTime.Now.Year)
                return false;

            // Validate CVV
            if (cvv.Length < 3 || cvv.Length > 4)
                return false;

            return true;
        }

        private async void SaveDelayCallback(object? state)
        {
            if (_pendingSave && !_disposed)
            {
                _pendingSave = false;
                try
                {
                    await SaveConfigurationAsync();
                }
                catch
                {
                    // Ignore errors in background save
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _saveDelayTimer?.Dispose();
                _saveSemaphore?.Dispose();
                
                // Clean up any temp files
                try
                {
                    var tempFile = _configFilePath + ".tmp";
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
                
                _disposed = true;
            }
        }
    }
}
