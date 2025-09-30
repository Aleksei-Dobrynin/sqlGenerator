using SQLFileGenerator.structures;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SQLFileGenerator
{
    /// <summary>
    /// Интерфейс для всех типов парсеров SQL-скриптов
    /// </summary>
    public interface IParser
    {
        /// <summary>
        /// Асинхронно парсит SQL-скрипт и возвращает структуру таблиц
        /// </summary>
        /// <param name="sqlScript">SQL-скрипт для парсинга</param>
        /// <returns>Список распарсенных таблиц</returns>
        Task<List<TableSchema>> ParseAsync(string sqlScript);

        /// <summary>
        /// Название парсера для логирования
        /// </summary>
        string Name { get; }
    }
}