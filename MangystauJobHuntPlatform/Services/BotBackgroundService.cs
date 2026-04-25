using Telegram.Bot;
using Telegram.Bot.Polling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MangystauJobHuntPlatform.Services;

public class BotBackgroundService:BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceProvider _serviceProvider;

    public BotBackgroundService(ITelegramBotClient botClient, IServiceProvider serviceProvider)
    {
        _botClient = botClient;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Настройки получения обновлений
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<Telegram.Bot.Types.Enums.UpdateType>() 
        };

        _botClient.StartReceiving(
            HandleUpdateAsync,           // 1. Метод обработки обновлений
            HandlePollingErrorAsync,     // 2. Метод обработки ошибок
            receiverOptions,             // 3. Опции (может быть null)
            stoppingToken
        );

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Telegram.Bot.Types.Update update, CancellationToken ct)
    {
        // Так как DbContext и BotEngine — это Scoped сервисы, создаем Scope
        using var scope = _serviceProvider.CreateScope();
        var botEngine = scope.ServiceProvider.GetRequiredService<TelegramBotService>();
        
        try 
        {
            await botEngine.HandleUpdateAsync(update, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при обработке: {ex.Message}");
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
    {
        Console.WriteLine("Ошибка API Telegram: " + exception.Message);
        return Task.CompletedTask;
    }
}