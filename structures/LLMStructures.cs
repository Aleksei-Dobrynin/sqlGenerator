namespace SQLFileGenerator
{
    /// <summary>
    /// Конфигурация для LLM парсера
    /// </summary>
    public class LLMParserConfig
    {
        /// <summary>
        /// Включен ли LLM парсер
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Endpoint для OpenAI-совместимого API
        /// </summary>
        public string ApiEndpoint { get; set; } = "http://localhost:11434/v1/chat/completions";

        /// <summary>
        /// Название модели
        /// </summary>
        public string Model { get; set; } = "codellama:7b";

        /// <summary>
        /// Температура для генерации (0.0 - 1.0)
        /// </summary>
        public double Temperature { get; set; } = 0.1;

        /// <summary>
        /// Максимальное количество токенов в ответе
        /// </summary>
        public int MaxTokens { get; set; } = 8192;

        /// <summary>
        /// Таймаут запроса в секундах
        /// </summary>
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Количество попыток при ошибке
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// Задержка между попытками в миллисекундах
        /// </summary>
        public int RetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Системный промпт для модели
        /// </summary>
        public string SystemPrompt { get; set; } = "";

        /// <summary>
        /// Формат ответа (json_object для структурированного вывода)
        /// </summary>
        public string ResponseFormat { get; set; } = "json_object";

        /// <summary>
        /// Логировать ли запросы и ответы
        /// </summary>
        public bool LogRequests { get; set; } = false;

        /// <summary>
        /// Кэшировать ли ответы
        /// </summary>
        public bool CacheResponses { get; set; } = true;
    }

    /// <summary>
    /// Общая конфигурация парсера
    /// </summary>
    public class ParserConfig
    {
        /// <summary>
        /// Режим парсера по умолчанию
        /// </summary>
        public string DefaultMode { get; set; } = "Standard";

        /// <summary>
        /// Показывать ли индикатор прогресса
        /// </summary>
        public bool ShowProgressIndicator { get; set; } = true;
    }
}