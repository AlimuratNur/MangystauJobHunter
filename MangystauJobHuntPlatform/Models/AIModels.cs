namespace MangystauJobHuntPlatform.Models;

// Структура ответа от Groq/OpenAI
public class AiResponse
{
    public List<Choice> Choices { get; set; }
}

public class Choice
{
    public Message Message { get; set; }
}

public class Message
{
    public string Content { get; set; }
}

// То, что мы ждем ВНУТРИ контента (наш JSON с координатами)
public class GeoCoords
{
    public double Lat { get; set; }
    public double Lon { get; set; }
}