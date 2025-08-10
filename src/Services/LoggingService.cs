namespace BanYodo.Services
{
    public class LoggingService
    {
        private readonly string _logFilePath;
        private readonly object _lockObject = new object();

        public LoggingService()
        {
            var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
            
            _logFilePath = Path.Combine(logDirectory, $"app_{DateTime.Now:yyyyMMdd}.log");
        }

        public void LogInfo(string message, string? context = null)
        {
            WriteLog("INFO", message, context);
        }

        public void LogWarning(string message, string? context = null)
        {
            WriteLog("WARN", message, context);
        }

        public void LogError(string message, Exception? exception = null, string? context = null)
        {
            var fullMessage = exception != null ? $"{message} | Exception: {exception.Message}\n{exception.StackTrace}" : message;
            WriteLog("ERROR", fullMessage, context);
        }

        public void LogDebug(string message, string? context = null)
        {
            WriteLog("DEBUG", message, context);
        }

        public string GetCurrentTime()
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            return $"[{timestamp}]";
        }

        private void WriteLog(string level, string message, string? context)
        {
            try
            {
                lock (_lockObject)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var contextInfo = !string.IsNullOrEmpty(context) ? $" [{context}]" : "";
                    var logEntry = $"[{timestamp}] [{level}]{contextInfo} {message}";
                    
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }
            }
            catch
            {
                // Ignore logging errors to prevent application crashes
            }
        }

        public List<string> GetRecentLogs(int lineCount = 100)
        {
            try
            {
                if (!File.Exists(_logFilePath))
                    return new List<string>();

                var lines = File.ReadAllLines(_logFilePath);
                return lines.TakeLast(lineCount).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public void ClearOldLogs(int daysToKeep = 7)
        {
            try
            {
                var logDirectory = Path.GetDirectoryName(_logFilePath);
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                
                var oldLogFiles = Directory.GetFiles(logDirectory, "app_*.log")
                    .Where(file => File.GetCreationTime(file) < cutoffDate);
                
                foreach (var file in oldLogFiles)
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
