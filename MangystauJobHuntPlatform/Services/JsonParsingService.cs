using System.Text.Json;
using MangystauJobHuntPlatform.Models;

namespace MangystauJobHuntPlatform.Services;

public class JsonParsingService
{
    public async Task<(double lat, double lon)> ParseAiResponse(HttpResponseMessage response)
    {
        var jsonString = await response.Content.ReadAsStringAsync();
    
        // 1. Парсим основной ответ от API
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = JsonSerializer.Deserialize<AiResponse>(jsonString, options);

        // 2. Извлекаем строку с JSON-координатами, которую сгенерировала LLM
        string innerJson = result?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrEmpty(innerJson)) return (43.648, 51.172); // Дефолтные координаты Актау (центр)

        try 
        {
            // 3. Парсим строку в объект координат
            var coords = JsonSerializer.Deserialize<GeoCoords>(innerJson, options);
            return (coords.Lat, coords.Lon);
        }
        catch (JsonException)
        {
            // Если AI выдал кривой JSON, возвращаем дефолт
            return (43.648, 51.172);
        }
    }
}