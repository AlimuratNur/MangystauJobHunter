using Mscc.GenerativeAI;
using Mscc.GenerativeAI.Types;

public class AiGeocodingService
{
    private readonly GoogleAI _googleAi;
    private readonly GenerativeModel _model;

    public AiGeocodingService()
    {
        // Инициализируем Google AI с твоим ключом
        // Ключ берем из https://aistudio.google.com/
        _googleAi = new GoogleAI("AIzaSyCPN7WSo1298Ci_AGNVjHRACvK4WBWNfvU");
        
        // Используем модель flash (она быстрее) или pro
        _model = _googleAi.GenerativeModel(Model.Gemini3Flash);
    }

    public async Task<(double lat, double lon)> GeocodeAktauAsync(string userLocationText)
    {
        try
        {
            // Формируем запрос
            string prompt = $"Ты — геокодер города Актау. Напиши координаты для: '{userLocationText}'. " +
                            "Ответ дай СТРОГО в формате JSON: {\"lat\": 43.6, \"lon\": 51.1}. Никакого текста.";

            // Вызываем генерацию
            var response = await _model.GenerateContent(prompt);
            var responseText = response.Text;

            Console.WriteLine($"[GenAI Debug] Ответ: {responseText}");

            // Извлекаем цифры регуляркой (на случай если ИИ добавит лишние символы)
            var matches = System.Text.RegularExpressions.Regex.Matches(responseText, @"\d+\.\d+");

            if (matches.Count >= 2)
            {
                double lat = double.Parse(matches[0].Value, System.Globalization.CultureInfo.InvariantCulture);
                double lon = double.Parse(matches[1].Value, System.Globalization.CultureInfo.InvariantCulture);

                // Валидация: Актау находится примерно в этих границах
                if (lat > 43.0 && lat < 45.0)
                {
                    return (lat, lon);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GenAI Error] Ошибка: {ex.Message}");
        }

        // Если ИИ подвел, возвращаем центр Актау (Акимат)
        return (43.6472, 51.1722);
    }
}