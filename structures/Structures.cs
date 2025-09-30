using Scriban.Functions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLFileGenerator.structures
{
    /// <summary>
    /// Представляет структуру таблицы базы данных, включая её имя, соответствующее имя сущности,
    /// а также коллекции столбцов и внешних ключей. Используется для генерации кода на основе шаблонов.
    /// </summary>
    public class TableSchema
    {
        /// <summary>
        /// Имя таблицы в базе данных (например, "user_profiles")
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Имя сущности в формате PascalCase (например, "UserProfile")
        /// </summary>
        public string EntityName { get; set; }

        /// <summary>
        /// Коллекция столбцов таблицы
        /// </summary>
        public List<ColumnSchema> Columns { get; set; } = new();

        /// <summary>
        /// Коллекция внешних ключей таблицы
        /// </summary>
        public List<ForeignKeyInfo> ForeignKeys { get; set; } = new();
    }

    /// <summary>
    /// Представляет информацию о столбце таблицы базы данных, 
    /// включая имя, тип C#, и флаги для первичного и внешнего ключей.
    /// </summary>
    public class ColumnSchema
    {
        /// <summary>
        /// Имя столбца в базе данных
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Тип данных C#, соответствующий типу столбца в базе данных
        /// </summary>
        public string CSharpType { get; set; }

        /// <summary>
        /// Флаг, указывающий является ли столбец первичным ключом
        /// </summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>
        /// Флаг, указывающий является ли столбец внешним ключом
        /// </summary>
        public bool IsForeignKey { get; set; }

        public bool IsNullable { get; set; }
    }

    /// <summary>
    /// Представляет информацию о внешнем ключе, включая имя столбца, 
    /// тип данных C#, и ссылки на связанную таблицу и столбец.
    /// </summary>
    public class ForeignKeyInfo
    {
        /// <summary>
        /// Имя столбца с внешним ключом
        /// </summary>
        public string ColumnName { get; set; }

        /// <summary>
        /// Тип данных C# для столбца с внешним ключом
        /// </summary>
        public string CSharpType { get; set; }

        /// <summary>
        /// Имя связанной таблицы
        /// </summary>
        public string ReferencesTable { get; set; }

        /// <summary>
        /// Имя связанного столбца в таблице-источнике
        /// </summary>
        public string ReferencesColumn { get; set; }

        public string? ConstraintName {  get; set; }
    }
}
