using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RealTimeTranslation
{
    /// <summary>
    /// Загальні інтерфейси та адаптер для реал-тайм перекладу.
    /// Просто приклад: показує як організувати Adapter pattern
    /// та асинхронну потокову передачу перекладу (IAsyncEnumerable).
    ///
    /// Що входить:
    /// - ITranslationProvider: загальний інтерфейс для сторонніх сервісів
    /// - MockTranslationProvider: локальний провайдер для тестування/демо
    /// - HttpTranslationProvider: шаблон для реального HTTP API (потрібно налаштувати ключі/ендпоінти)
    /// - TranslationAdapter: "адаптер", що уніфікує роботу з різними провайдерами
    /// - RealTimeTranslator: вищий рівень, який надає стрім перекладу і події
    /// - Console demo: приклад використання
    ///
    /// Примітки:
    /// - Для реальної інтеграції з Google/Microsoft/іншими перекладачами треба реалізувати ITranslationProvider,
    ///   використовуючи їх HTTP/WebSocket API (з дотриманням ліцензій та обмежень).
    /// - Цей код не виконує зовнішніх викликів сам по собі (окрім шаблону HttpTranslationProvider): він безпечний для локального запуску.
    /// </summary>

    #region Models
    public record TranslationRequest(string Text, string FromLanguage, string ToLanguage);

    public record TranslationResponse(string TranslatedText, bool IsFinal);
    #endregion

    #region Provider Interface
    /// <summary>
    /// Уніфікований інтерфейс для провайдерів перекладу.
    /// - TranslateAsync повертає повний результат (корисно для коротких запитів)
    /// - StreamTranslateAsync реалізує потоковий переклад по частинах (реальний стрім або симуляція)
    /// </summary>
    public interface ITranslationProvider : IDisposable
    {
        /// <summary>
        /// Повний переклад (одноразово)
        /// </summary>
        Task<TranslationResponse> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Потоковий переклад — типово реалізується через серверні стріми або WebSocket.
        /// Повертає послідовність часткових результатів (камішені оновлення).
        /// </summary>
        IAsyncEnumerable<TranslationResponse> StreamTranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default);
    }
    #endregion

    #region Mock Provider
    /// <summary>
    /// Простий провайдер, що симулює стрім: ділить вхідний текст на слова/фрази і повертає поступово.
    /// Використовується для локального тестування.
    /// </summary>
    public class MockTranslationProvider : ITranslationProvider
    {
        private bool _disposed = false;

        public Task<TranslationResponse> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
        {
            // Простий "фейковий" переклад: додаємо мітку мови.
            var result = $"[{request.ToLanguage}] {request.Text}";
            return Task.FromResult(new TranslationResponse(result, true));
        }

        public async IAsyncEnumerable<TranslationResponse> StreamTranslateAsync(TranslationRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var words = request.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();

            for (int i = 0; i < words.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // "Перекладаємо" кожне слово — в реальності це буде від сервера
                sb.Append(words[i]);
                if (i < words.Length - 1) sb.Append(' ');

                // Повертаємо частковий результат
                yield return new TranslationResponse($"[{request.ToLanguage}] {sb}", false);

                // Затримка, щоб симулювати реальний стрім
                await Task.Delay(150, cancellationToken);
            }

            // Останній (фінальний) кусок
            yield return new TranslationResponse($"[{request.ToLanguage}] {sb}", true);
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
    #endregion

    #region HTTP Provider Template
    /// <summary>
    /// Шаблон провайдера для HTTP API. Тут показано структуру — замініть URL і логіку під конкретний сервіс.
    /// Для стрімінгових API (сокети/серверні стріми) треба реалізувати окрему логіку.
    /// </summary>
    public class HttpTranslationProvider : ITranslationProvider
    {
        private readonly HttpClient _http;
        private bool _disposed = false;
        private readonly string _endpoint;
        private readonly string _apiKey;

        public HttpTranslationProvider(string endpoint, string apiKey)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _apiKey = apiKey; // може бути null для відкритих API
            _http = new HttpClient();
        }

        public async Task<TranslationResponse> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
        {
            // ЦЕ ШАБЛОН — замініть під API, яке ви використовуєте (Google, Azure, Yandex тощо).
            var payload = new
            {
                q = request.Text,
                source = request.FromLanguage,
                target = request.ToLanguage
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var req = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = content
            };

            if (!string.IsNullOrEmpty(_apiKey))
                req.Headers.Add("Authorization", $"Bearer {_apiKey}");

            using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var raw = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            // Тут треба розпарсити реальну відповідь API. Для прикладу припустимо просту структуру.
            using var doc = JsonDocument.Parse(raw);
            var translated = doc.RootElement.GetProperty("translatedText").GetString();

            return new TranslationResponse(translated ?? string.Empty, true);
        }

        public async IAsyncEnumerable<TranslationResponse> StreamTranslateAsync(TranslationRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Багато API не надають прості HTTP потокові відповіді; часто використовують WebSocket або server-sent events (SSE).
            // Тут — приклад, як ви могли б імітувати стрім через кілька послідовних HTTP викликів (НЕ ідеально, але практично).

            var words = request.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();

            for (int i = 0; i < words.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Виконуємо попередній переклад для частини тексту
                sb.Append(words[i]);
                if (i < words.Length - 1) sb.Append(' ');

                var partialReq = new TranslationRequest(sb.ToString(), request.FromLanguage, request.ToLanguage);
                var partialResp = await TranslateAsync(partialReq, cancellationToken).ConfigureAwait(false);

                yield return new TranslationResponse(partialResp.TranslatedText, false);

                // Захист від спаму: поважайте rate limits
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }

            // Фінальна відповідь (могла б бути більш точною)
            yield return new TranslationResponse(await TranslateAsync(request, cancellationToken).ConfigureAwait(false));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _http.Dispose();
                _disposed = true;
            }
        }
    }
    #endregion

    #region Adapter
    /// <summary>
    /// TranslationAdapter: уніфікує виклики до конкретного провайдера і додає можливості:
    /// - retry/backoff (простий приклад)
    /// - кешування (приклад на місці)
    /// - перетворення результату до загальної моделі
    /// </summary>
    public class TranslationAdapter : IDisposable
    {
        private readonly ITranslationProvider _provider;
        private bool _disposed = false;

        public TranslationAdapter(ITranslationProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// Прості налаштування повторів. Для продакшену краще використати бібліотеки Polly.
        /// </summary>
        private async Task<T> RetryAsync<T>(Func<Task<T>> action, int maxAttempts = 3, CancellationToken cancellationToken = default)
        {
            var attempt = 0;
            var delay = 200;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    attempt++;
                    return await action().ConfigureAwait(false);
                }
                catch (Exception) when (attempt < maxAttempts)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    delay *= 2; // екпонентальний backoff
                }
            }
        }

        public Task<TranslationResponse> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
        {
            return RetryAsync(() => _provider.TranslateAsync(request, cancellationToken), 3, cancellationToken);
        }

        public async IAsyncEnumerable<TranslationResponse> StreamTranslateAsync(TranslationRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in _provider.StreamTranslateAsync(request, cancellationToken))
            {
                // Можна тут робити нормалізацію тексту, фільтрацію, агрегацію частин тощо.
                yield return item;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _provider.Dispose();
                _disposed = true;
            }
        }
    }
    #endregion

    #region Real-time translator (high-level)
    /// <summary>
    /// Простий клас, що надає зручний інтерфейс для реального часу.
    /// Приклад використання:
    /// var translator = new RealTimeTranslator(adapter);
    /// await foreach(var part in translator.TranslateStream(request)) Console.WriteLine(part.TranslatedText);
    /// </summary>
    public class RealTimeTranslator : IDisposable
    {
        private readonly TranslationAdapter _adapter;
        private bool _disposed = false;

        public RealTimeTranslator(TranslationAdapter adapter)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        /// <summary>
        /// Асинхронний стрім часткових результатів.
        /// Повертає IAsyncEnumerable, що можна підписати в UI або виводити в консоль.
        /// </summary>
        public IAsyncEnumerable<TranslationResponse> TranslateStream(TranslationRequest request, CancellationToken cancellationToken = default)
        {
            return _adapter.StreamTranslateAsync(request, cancellationToken);
        }

        /// <summary>
        /// Одноразовий (повний) переклад.
        /// </summary>
        public Task<TranslationResponse> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
        {
            return _adapter.TranslateAsync(request, cancellationToken);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _adapter.Dispose();
                _disposed = true;
            }
        }
    }
    #endregion

    #region Demo Console App
    public static class Demo
    {
        public static async Task RunDemoAsync()
        {
            Console.WriteLine("Demo: реальний час перекладу (Mock)");

            using var provider = new MockTranslationProvider();
            using var adapter = new TranslationAdapter(provider);
            using var translator = new RealTimeTranslator(adapter);

            var request = new TranslationRequest("Hello world. This is a streaming translation demo.", "en", "uk");

            using var cts = new CancellationTokenSource();

            // При бажанні скасувати після певного часу:
            // cts.CancelAfter(TimeSpan.FromSeconds(10));

            try
            {
                await foreach (var part in translator.TranslateStream(request, cts.Token))
                {
                    // В UI цей текст можна відобразити по мірі надходження
                    Console.WriteLine($"[Partial: IsFinal={part.IsFinal}] {part.TranslatedText}");
                }

                var final = await translator.TranslateAsync(request, cts.Token);
                Console.WriteLine($"Final: {final.TranslatedText}");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Переклад скасовано.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка під час перекладу: {ex}");
            }
        }

        // Простий запуск з Main (для локального тестування)
        public static async Task Main()
        {
            await RunDemoAsync();
            Console.WriteLine("Готово. Натисніть Enter для виходу.");
            Console.ReadLine();
        }
    }
    #endregion
}
