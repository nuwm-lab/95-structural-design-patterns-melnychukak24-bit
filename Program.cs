using System;
using System.Collections.Generic;

namespace TranslationAdapterExample
{
    // --- 1. Цільовий інтерфейс (Target) ---
    // Наш "ідеальний" інтерфейс, який хоче бачити система.
    public interface ITranslator
    {
        string Translate(string text, string sourceLanguage, string targetLanguage);
    }

    // --- 2. Адаптований клас (Adaptee) ---
    // "Чужий" сервіс. Ми не можемо його змінити.
    public class ExternalTranslationService
    {
        // Наша "база даних" перекладів для імітації API
        private readonly Dictionary<string, string> _dictionary = new Dictionary<string, string>
        {
            // en -> uk
            ["hello|en|uk"] = "Привіт",
            ["world|en|uk"] = "Світ",
            ["adapter|en|uk"] = "Адаптер",

            // uk -> en
            ["привіт|uk|en"] = "Hello",
            ["світ|uk|en"] = "World",
            ["адаптер|uk|en"] = "Adapter",

            // uk -> de
            ["світ|uk|de"] = "Welt",
            ["привіт|uk|de"] = "Hallo",

            // de -> uk
            ["welt|de|uk"] = "Світ",
            ["hallo|de|uk"] = "Привіт"
        };
        
        // Метод з "дивним" ім'ям, який ми маємо адаптувати
        public string SpecificTranslateRequest(string content, string fromLangCode, string toLangCode)
        {
            Console.WriteLine($"[ExternalService]: Отримано запит API: '{content}' ({fromLangCode} -> {toLangCode})...");
            
            // Створюємо ключ для пошуку в нашій імітаційній базі
            string key = $"{content.ToLower()}|{fromLangCode.ToLower()}|{toLangCode.ToLower()}";

            if (_dictionary.TryGetValue(key, out string result))
            {
                return result;
            }

            return $"[Переклад для '{content}' не знайдено]";
        }
    }

    // --- 3. Адаптер (Adapter) ---
    // Клас, що поєднує наш інтерфейс (ITranslator) і чужий клас (ExternalTranslationService).
    // Він не змінився.
    public class TranslationAdapter : ITranslator
    {
        private readonly ExternalTranslationService _externalService;

        public TranslationAdapter(ExternalTranslationService externalService)
        {
            _externalService = externalService;
        }

        // Реалізація методу нашого інтерфейсу
        public string Translate(string text, string sourceLanguage, string targetLanguage)
        {
            Console.WriteLine($"[Adapter]: Адаптую запит для клієнта...");
            
            // Перетворення виклику Translate() на SpecificTranslateRequest()
            return _externalService.SpecificTranslateRequest(text, sourceLanguage, targetLanguage);
        }
    }

    // --- 4. Клієнтський код (Client) ---
    // Тепер цей код інтерактивний.
    class Program
    {
        static void Main(string[] args)
        {
            // 1. Створення об'єктів (як і раніше)
            ExternalTranslationService externalApi = new ExternalTranslationService();
            ITranslator translator = new TranslationAdapter(externalApi);

            Console.WriteLine("--- Консольний Перекладач (Патерн 'Адаптер') ---");
            Console.WriteLine("Доступні мови для імітації: en, uk, de.");
            Console.WriteLine("Спробуйте ввести: 'Hello', 'Привіт', 'Світ', 'Welt'");
            Console.WriteLine("Введіть 'exit' для виходу.");
            Console.WriteLine("-------------------------------------------------");

            while (true)
            {
                // 2. Отримання даних від користувача
                Console.Write("\nВведіть текст для перекладу: ");
                string textToTranslate = Console.ReadLine();

                if (textToTranslate.ToLower() == "exit")
                {
                    break;
                }

                Console.Write("Введіть мову оригіналу (напр. 'en'): ");
                string sourceLanguage = Console.ReadLine().ToLower();

                Console.Write("Введіть цільову мову (напр. 'uk'): ");
                string targetLanguage = Console.ReadLine().ToLower();
                Console.WriteLine();

                // 3. Використання адаптера
                // Клієнт все ще працює ЛИШЕ з інтерфейсом ITranslator.
                string translatedText = translator.Translate(textToTranslate, sourceLanguage, targetLanguage);

                // 4. Виведення результату
                Console.WriteLine("--- Результат ---");
                Console.WriteLine($"Переклад: {translatedText}");
                Console.WriteLine("-----------------");
            }

            Console.WriteLine("Роботу завершено.");
        }
    }
}
