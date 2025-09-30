using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SQLFileGenerator
{
    /// <summary>
    /// Класс для построения OpenAI-совместимых запросов
    /// </summary>
    public class LLMRequestBuilder
    {
        /// <summary>
        /// Создает запрос для chat completions API
        /// </summary>
        public ChatCompletionRequest BuildChatCompletionRequest(
            string systemPrompt,
            string userPrompt,
            LLMParserConfig config)
        {
            var request = new ChatCompletionRequest
            {
                Model = config.Model,
                Temperature = config.Temperature,
                MaxTokens = config.MaxTokens,
                Messages = new List<ChatMessage>
                {
                    new ChatMessage
                    {
                        Role = "system",
                        Content = systemPrompt + GetSchemaDescription()
                    },
                    new ChatMessage
                    {
                        Role = "user",
                        Content = userPrompt
                    }
                }
            };

            // Добавляем response_format если поддерживается
            if (!string.IsNullOrEmpty(config.ResponseFormat))
            {
                request.ResponseFormat = new ResponseFormat { Type = config.ResponseFormat };
            }

            return request;
        }

        /// <summary>
        /// Получает описание схемы для LLM
        /// </summary>
        private string GetSchemaDescription()
        {
            return @"

IMPORTANT: Return ONLY valid JSON without markdown formatting or code blocks.
CRITICAL: Always return an ARRAY of tables, even if there is only one table. Wrap single table in [].

The JSON must be an ARRAY and match this exact structure:
[
  {
    ""TableName"": ""table_name_from_sql"",
    ""EntityName"": ""PascalCaseTableName"",
    ""Columns"": [
      {
        ""Name"": ""column_name"",
        ""CSharpType"": ""string|int|long|bool|DateTime|decimal|float|double|object"",
        ""IsPrimaryKey"": false,
        ""IsForeignKey"": false,
        ""IsNullable"": true
      }
    ],
    ""ForeignKeys"": [
      {
        ""ColumnName"": ""foreign_key_column"",
        ""CSharpType"": ""int"",
        ""ReferencesTable"": ""referenced_table"",
        ""ReferencesColumn"": ""referenced_column"",
        ""ConstraintName"": ""constraint_name""
      }
    ]
  }
]

EVEN FOR A SINGLE TABLE, return it as: [{table_object}], not just {table_object}

Type mapping rules:
- integer, serial -> int
- bigint, bigserial -> long
- text, varchar, character varying -> string
- boolean -> bool
- timestamp, date -> DateTime
- real -> float
- double precision -> double
- numeric, decimal -> decimal
- other types -> object

Detect PRIMARY KEY from:
1. Column constraints (e.g., 'id serial PRIMARY KEY')
2. Table constraints (e.g., 'CONSTRAINT pk_name PRIMARY KEY (column)')

Detect FOREIGN KEY from:
1. Column REFERENCES (e.g., 'user_id INTEGER REFERENCES users(id)')
2. ALTER TABLE ADD CONSTRAINT statements

For PascalCase conversion:
- snake_case -> PascalCase (e.g., user_profile -> UserProfile)
- Remove underscores and capitalize each word";
        }
    }

    /// <summary>
    /// Модель запроса для Chat Completion API
    /// </summary>
    public class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("response_format")]
        public ResponseFormat ResponseFormat { get; set; }
    }

    /// <summary>
    /// Сообщение в чате
    /// </summary>
    public class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }
    }

    /// <summary>
    /// Формат ответа
    /// </summary>
    public class ResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    /// <summary>
    /// Модель ответа от Chat Completion API
    /// </summary>
    public class ChatCompletionResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("object")]
        public string Object { get; set; }

        [JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("choices")]
        public List<Choice> Choices { get; set; }

        [JsonPropertyName("usage")]
        public Usage Usage { get; set; }
    }

    public class Choice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("message")]
        public ChatMessage Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string FinishReason { get; set; }
    }

    public class Usage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }
}