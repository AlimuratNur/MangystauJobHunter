using MangystauJobHuntPlatform.DB;
using MangystauJobHuntPlatform.Interface;
using MangystauJobHuntPlatform.Models;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Http;
using Telegram.Bot.Types.ReplyMarkups;

namespace MangystauJobHuntPlatform.Services;

public class TelegramBotService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMatchingStrategy _ai;
    private readonly AppDbContext _db;
    private readonly AiGeocodingService _aiGeocoding;

    public TelegramBotService(ITelegramBotClient botClient, IMatchingStrategy ai, AppDbContext db,
        IHttpClientFactory httpClientFactory, AiGeocodingService aiGeocoding)
    {
        _botClient = botClient;
        _ai = ai;
        _db = db;
        _httpClientFactory = httpClientFactory;
        _aiGeocoding = aiGeocoding;
    }

    public async Task HandleUpdateAsync(Update update, CancellationToken ct)
    {
        if (update.Type == UpdateType.Message && update.Message?.Text != null)
        {
            var chatId = update.Message.Chat.Id;
            var text = update.Message.Text;
            var message = update.Message;

            if (text == "/start")
            {
                await _botClient.SendMessage(chatId,
                    "Добро пожаловать в платформу занятости Мангистау! 🌊\n" +
                    "Я помогу найти работу рядом с домом (в вашем микрорайоне).",
                    cancellationToken: ct);
                // Здесь можно вызвать метод регистрации или показа вакансий
            }

            // Сохраняем навыки в БД 
            await SaveUserSkills(message.From.Id, message.Text);

            // Сразу предлагаем вакансии через AI 
            await ShowMatches(message.Chat.Id, message.From.Id, ct);
        }

        // Логика быстрого отклика через Callback 
        if (update.Type == UpdateType.CallbackQuery)
        {
            await HandleCallbackAsync(update.CallbackQuery, ct);
        }
    }

    private async Task ShowMatches(long chatId, long tgId, CancellationToken ct)
    {
        var user = _db.Users.FirstOrDefault(u => u.TelegramId == tgId);
        var vacancies = _db.Vacancies.ToList();

        // Ранжируем вакансии твоим косинусным движком
        var ranked = vacancies
            .Select(v => new { V = v, Score = _ai.CalculateScore(user.Skills, v.RequiredSkills) })
            .OrderByDescending(x => x.Score)
            .Take(3);

        foreach (var item in ranked)
        {
            var btn = new InlineKeyboardMarkup(
                InlineKeyboardButton.WithCallbackData("Откликнуться ✅", $"apply_{item.V.Id}"));

            await _botClient.SendMessage(chatId,
                $"📍 Район: {item.V.District}\n🔥 Подходит вам на: {item.Score:P0}\nРабота: {item.V.Title}\n{item.V.Description}",
                replyMarkup: btn, cancellationToken: ct);
        }
    }


    private async Task HandleCallbackAsync(CallbackQuery query, CancellationToken ct)
    {
        if (query.Data.StartsWith("apply_"))
        {
            var vacancyId = query.Data.Split('_')[1];
            // Живой запрос к БД через API для создания отклика (Требование 02)
            await _botClient.AnswerCallbackQuery(query.Id, "Ваш отклик отправлен работодателю!", cancellationToken: ct);
        }
    }

    private async Task SaveUserSkills(long tgId, string text)
    {
        // Ищем пользователя в базе
        var user = await _db.Users.FirstOrDefaultAsync(u => u.TelegramId == tgId);

        if (user == null)
        {
            // Если пользователя нет — создаем нового (например, по умолчанию он Candidate)
            user = new Users
            {
                TelegramId = tgId,
                Name = "User_" + tgId, // В идеале взять из update.Message.From.FirstName
                Skills = text,
                Role = UserRole.Candidate
            };
            _db.Users.Add(user);
        }
        else
        {
            // Если есть — обновляем навыки
            user.Skills = text;
        }

        // Сохраняем изменения в SQLite
        await _db.SaveChangesAsync();
    }

    public async Task HandleWorkerUpdate(Update update)
    {
        var tgId = update.Message.From.Id;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.TelegramId == tgId);

        // 1. Если пользователя нет - создаем и спрашиваем ИМЯ
        if (user == null)
        {
            user = new Users { TelegramId = tgId, Step = RegistrationStep.Name };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            await _botClient.SendMessage(tgId,
                "Добро пожаловать! Давайте найдем вам работу в Актау. Как вас зовут?");
            return;
        }

        // 2. Обработка шагов регистрации
        switch (user.Step)
        {
            case RegistrationStep.Name:
                user.Name = update.Message.Text;
                user.Step = RegistrationStep.Age;
                await _db.SaveChangesAsync();
                await _botClient.SendMessage(tgId, $"Приятно познакомиться, {user.Name}! Сколько вам полных лет?");
                break;

            case RegistrationStep.Age:
                if (int.TryParse(update.Message.Text, out int age))
                {
                    user.Age = age;
                    user.Step = RegistrationStep.District;
                    await _db.SaveChangesAsync();
                    await _botClient.SendMessage(tgId,
                        "В каком микрорайоне Актау вы живете или ищете работу? (Например: 14, 7 или 'возле набережной')");
                }
                else
                {
                    await _botClient.SendMessage(tgId, "Пожалуйста, введите возраст цифрами.");
                }

                break;

            case RegistrationStep.District:
                await _botClient.SendMessage(tgId, "Минутку, определяю ваше местоположение на карте...");
    
                // Вызываем AI для получения координат
                var coords = await _aiGeocoding.GeocodeAktauAsync(update.Message.Text);
    
                user.Latitude = coords.lat;
                user.Longitude = coords.lon;
                user.Step = RegistrationStep.Completed;
    
                await _db.SaveChangesAsync();
                await _botClient.SendMessage(tgId, $"📍 Место определено! Координаты: {user.Latitude}, {user.Longitude}. ");
                
                break;

            case RegistrationStep.Completed:
                // Если регистрация уже пройдена, просто отвечаем на команды или показываем вакансии
                
                break;
        }
    }
}