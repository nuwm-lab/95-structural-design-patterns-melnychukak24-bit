// ITranslator.cs
public interface ITranslator
{
    string Translate(string text, string fromLanguage, string toLanguage);
}
// GoogleTranslator.cs
public class GoogleTranslator
{
    public string GoogleTranslate(string input, string sourceLang, string targetLang)
    {
        return $"[Google] Перекладено '{input}' з {sourceLang} на {targetLang}";
    }
}
// DeepLTranslator.cs
public class DeepLTranslator
{
    public string DeepLTranslateText(string text, string from, string to)
    {
        return $"[DeepL] Перекладено '{text}' з {from} на {to}";
    }
}
// TranslatorAdapter.cs
public class GoogleTranslatorAdapter : ITranslator
{
    private readonly GoogleTranslator _google;

    public GoogleTranslatorAdapter(GoogleTranslator google)
    {
        _google = google;
    }

    public string Translate(string text, string fromLanguage, string toLanguage)
    {
        return _google.GoogleTranslate(text, fromLanguage, toLanguage);
    }
}

public class DeepLTranslatorAdapter : ITranslator
{
    private readonly DeepLTranslator _deepl;

    public DeepLTranslatorAdapter(DeepLTranslator deepl)
    {
        _deepl = deepl;
    }

    public string Translate(string text, string fromLanguage, string toLanguage)
    {
        return _deepl.DeepLTranslateText(text, fromLanguage, toLanguage);
    }
}
// Program.cs
using System;

class Program
{
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("=== Реальний переклад через адаптер ===");
        Console.WriteLine("Введіть текст:");
        string text = Console.ReadLine();

        Console.WriteLine("Введіть мову з якої перекладати (наприклад: en):");
        string fromLang = Console.ReadLine();

        Console.WriteLine("Введіть мову на яку перекладати (наприклад: uk):");
        string toLang = Console.ReadLine();

        // Використаємо два різні перекладачі
        ITranslator googleAdapter = new GoogleTranslatorAdapter(new GoogleTranslator());
        ITranslator deeplAdapter = new DeepLTranslatorAdapter(new DeepLTranslator());

        Console.WriteLine("\nРезультати перекладу:");
        Console.WriteLine(googleAdapter.Translate(text, fromLang, toLang));
        Console.WriteLine(deeplAdapter.Translate(text, fromLang, toLang));
    }
}

