using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SQLFileGenerator
{
    /// <summary>
    /// Генерация каркаса словарей i18next (label / message / common) из схемы.
    /// Закрывает дефект D-21/F-3: сген-формы зовут translate("label:&lt;View&gt;.&lt;field&gt;"),
    /// t("message:…"), t("common:…"), но JSON-словари ранее не эмитились — i18next рендерил сами ключи.
    ///
    /// Значения — humanized-заглушки (имя поля → читаемая латиница); полноценный перевод — вручную.
    /// Идемпотентно: существующие переводы НЕ затираются (мержим только отсутствующие ключи).
    /// Язык(и) — параметр (default "en").
    /// </summary>
    public static class LocaleGenerator
    {
        public static void GenerateLocales(List<TableSchema> tables, string resultDir, string[]? languages = null)
        {
            if (languages == null || languages.Length == 0)
                languages = new[] { "en" };

            var label = BuildLabel(tables);
            var message = BuildMessage();
            var common = BuildCommon();

            foreach (var lng in languages)
            {
                var dir = Path.Combine(resultDir, "public", "locales", lng);
                WriteMerged(Path.Combine(dir, "label.json"), label);
                WriteMerged(Path.Combine(dir, "message.json"), message);
                WriteMerged(Path.Combine(dir, "common.json"), common);
            }
        }

        /// <summary>label.json: ключи &lt;Entity&gt;AddEditView / &lt;Entity&gt;ListView с entityTitle и полями.</summary>
        private static JsonObject BuildLabel(List<TableSchema> tables)
        {
            var root = new JsonObject();
            foreach (var t in tables)
            {
                var addEdit = new JsonObject { ["entityTitle"] = Humanize(t.EntityName) };
                var list = new JsonObject { ["entityTitle"] = Humanize(t.EntityName) };
                foreach (var c in t.Columns)
                {
                    var human = Humanize(c.Name);
                    addEdit[c.Name] = human;
                    list[c.Name] = human;
                }
                root[$"{t.EntityName}AddEditView"] = addEdit;
                root[$"{t.EntityName}ListView"] = list;
            }
            return root;
        }

        /// <summary>message.json: фиксированный набор message-ключей, используемых шаблонами.</summary>
        private static JsonObject BuildMessage()
        {
            return new JsonObject
            {
                ["somethingWentWrong"] = "Something went wrong",
                ["error"] = new JsonObject
                {
                    ["alertMessageAlert"] = "Please correct the highlighted fields"
                },
                ["snackbar"] = new JsonObject
                {
                    ["successSave"] = "Saved successfully",
                    ["successEdit"] = "Updated successfully",
                    ["successDelete"] = "Deleted successfully"
                }
            };
        }

        /// <summary>common.json: общие подписи + bare-ключи (areYouSure/delete/no) без неймспейса.</summary>
        private static JsonObject BuildCommon()
        {
            return new JsonObject
            {
                ["save"] = "Save",
                ["cancel"] = "Cancel",
                ["goOut"] = "Exit",
                ["areYouSure"] = "Are you sure?",
                ["delete"] = "Delete",
                ["no"] = "No"
            };
        }

        private static void WriteMerged(string path, JsonObject generated)
        {
            JsonObject target;
            if (File.Exists(path))
            {
                try
                {
                    var existingText = File.ReadAllText(path);
                    target = JsonNode.Parse(existingText) as JsonObject ?? new JsonObject();
                }
                catch
                {
                    // Битый/нечитаемый существующий файл — не теряем его молча: пишем рядом .bak, начинаем чистый.
                    try { File.Copy(path, path + ".bak", overwrite: true); } catch { /* ignore */ }
                    target = new JsonObject();
                }
            }
            else
            {
                target = new JsonObject();
            }

            DeepMergeMissing(target, generated);

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var opts = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            File.WriteAllText(path, target.ToJsonString(opts));
            Console.WriteLine($"Locale skeleton generated/merged: {path}");
        }

        /// <summary>
        /// Добавляет ключи из source в target только если их там ещё нет — существующие переводы сохраняются.
        /// Узлы клонируются (DeepClone) при вставке, т.к. JsonNode нельзя переприсвоить чужому родителю.
        /// </summary>
        private static void DeepMergeMissing(JsonObject target, JsonObject source)
        {
            foreach (var kv in source)
            {
                if (kv.Value is JsonObject srcObj)
                {
                    if (target[kv.Key] is JsonObject tgtObj)
                    {
                        DeepMergeMissing(tgtObj, srcObj);
                    }
                    else if (!target.ContainsKey(kv.Key) || target[kv.Key] is null)
                    {
                        target[kv.Key] = srcObj.DeepClone();
                    }
                }
                else
                {
                    if (!target.ContainsKey(kv.Key) || target[kv.Key] is null)
                    {
                        target[kv.Key] = kv.Value?.DeepClone();
                    }
                }
            }
        }

        /// <summary>
        /// Имя поля/сущности → читаемая подпись. Вставляет пробелы перед заглавными (camel/Pascal),
        /// разбивает по _ - . и капитализирует слова. "created_at" → "Created At", "UserCheck" → "User Check".
        /// </summary>
        private static string Humanize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return name;

            // Разделяем camelCase/PascalCase границы: "userCheck" -> "user Check"
            var spaced = Regex.Replace(name, "([a-z0-9])([A-Z])", "$1 $2");
            var words = spaced.Split(new[] { ' ', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            foreach (var w in words)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(char.ToUpper(w[0]));
                if (w.Length > 1) sb.Append(w.Substring(1));
            }
            return sb.ToString();
        }
    }
}
