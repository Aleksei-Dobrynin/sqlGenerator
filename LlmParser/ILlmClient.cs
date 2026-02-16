namespace SQLFileGenerator.LlmParser
{
    /// <summary>
    /// Интерфейс для HTTP клиента LLM API
    /// </summary>
    public interface ILlmClient
    {
        /// <summary>
        /// Отправляет запрос на completion к LLM API
        /// </summary>
        /// <param name="systemPrompt">Системный промпт с инструкциями</param>
        /// <param name="userPrompt">Пользовательский промпт с SQL</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Ответ модели (JSON строка)</returns>
        Task<string> SendCompletionRequestAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Выполняет warmup запрос для предзагрузки модели в память (Ollama)
        /// </summary>
        /// <param name="cancellationToken">Токен отмены</param>
        Task WarmupAsync(CancellationToken cancellationToken = default);
    }
}
