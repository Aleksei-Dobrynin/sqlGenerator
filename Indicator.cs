using System;
using System.Threading;
using System.Threading.Tasks;

namespace SQLFileGenerator
{
    /// <summary>
    /// Класс для отображения индикатора прогресса в консоли
    /// </summary>
    public class ProgressIndicator : IDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _animationTask;
        private readonly string _message;
        private readonly int _startLeft;
        private readonly int _startTop;
        private bool _disposed;

        /// <summary>
        /// Стили анимации
        /// </summary>
        public enum AnimationStyle
        {
            Spinner,
            Dots,
            Bar,
            Wave
        }

        private readonly AnimationStyle _style;
        private readonly string[] _spinnerFrames = { "|", "/", "-", "\\" };
        private readonly string[] _dotsFrames = { ".", "..", "...", "....", "....." };
        private readonly string[] _barFrames = { "[■□□□□□□□□□]", "[■■□□□□□□□□]", "[■■■□□□□□□□]", "[■■■■□□□□□□]",
                                                   "[■■■■■□□□□□]", "[■■■■■■□□□□]", "[■■■■■■■□□□]", "[■■■■■■■■□□]",
                                                   "[■■■■■■■■■□]", "[■■■■■■■■■■]" };
        private readonly string[] _waveFrames = { "~", "~~", "~~~", "~~~~", "~~~~~", "~~~~~~", "~~~~~", "~~~~", "~~~", "~~" };

        /// <summary>
        /// Создает новый индикатор прогресса
        /// </summary>
        public ProgressIndicator(string message, AnimationStyle style = AnimationStyle.Spinner)
        {
            _message = message;
            _style = style;
            _cancellationTokenSource = new CancellationTokenSource();

            // Сохраняем позицию курсора
            _startLeft = Console.CursorLeft;
            _startTop = Console.CursorTop;

            // Скрываем курсор для более плавной анимации
            Console.CursorVisible = false;

            // Запускаем анимацию
            _animationTask = Task.Run(() => Animate(_cancellationTokenSource.Token));
        }

        /// <summary>
        /// Анимация индикатора
        /// </summary>
        private void Animate(CancellationToken token)
        {
            var frames = GetFrames();
            var frameIndex = 0;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    lock (Console.Out)
                    {
                        Console.SetCursorPosition(_startLeft, _startTop);

                        var frame = frames[frameIndex % frames.Length];
                        var output = _style switch
                        {
                            AnimationStyle.Spinner => $"{_message} {frame}    ",
                            AnimationStyle.Dots => $"{_message}{frame}    ",
                            AnimationStyle.Bar => $"{_message} {frame}    ",
                            AnimationStyle.Wave => $"{_message} {frame}    ",
                            _ => $"{_message} {frame}    "
                        };

                        Console.Write(output);
                    }

                    frameIndex++;
                    Thread.Sleep(GetDelay());
                }
                catch (Exception)
                {
                    // Игнорируем ошибки позиционирования курсора
                    break;
                }
            }
        }

        /// <summary>
        /// Получает массив кадров для текущего стиля
        /// </summary>
        private string[] GetFrames()
        {
            return _style switch
            {
                AnimationStyle.Spinner => _spinnerFrames,
                AnimationStyle.Dots => _dotsFrames,
                AnimationStyle.Bar => _barFrames,
                AnimationStyle.Wave => _waveFrames,
                _ => _spinnerFrames
            };
        }

        /// <summary>
        /// Получает задержку между кадрами
        /// </summary>
        private int GetDelay()
        {
            return _style switch
            {
                AnimationStyle.Spinner => 100,
                AnimationStyle.Dots => 300,
                AnimationStyle.Bar => 200,
                AnimationStyle.Wave => 150,
                _ => 100
            };
        }

        /// <summary>
        /// Останавливает индикатор и выводит финальное сообщение
        /// </summary>
        public void Stop(string finalMessage = null, bool success = true)
        {
            if (_disposed) return;

            _cancellationTokenSource.Cancel();

            try
            {
                _animationTask.Wait(1000);
            }
            catch { }

            lock (Console.Out)
            {
                Console.SetCursorPosition(_startLeft, _startTop);

                if (!string.IsNullOrEmpty(finalMessage))
                {
                    var color = success ? ConsoleColor.Green : ConsoleColor.Red;
                    var symbol = success ? "✓" : "✗";

                    var originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = color;
                    Console.Write($"{finalMessage} [{symbol}]");
                    Console.ForegroundColor = originalColor;
                }
                else
                {
                    Console.Write(new string(' ', _message.Length + 20));
                    Console.SetCursorPosition(_startLeft, _startTop);
                }

                Console.WriteLine();
                Console.CursorVisible = true;
            }
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            Stop();
            _cancellationTokenSource?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Вспомогательный класс для удобной работы с индикаторами
    /// </summary>
    public static class Progress
    {
        /// <summary>
        /// Выполняет операцию с отображением прогресса
        /// </summary>
        public static async Task<T> RunWithProgress<T>(string message, Func<Task<T>> operation,
            ProgressIndicator.AnimationStyle style = ProgressIndicator.AnimationStyle.Spinner)
        {
            using (var indicator = new ProgressIndicator(message, style))
            {
                try
                {
                    var result = await operation();
                    indicator.Stop($"{message} - Completed", true);
                    return result;
                }
                catch (Exception ex)
                {
                    indicator.Stop($"{message} - Failed", false);
                    throw;
                }
            }
        }

        /// <summary>
        /// Выполняет операцию с отображением прогресса (без возвращаемого значения)
        /// </summary>
        public static async Task RunWithProgress(string message, Func<Task> operation,
            ProgressIndicator.AnimationStyle style = ProgressIndicator.AnimationStyle.Spinner)
        {
            using (var indicator = new ProgressIndicator(message, style))
            {
                try
                {
                    await operation();
                    indicator.Stop($"{message} - Completed", true);
                }
                catch (Exception ex)
                {
                    indicator.Stop($"{message} - Failed", false);
                    throw;
                }
            }
        }
    }
}