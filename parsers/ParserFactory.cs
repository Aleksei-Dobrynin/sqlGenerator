using System;
using Microsoft.Extensions.Configuration;

namespace SQLFileGenerator
{
    /// <summary>
    /// Режимы работы парсера
    /// </summary>
    public enum ParserMode
    {
        /// <summary>
        /// Стандартный парсер на регулярных выражениях
        /// </summary>
        Standard,

        /// <summary>
        /// Парсер на основе LLM
        /// </summary>
        LLM
    }

    /// <summary>
    /// Фабрика для создания парсеров
    /// </summary>
    public static class ParserFactory
    {
        /// <summary>
        /// Создает парсер указанного типа
        /// </summary>
        /// <param name="mode">Режим парсера</param>
        /// <param name="configuration">Конфигурация (необходима для LLM парсера)</param>
        /// <returns>Экземпляр парсера</returns>
        public static IParser CreateParser(ParserMode mode, IConfiguration configuration = null)
        {
            Logger.LogInfo($"Creating parser in {mode} mode");

            return mode switch
            {
                ParserMode.LLM => new LLMParserAdapter(configuration),
                ParserMode.Standard => new StandardParserAdapter(),
                _ => new StandardParserAdapter()
            };
        }

        /// <summary>
        /// Преобразует строку в режим парсера
        /// </summary>
        public static ParserMode ParseMode(string modeString)
        {
            if (string.IsNullOrWhiteSpace(modeString))
                return ParserMode.Standard;

            return modeString.ToLower() switch
            {
                "llm" => ParserMode.LLM,
                "standard" => ParserMode.Standard,
                _ => ParserMode.Standard
            };
        }
    }
}