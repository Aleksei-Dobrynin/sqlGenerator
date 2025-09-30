using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SQLFileGenerator
{
    /// <summary>
    /// Простая система логирования с выводом в консоль и файл
    /// </summary>
    public static class Logger
    {
        private static string _logFilePath;
        private static readonly object _lockObj = new object();
        private static bool _isInitialized = false;

        /// <summary>
        /// Уровни логирования
        /// </summary>
        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error,
            Critical
        }

        /// <summary>
        /// Инициализация логгера
        /// </summary>
        public static void Initialize(string logPath = null)
        {
            if (_isInitialized) return;

            // Создаем директорию для логов если её нет
            var logsDir = logPath ?? "logs";
            Directory.CreateDirectory(logsDir);

            // Формируем имя файла с датой
            var fileName = $"parser_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log";
            _logFilePath = Path.Combine(logsDir, fileName);

            _isInitialized = true;

            LogInfo($"=== SQL File Generator with LLM Parser Started ===");
            LogInfo($"Log file: {_logFilePath}");
        }

        /// <summary>
        /// Логирование отладочной информации
        /// </summary>
        public static void LogDebug(string message)
        {
            Log(LogLevel.Debug, message, ConsoleColor.Gray);
        }

        /// <summary>
        /// Логирование информационных сообщений
        /// </summary>
        public static void LogInfo(string message)
        {
            Log(LogLevel.Info, message, ConsoleColor.White);
        }

        /// <summary>
        /// Логирование предупреждений
        /// </summary>
        public static void LogWarning(string message)
        {
            Log(LogLevel.Warning, message, ConsoleColor.Yellow);
        }

        /// <summary>
        /// Логирование ошибок
        /// </summary>
        public static void LogError(string message, Exception ex = null)
        {
            var fullMessage = ex != null
                ? $"{message}\nException: {ex.Message}\nStackTrace: {ex.StackTrace}"
                : message;

            Log(LogLevel.Error, fullMessage, ConsoleColor.Red);
        }

        /// <summary>
        /// Логирование критических ошибок
        /// </summary>
        public static void LogCritical(string message, Exception ex = null)
        {
            var fullMessage = ex != null
                ? $"{message}\nException: {ex.Message}\nStackTrace: {ex.StackTrace}"
                : message;

            Log(LogLevel.Critical, fullMessage, ConsoleColor.DarkRed);
        }

        /// <summary>
        /// Основной метод логирования
        /// </summary>
        private static void Log(LogLevel level, string message, ConsoleColor color)
        {
            if (!_isInitialized) Initialize();

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] [{level,-8}] {message}";

            // Вывод в консоль с цветом
            lock (_lockObj)
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine(logEntry);
                Console.ForegroundColor = originalColor;
            }

            // Запись в файл
            Task.Run(() => WriteToFile(logEntry));
        }

        /// <summary>
        /// Запись в файл логов
        /// </summary>
        private static void WriteToFile(string logEntry)
        {
            try
            {
                lock (_lockObj)
                {
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }

        /// <summary>
        /// Логирование JSON запроса/ответа
        /// </summary>
        public static void LogJson(string title, string json, bool truncate = true)
        {
            var maxLength = 1000;
            var displayJson = truncate && json.Length > maxLength
                ? json.Substring(0, maxLength) + "... [truncated]"
                : json;

            LogDebug($"{title}:\n{displayJson}");
        }

        /// <summary>
        /// Логирование статистики парсинга
        /// </summary>
        public static void LogParsingStats(int tablesCount, int totalColumns, int totalForeignKeys, TimeSpan elapsed)
        {
            var stats = new StringBuilder();
            stats.AppendLine("\n=== PARSING STATISTICS ===");
            stats.AppendLine($"Tables parsed: {tablesCount}");
            stats.AppendLine($"Total columns: {totalColumns}");
            stats.AppendLine($"Total foreign keys: {totalForeignKeys}");
            stats.AppendLine($"Time elapsed: {elapsed.TotalSeconds:F2} seconds");
            stats.AppendLine("==========================");

            LogInfo(stats.ToString());
        }
    }
}