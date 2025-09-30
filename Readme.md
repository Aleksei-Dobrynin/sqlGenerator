# 🤖 LLM Parser для SQL File Generator

## 📋 Описание

LLM Parser - это альтернативный метод парсинга SQL-скриптов с использованием языковых моделей. Он обеспечивает более гибкий и интеллектуальный анализ сложных SQL-конструкций.

## 🚀 Быстрый старт

### 1. Установка Ollama

```bash
# Windows (PowerShell)
winget install Ollama.Ollama

# Linux
curl -fsSL https://ollama.com/install.sh | sh

# macOS
brew install ollama
```

### 2. Загрузка модели

```bash
# Рекомендуемая модель для SQL парсинга
ollama pull codellama:7b

# Альтернативные модели
ollama pull mistral:7b-instruct
ollama pull deepseek-coder:6.7b
ollama pull phi-3:3.8b
```

### 3. Запуск Ollama

```bash
# Запустить Ollama сервер
ollama serve

# Проверить работу
curl http://localhost:11434/api/tags
```

### 4. Настройка appsettings.json

```json
{
  "LLMParser": {
    "Enabled": true,
    "ApiEndpoint": "http://localhost:11434/v1/chat/completions",
    "Model": "codellama:7b",
    "Temperature": 0.1,
    "MaxTokens": 8192
  },
  "Parser": {
    "DefaultMode": "LLM"
  }
}
```

## 🎯 Режимы работы

### Standard Mode
- Использует регулярные выражения
- Быстрый и предсказуемый
- Ограничен стандартными конструкциями

### LLM Mode
- Использует AI для парсинга
- Понимает сложные конструкции
- Требует работающий Ollama

### Hybrid Mode
- Сначала пробует LLM
- При ошибке переключается на Standard
- Максимальная надежность

## ⚙️ Параметры конфигурации

| Параметр | Описание | Значение по умолчанию |
|----------|----------|----------------------|
| `Enabled` | Включить LLM парсер | `true` |
| `ApiEndpoint` | URL API endpoint | `http://localhost:11434/v1/chat/completions` |
| `Model` | Название модели | `codellama:7b` |
| `Temperature` | Креативность (0.0-1.0) | `0.1` |
| `MaxTokens` | Макс. длина ответа | `8192` |
| `TimeoutSeconds` | Таймаут запроса | `60` |
| `RetryCount` | Количество попыток | `3` |
| `CacheResponses` | Кэширование ответов | `true` |
| `LogRequests` | Логирование запросов | `false` |

## 🧪 Тестирование

### Проверка работы Ollama

```bash
curl -X POST http://localhost:11434/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "codellama:7b",
    "messages": [
      {"role": "system", "content": "You are a helpful assistant."},
      {"role": "user", "content": "Hello!"}
    ]
  }'
```

### Пример SQL для тестирования

```sql
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    email VARCHAR(255) UNIQUE NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE posts (
    id SERIAL PRIMARY KEY,
    user_id INTEGER REFERENCES users(id),
    title VARCHAR(200) NOT NULL,
    content TEXT,
    published BOOLEAN DEFAULT false
);
```

## 🐛 Решение проблем

### Ollama не запускается
```bash
# Проверить статус
systemctl status ollama

# Перезапустить
systemctl restart ollama

# Логи
journalctl -u ollama -f
```

### Медленная генерация
- Используйте меньшую модель (phi-3:3.8b)
- Уменьшите MaxTokens
- Включите кэширование

### Неправильный парсинг
- Увеличьте Temperature до 0.3
- Попробуйте другую модель
- Используйте Hybrid режим

## 📊 Сравнение моделей

| Модель | Размер | Скорость | Качество | RAM |
|--------|--------|----------|----------|-----|
| phi-3:3.8b | 2.3GB | ⚡⚡⚡ | ⭐⭐⭐ | 4GB |
| codellama:7b | 3.8GB | ⚡⚡ | ⭐⭐⭐⭐ | 8GB |
| mistral:7b | 4.1GB | ⚡⚡ | ⭐⭐⭐⭐ | 8GB |
| deepseek-coder:6.7b | 3.8GB | ⚡⚡ | ⭐⭐⭐⭐⭐ | 8GB |

## 🔧 Продвинутые настройки

### Использование других провайдеров

#### OpenAI API
```json
{
  "LLMParser": {
    "ApiEndpoint": "https://api.openai.com/v1/chat/completions",
    "Model": "gpt-3.5-turbo",
    "ApiKey": "sk-..."
  }
}
```

#### LocalAI
```json
{
  "LLMParser": {
    "ApiEndpoint": "http://localhost:8080/v1/chat/completions",
    "Model": "ggml-model"
  }
}
```

### Кастомный системный промпт

```json
{
  "LLMParser": {
    "SystemPrompt": "You are an expert SQL parser. Analyze PostgreSQL scripts and return structured JSON..."
  }
}
```

## 📝 Примеры использования

### Командная строка
```bash
# Использовать LLM парсер
dotnet run
# Выбрать опцию 2

# Автоматический режим из конфига
dotnet run -- ./output
```

### Программный вызов
```csharp
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var parser = ParserFactory.CreateParser(ParserMode.LLM, config);
var tables = await parser.ParseAsync(sqlScript);
```

## 🔍 Отладка

Включите логирование в `appsettings.json`:
```json
{
  "LLMParser": {
    "LogRequests": true
  },
  "Logging": {
    "LogLevel": {
      "SQLFileGenerator": "Debug"
    }
  }
}
```

## 📚 Полезные ссылки

- [Ollama Documentation](https://ollama.com/docs)
- [OpenAI API Reference](https://platform.openai.com/docs/api-reference)
- [LocalAI GitHub](https://github.com/mudler/LocalAI)

## ⚠️ Известные ограничения

1. Требует ~8GB RAM для моделей 7B
2. Первый запрос может быть медленным (загрузка модели)
3. Не все SQL диалекты поддерживаются одинаково хорошо
4. Результат зависит от качества модели

## 🤝 Поддержка

При возникновении проблем:
1. Проверьте логи Ollama
2. Убедитесь в корректности конфигурации
3. Попробуйте Hybrid режим
4. Создайте issue на GitHub