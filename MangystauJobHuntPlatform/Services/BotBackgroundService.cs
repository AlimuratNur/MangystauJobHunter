using Telegram.Bot;
using Telegram.Bot.Polling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MangystauJobHuntPlatform.Services;

public class BotBackgroundService:BackgroundService
{
    private readonly ITelegramBotClient _workerBot;
    private readonly ITelegramBotClient _employerBot;
    private readonly IServiceProvider _serviceProvider;

    public BotBackgroundService(
        [FromKeyedServices("WorkerBot")] ITelegramBotClient workerBot,
        [FromKeyedServices("EmployerBot")] ITelegramBotClient employerBot,
        IServiceProvider serviceProvider)
    {
        _workerBot = workerBot;
        _employerBot = employerBot;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Запуск обоих ботов параллельно
        _workerBot.StartReceiving(
            updateHandler: async (bot, update, ct) => 
            {
                using var scope = _serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<TelegramBotService>();
                await service.HandleUpdate(update, ct);
            },
            errorHandler: async (bot, ex, ct) => Console.WriteLine(ex),
            cancellationToken: stoppingToken
        );

        _employerBot.StartReceiving(
            updateHandler: async (bot, update, ct) => 
            {
                using var scope = _serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<EmployerBotService>();
                await service.HandleUpdate(update);
            },
            errorHandler: async (bot, ex, ct) => Console.WriteLine(ex),
            cancellationToken: stoppingToken
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
            await botEngine.HandleUpdate(update, ct);
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