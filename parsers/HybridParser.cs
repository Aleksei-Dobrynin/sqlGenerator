using Microsoft.Extensions.Configuration;
using SQLFileGenerator.structures;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SQLFileGenerator
{
    /// <summary>
    /// Гибридный парсер, который пробует LLM, а при ошибке использует стандартный
    /// </summary>
    public class HybridParser : IParser
    {
        private readonly IParser _primaryParser;
        private readonly IParser _fallbackParser;
        private readonly bool _preferLLM;

        public string Name => "Hybrid Parser";

        public HybridParser(IConfiguration configuration, bool preferLLM = true)
        {
            _preferLLM = preferLLM;

            if (_preferLLM)
            {
                // Сначала пробуем LLM, потом стандартный
                _primaryParser = new LLMParserAdapter(configuration);
                _fallbackParser = new StandardParserAdapter();
            }
            else
            {
                // Сначала пробуем стандартный, потом LLM
                _primaryParser = new StandardParserAdapter();
                _fallbackParser = new LLMParserAdapter(configuration);
            }
        }

        /// <summary>
        /// Парсит с использованием primary парсера, при ошибке переключается на fallback
        /// </summary>
        public async Task<List<TableSchema>> ParseAsync(string sqlScript)
        {
            Console.WriteLine($"Using {Name} (Primary: {_primaryParser.Name}, Fallback: {_fallbackParser.Name})");

            try
            {
                // Пробуем primary парсер
                var result = await _primaryParser.ParseAsync(sqlScript);

                // Проверяем результат
                if (result != null && result.Count > 0)
                {
                    Console.WriteLine($"Successfully parsed with {_primaryParser.Name}");
                    return result;
                }

                Console.WriteLine($"{_primaryParser.Name} returned no results, switching to fallback");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{_primaryParser.Name} failed: {ex.Message}");
                Console.WriteLine($"Switching to fallback parser: {_fallbackParser.Name}");
            }

            // Используем fallback парсер
            try
            {
                var fallbackResult = await _fallbackParser.ParseAsync(sqlScript);

                if (fallbackResult != null && fallbackResult.Count > 0)
                {
                    Console.WriteLine($"Successfully parsed with fallback {_fallbackParser.Name}");
                    return fallbackResult;
                }

                Console.WriteLine("Both parsers returned no results");
                return new List<TableSchema>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fallback parser also failed: {ex.Message}");
                throw new AggregateException(
                    $"Both parsers failed. {_primaryParser.Name} and {_fallbackParser.Name} couldn't parse the SQL script",
                    ex);
            }
        }
    }
}