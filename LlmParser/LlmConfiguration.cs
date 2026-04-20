namespace SQLFileGenerator.LlmParser
{
    /// <summary>
    /// Конфигурация для LLM парсера SQL.
    /// Загружается из appsettings.json секции "LlmParser".
    /// </summary>
    public class LlmConfiguration
    {
        /// <summary>
        /// URL API (OpenAI-совместимый endpoint).
        /// Примеры: http://localhost:11434/v1 (Ollama), https://api.openai.com/v1 (OpenAI)
        /// </summary>
        public string ApiUrl { get; set; } = "http://localhost:11434/v1";

        /// <summary>
        /// API ключ (пустой для локального Ollama)
        /// </summary>
        public string ApiKey { get; set; } = "";

        /// <summary>
        /// Имя модели (например: llama3.2, gpt-4o-mini, mistral)
        /// </summary>
        public string ModelName { get; set; } = "llama3.2";

        /// <summary>
        /// Максимальное количество токенов в ответе
        /// </summary>
        public int MaxTokens { get; set; } = 4096;

        /// <summary>
        /// Температура генерации (0.0-1.0). Низкая = более детерминированный вывод
        /// </summary>
        public double Temperature { get; set; } = 0.1;

        /// <summary>
        /// Максимальное количество таблиц в одном chunk для больших БД.
        /// 0 = отключить чанкование (весь SQL в одном запросе, рекомендуется для моделей с большим контекстом).
        /// </summary>
        public int MaxTablesPerChunk { get; set; } = 0;

        /// <summary>
        /// Максимальное количество повторных попыток при ошибках
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Базовая задержка между повторными попытками (мс)
        /// </summary>
        public int RetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Таймаут запроса к API (секунды). По умолчанию 300 для локальных моделей
        /// </summary>
        public int TimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// Выполнить warmup запрос для предзагрузки модели в память (для Ollama).
        /// Полезно при первом запуске, когда модель ещё не загружена.
        /// </summary>
        public bool WarmupModel { get; set; } = false;

        /// <summary>
        /// Таймаут для warmup запроса (секунды). Может быть больше основного таймаута.
        /// </summary>
        public int WarmupTimeoutSeconds { get; set; } = 600;
    }
}
