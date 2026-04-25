namespace MangystauJobHuntPlatform.Services;

public class AiGeocodingService
{
    private readonly HttpClient _httpClient;
    private const string ApiKey = "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"; // Получается за 1 минуту на groq.com

    public AiGeocodingService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<(double lat, double lon)> GeocodeAktauAsync(string userLocationText)
    {
        var request = new
        {
            model = "llama-3.3-70b-versatile", // Очень мощная и быстрая модель
            messages = new[]
            {
                new { role = "system", content = "You are a geocoder for Aktau, Kazakhstan. Return ONLY JSON: { \"lat\": 0.0, \"lon\": 0.0 } based on the location description." },
                new { role = "user", content = userLocationText }
            },
            response_format = new { type = "json_object" }
        };

        // Отправляем запрос к AI
        var response = await _httpClient.PostAsJsonAsync("https://api.groq.com/openai/v1/chat/completions", request);
        // 
        var jsonParser = new JsonParsingService();
        var coor = await jsonParser.ParseAiResponse(response);
        
        return (coor.lat, coor.lon); // Теперь у тебя есть реальные координаты!
    }
}