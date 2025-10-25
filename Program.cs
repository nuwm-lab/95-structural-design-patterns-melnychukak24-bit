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
