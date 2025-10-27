using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks; // Потрібно для асинхронності

namespace TranslationAdapterExample
{
    // --- 1. Цільовий інтерфейс (Target) ---

    /// <summary>
    /// Цільовий інтерфейс, який очікує наша система.
    /// Тепер він асинхронний, щоб підтримувати операції "реального часу".
    /// </summary>
    public interface ITranslator
    {
        /// <summary>
        /// Асинхронно перекладає текст.
        /// </summary>
        /// <param name="text">Текст для перекладу.</param>
        /// <param name="sourceLanguage">Мова оригіналу (ISO 639-1 код).</param>
        /// <param name="targetLanguage">Цільова мова (ISO 639-1 код).</param>
        /// <returns>Перекладений текст.</returns>
        Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage);
    }

    // --- 2. Адаптований клас (Adaptee) ---

    /// <summary>
    /// Імітація стороннього сервісу перекладу.
    /// Ми не можемо змінити цей клас, він має "дивний" асинхронний API.
    /// </summary>
    public class ExternalTranslationService
    {
        // Використовуємо StringComparer.OrdinalIgnoreCase для словника,
        // щоб пошук за ключем не залежав від регістру.
        private readonly Dictionary<string, string> _dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // en -> uk
            ["hello|en|uk"] = "Привіт",
            ["world|en|uk"] = "Світ",
            ["adapter|en|uk"] = "Адаптер",
            ["clan|en|uk"] = "клан",

            // uk -> en
            ["привіт|uk|en"] = "Hello",
            ["світ|uk|en"] = "World",
            ["адаптер|uk|en"] = "Adapter",
            ["клан|uk|en"] = "clan",

            // uk -> de
            ["світ|uk|de"] = "Welt",
            ["привіт|uk|de"] = "Hallo",

            // de -> uk
            ["welt|de|uk"] = "Світ",
            ["hallo|de|uk"] = "Привіт"
        };
        
        /// <summary>
        /// Набір мов, що підтримуються "сервісом".
        /// </summary>
        public IReadOnlyList<string> SupportedLanguages { get; } = new List<string> { "en", "uk", "de" }.AsReadOnly();

        /// <summary>
        /// "Дивний" асинхронний метод, який ми маємо адаптувати.
        /// </summary>
        public async Task<string> SpecificTranslateRequestAsync(string content, string fromLangCode, string toLangCode)
        {
            // Валідація вхідних аргументів, як ви й рекомендували.
            if (string.IsNullOrWhiteSpace(content)) return "[Помилка: Текст не може бути порожнім]";
            if (string.IsNullOrWhiteSpace(fromLangCode)) return "[Помилка: Мова оригіналу не вказана]";
            if (string.IsNullOrWhiteSpace(toLangCode)) return "[Помилка: Цільова мова не вказана]";

            // Імітація мережевої затримки (I/O operation)
            await Task.Delay(150); // 150 мс

            Console.WriteLine($"[ExternalService]: Отримано API запит: '{content}' ({fromLangCode} -> {toLangCode})...");
            
            // Використовуємо Trim() та НЕ .ToLower(), оскільки словник тепер ігнорує регістр
            string key = $"{content.Trim()}|{fromLangCode.Trim()}|{toLangCode.Trim()}";

            if (_dictionary.TryGetValue(key, out string result))
            {
                return result;
            }

            return $"[Переклад для '{content}' не знайдено]";
        }
    }

    // --- 3. Адаптер (Adapter) ---

    /// <summary>
    /// Адаптер, що перетворює інтерфейс <see cref="ExternalTranslationService"/>
    /// на наш цільовий <see cref="ITranslator"/>.
    /// </summary>
    public class TranslationAdapter : ITranslator
    {
        private readonly ExternalTranslationService _externalService;

        /// <summary>
        /// Створює екземпляр адаптера.
        /// </summary>
        /// <param name="externalService">Зовнішній сервіс, який потрібно адаптувати.</param>
        /// <exception cref="ArgumentNullException">Кидається, якщо сервіс не надано (null).</exception>
        public TranslationAdapter(ExternalTranslationService externalService)
        {
            // Перевірка на null, як ви рекомендували
            _externalService = externalService ?? throw new ArgumentNullException(nameof(externalService));
        }

        /// <inheritdoc/>
        public async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
        {
            Console.WriteLine($"[Adapter]: Адаптую асинхронний запит для клієнта...");
            
            // "Адаптація" виклику:
            // Наш метод TranslateAsync викликає "дивний" метод SpecificTranslateRequestAsync
            return await _externalService.SpecificTranslateRequestAsync(text, sourceLanguage, targetLanguage);
        }
    }

    // --- 4. Клієнтський код (Client) ---

    /// <summary>
    /// Головний клас програми. Оголошений як static за конвенцією.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Точка входу. Тепер асинхронна (async Task).
        /// </summary>
        static async Task Main(string[] args)
        {
            // Налаштування консолі для коректної роботи з українською мовою
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            // --- Налаштування ---
            ExternalTranslationService externalApi = new ExternalTranslationService();
            ITranslator translator = new TranslationAdapter(externalApi);
            // --- -------------- ---

            Console.WriteLine("--- Консольний Перекладач (Патерн 'Адаптер') v2.0 ---");
            
            // Використовуємо властивість, як ви радили, для UX
            Console.WriteLine($"Доступні мови: {string.Join(", ", externalApi.SupportedLanguages)}");
            Console.WriteLine("Введіть 'exit' для виходу.");
            Console.WriteLine("-------------------------------------------------");

            while (true)
            {
                // 1. Безпечне зчитування тексту
                Console.Write("\nВведіть текст для перекладу: ");
                string textToTranslate = Console.ReadLine();

                // Використовуємо надійні перевірки
                if (string.IsNullOrWhiteSpace(textToTranslate))
                {
                    Console.WriteLine("Порожній ввід. Спробуйте ще раз.");
                    continue;
                }
                
                // Використовуємо StringComparison.OrdinalIgnoreCase
                if (string.Equals(textToTranslate.Trim(), "exit", StringComparison.OrdinalIgnoreCase)) 
                {
                    break;
                }

                // 2. Безпечне зчитування мов
                Console.Write($"Введіть мову оригіналу (напр. '{externalApi.SupportedLanguages[1]}'): ");
                string sourceLanguage = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(sourceLanguage))
                {
                    Console.WriteLine("Мова не вказана. Спробуйте ще раз.");
                    continue;
                }

                Console.Write($"Введіть цільову мову (напр. '{externalApi.SupportedLanguages[0]}'): ");
                string targetLanguage = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(targetLanguage))
                {
                    Console.WriteLine("Мова не вказана. Спробуйте ще раз.");
                    continue;
                }

                Console.WriteLine(); // Відступ

                try
                {
                    // 3. Асинхронний виклик
                    // Клієнт все ще працює ЛИШЕ з інтерфейсом ITranslator.
                    string translatedText = await translator.TranslateAsync(
                        textToTranslate, 
                        sourceLanguage, 
                        targetLanguage
                    );

                    // 4. Виведення результату
                    Console.WriteLine("--- Результат ---");
                    Console.WriteLine($"Переклад: {translatedText}");
                    Console.WriteLine("-----------------");
                }
                catch (Exception ex)
                {
                    // Обробка будь-яких неочікуваних помилок
                    Console.WriteLine($"[Помилка адаптера]: {ex.Message}");
                }
            }

            Console.WriteLine("Роботу завершено.");
        }
    }
}
