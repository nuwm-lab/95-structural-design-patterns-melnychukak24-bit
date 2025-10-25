using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace RealTimeTranslation
{
    #region Models
    public record TranslationRequest(string Text, string FromLanguage, string ToLanguage);
    public record TranslationResponse(string TranslatedText, bool IsFinal);
    #endregion

    #region Provider Interface
    public interface ITranslationProvider : IAsyncDisposable, IDisposable
    {
        Task<TranslationResponse> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default);
        IAsyncEnumerable<TranslationResponse> StreamTranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default);
    }
    #endregion

    #region Mock Provider
    public sealed class MockTranslationProvider : ITranslationProvider
    {
        public Task<TranslationResponse> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
        {
            var result = $"[{request.ToLanguage}] {request.Text}";
            return Task.FromResult(new TranslationResponse(result, true));
        }

        public async IAsyncEnumerable<TranslationResponse> StreamTranslateAsync(
            TranslationRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var words = request.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();

            for (int i = 0; i < words.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                sb.Append(words[i]);
                if (i < words.Length - 1) sb.Append(' ');

                bool isFinal = i == words.Length - 1;
                yield return new TranslationResponse($"[{request.ToLanguage}] {sb}", isFinal);

                await Task.Delay(150, cancellationToken).ConfigureAwait(false);
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Dispose() { }
    }
    #endregion

    #region HTTP Provider
    public sealed class HttpTranslationProvider : ITranslationProvider
    {
        private readonly HttpClient _http;
        private readonly string _endpoint;
        private readonly string? _apiKey;
        private readonly ILogger? _logger;
        private bool _disposed;

        public HttpTranslationProvider(string endpoint, string? apiKey = null, HttpClient? httpClient = null, ILogger? logger = null)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _apiKey = apiKey;
            _http = httpClient ?? new HttpClient();
            _logger = logger;
        }

        public async Task<TranslationResponse> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
        {
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

            // ✅ Безпечний парсинг JSON
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("translatedText", out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var translated = prop.GetString() ?? string.Empty;
                return new TranslationResponse(translated, true);
            }
            else
            {
                _logger?.LogError("Unexpected response format: {raw}", raw);
                throw new InvalidOperationException("Unexpected response format from translation API");
            }
        }

        // ✅ Виправлений стрім без дублювання фінального yield
        public async IAsyncEnumerable<TranslationResponse> StreamTranslateAsync(
            TranslationRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var words = request.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();

            for (int i = 0; i < words.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                sb.Append(words[i]);
                if (i < words.Length - 1) sb.Append(' ');

                var partialReq = new TranslationRequest(sb.ToString(), request.FromLanguage, request.ToLanguage);
                var partialResp = await TranslateAsync(partialReq, cancellationToken).ConfigureAwait(false);

                bool isFinal = i == words.Length - 1;
                yield return new TranslationResponse(partialResp.TranslatedText, isFinal);

                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _http.Dispose();
                _disposed = true;
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
    #endregion

    #region Adapter
    public sealed class TranslationAdapter : IDisposable
    {
        private readonly ITranslationProvider _provider;
        private readonly ILogger? _logger;
        private bool _disposed;

        public TranslationAdapter(ITranslationProvider provider, ILogger? logger = null)
        {
            _provider = provider;
            _logger = logger;
        }

        // ✅ Покращений RetryAsync з фільтрацією винятків
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
                catch (HttpRequestException ex) when (attempt < maxAttempts)
                {
                    _logger?.LogWarning(ex, "HTTP error, retrying attempt {attempt}", attempt);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    delay *= 2;
                }
                catch (TaskCanceledException ex) when (attempt < maxAttempts)
                {
                    _logger?.LogWarning(ex, "Task canceled (timeout?), retrying {attempt}", attempt);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    delay *= 2;
                }
            }
        }

        public Task<TranslationResponse> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
            => RetryAsync(() => _provider.TranslateAsync(request, cancellationToken), 3, cancellationToken);

        public async IAsyncEnumerable<TranslationResponse> StreamTranslateAsync(
            TranslationRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in _provider.StreamTranslateAsync(request, cancellationToken))
                yield return item;
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

    #region High-Level Translator
    public sealed class RealTimeTranslator : IDisposable
    {
        private readonly TranslationAdapter _adapter;
        private bool _disposed;

        public RealTimeTranslator(TranslationAdapter adapter)
        {
            _adapter = adapter;
        }

        /// <summary>
        /// Асинхронний стрім перекладу з частковими результатами.
        /// </summary>
        public IAsyncEnumerable<TranslationResponse> TranslateStreamAsync(TranslationRequest request, CancellationToken cancellationToken = default)
            => _adapter.StreamTranslateAsync(request, cancellationToken);

        public Task<TranslationResponse> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
            => _adapter.TranslateAsync(request, cancellationToken);

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

    #region Demo (окремий sample)
    public static class Demo
    {
        public static async Task RunDemoAsync()
        {
            Console.WriteLine(\"Demo: Реальний час перекладу (Mock)\");
            using var provider = new MockTranslationProvider();
            using var adapter = new TranslationAdapter(provider);
            using var translator = new RealTimeTranslator(adapter);

            var request = new TranslationRequest(\"Hello world! This is a streaming translation demo.\", \"en\", \"uk\");

            await foreach (var part in translator.TranslateStreamAsync(request))
            {
                Console.WriteLine($\"[Partial: IsFinal={part.IsFinal}] {part.TranslatedText}\");
            }

            var final = await translator.TranslateAsync(request);
            Console.WriteLine($\"Final: {final.TranslatedText}\");
        }

        public static async Task Main()
        {
            await RunDemoAsync();
        }
    }
    #endregion
}
