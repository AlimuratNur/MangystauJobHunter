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
    private readonly ITelegramBotClient _employerBot;

    public TelegramBotService([FromKeyedServices("WorkerBot")] ITelegramBotClient botClient,
        [FromKeyedServices("EmployerBot")] ITelegramBotClient employerBot,
        IMatchingStrategy ai,
        AppDbContext db,
        IHttpClientFactory httpClientFactory, AiGeocodingService aiGeocoding)
    {
        _botClient = botClient;
        _ai = ai;
        _db = db;
        _httpClientFactory = httpClientFactory;
        _aiGeocoding = aiGeocoding;
        _employerBot = employerBot;
    }

    public async Task HandleUpdate(Update update, CancellationToken ct)
    {
        // Обработка сообщений
        if (update.Type == UpdateType.Message && update.Message != null)
        {
            // Теперь ЛЮБОЕ сообщение идет в логику регистрации/обработки
            await HandleWorkerUpdate(update, ct);
        }

        // Обработка кнопок
        if (update.Type == UpdateType.CallbackQuery)
        {
            await HandleCallbackAsync(update.CallbackQuery, ct);
        }
    }

    private async Task ShowMatches(long tgId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.TelegramId == tgId);
        var allVacancies = await _db.Vacancies.ToListAsync();

        // Фильтруем по расстоянию (например, в радиусе 5 км)
        var nearby = allVacancies
            .Select(v => new
            {
                Vacancy = v,
                Distance = CalculateDistance(user.Latitude, user.Longitude, v.Latitude, v.Longitude)
            })
            .Where(x => x.Distance <= 5.0) // 5 километров
            .OrderBy(x => x.Distance)
            .Take(5);

        if (!nearby.Any())
        {
            await _botClient.SendMessage(tgId, "К сожалению, в вашем районе пока нет вакансий. 😔",
                cancellationToken: ct);
            return;
        }

        foreach (var item in nearby)
        {
            var btn = new InlineKeyboardMarkup(
                InlineKeyboardButton.WithCallbackData("Откликнуться ✅", $"apply_{item.Vacancy.Id}"));
            await _botClient.SendMessage(tgId,
                $"🏢 {item.Vacancy.Title}\n" +
                $"📍 Расстояние: {item.Distance:F1} км\n" +
                $"📝 {item.Vacancy.Description}",
                replyMarkup: btn, cancellationToken: ct);
        }
    }


// Метод обработки колбэка (нажатие кнопки "Откликнуться")
    private async Task HandleCallbackAsync(CallbackQuery query, CancellationToken ct)
    {
        if (query.Data.StartsWith("apply_"))
        {
            var vacancyId = int.Parse(query.Data.Split('_')[1]);
            var workerTgId = query.From.Id;

            // Находим вакансию и данные соискателя
            var vacancy = await _db.Vacancies.FirstOrDefaultAsync(v => v.Id == vacancyId, ct);
            var worker = await _db.Users.FirstOrDefaultAsync(u => u.TelegramId == workerTgId, ct);

            if (vacancy != null && worker != null)
            {
                // Сообщение для работодателя
                string notifyText = $"🚀 **Новый отклик на вакансию: {vacancy.Title}**\n\n" +
                                    $"👤 Имя: {worker.Name}\n" +
                                    $"📊 Навыки: {worker.Skills}\n" +
                                    $"📍 Район: {worker.District}\n\n" +
                                    $"💬 Связаться с кандидатом: [Написать](tg://user?id={workerTgId})";

                try
                {
                    // Отправляем сообщение РАБОТОДАТЕЛЮ через его бота
                    await _employerBot.SendMessage(
                        chatId: vacancy.OwnerId,
                        text: notifyText,
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                        cancellationToken: ct);

                    // Подтверждение соискателю
                    await _botClient.AnswerCallbackQuery(
                        query.Id,
                        "Ваш профиль успешно отправлен работодателю!",
                        showAlert: true,
                        cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    await _botClient.AnswerCallbackQuery(query.Id,
                        "Ошибка: Работодатель не запустил бота или заблокировал его.");
                }
            }
        }
    }


// Простая функция расчета расстояния
private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
{
    var d1 = lat1 * (Math.PI / 180.0);
    var num1 = lat2 * (Math.PI / 180.0);
    var d2 = (lat2 - lat1) * (Math.PI / 180.0);
    var d3 = (lon2 - lon1) * (Math.PI / 180.0);
    var a = Math.Sin(d2 / 2.0) * Math.Sin(d2 / 2.0) +
            Math.Cos(d1) * Math.Cos(num1) * Math.Sin(d3 / 2.0) * Math.Sin(d3 / 2.0);
    return 6371 * (2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a)));
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

public async Task HandleWorkerUpdate(Update update, CancellationToken ct)
{
    var msg = update.Message;
    var tgId = msg.From.Id;
    var text = msg.Text;

    var user = await _db.Users.FirstOrDefaultAsync(u => u.TelegramId == tgId, cancellationToken: ct);

    // Логика команды /start
    if (text == "/start")
    {
        if (user != null)
        {
            // Сбрасываем прогресс существующего пользователя вместо удаления
            user.Step = RegistrationStep.Name;
            user.Name = msg.From.FirstName ?? "Пользователь";
        }
        else
        {
            // Создаем нового, если его нет
            user = new Users
            {
                TelegramId = tgId,
                Name = msg.From.FirstName ?? "Пользователь",
                Step = RegistrationStep.Name,
                Skills = "Не указано",
                Role = UserRole.Candidate,
                District = "не указан"
            };
            _db.Users.Add(user);
        }

        await _db.SaveChangesAsync(ct);
        await _botClient.SendMessage(tgId, "Добро пожаловать! Давайте найдем работу в Актау. Как вас зовут?",
            cancellationToken: ct);
        return;
    }

    // Если пользователь каким-то образом не в базе и не ввел /start
    if (user == null) return;

    switch (user.Step)
    {
        case RegistrationStep.Name:
            user.Name = text;
            user.Step = RegistrationStep.Age;
            await _db.SaveChangesAsync(ct);
            await _botClient.SendMessage(tgId, $"Приятно познакомиться, {user.Name}! Сколько вам лет?",
                cancellationToken: ct);
            break;

        case RegistrationStep.Age:
            if (int.TryParse(text, out int age))
            {
                user.Age = age;
                user.Step = RegistrationStep.Skill;
                await _db.SaveChangesAsync(ct);
                await _botClient.SendMessage(tgId, "Какие навыки у вас есть?",
                    cancellationToken: ct);
            }
            else
            {
                await _botClient.SendMessage(tgId, "Пожалуйста, введите возраст числом.", cancellationToken: ct);
            }

            break;
        
        case RegistrationStep.Skill:
            user.Skills = text;
            user.Step = RegistrationStep.District;
            await _db.SaveChangesAsync(ct);
            await _botClient.SendMessage(tgId,"В каком микрорайоне вы живете?", 
                cancellationToken:ct);
            break;
        
        case RegistrationStep.District:
            await _botClient.SendMessage(tgId, "🤖 AI сопоставляет адрес с картой Актау...", cancellationToken: ct);
            var coords = await _aiGeocoding.GeocodeAktauAsync(text);
            user.Latitude = coords.lat;
            user.Longitude = coords.lon;
            user.District = text;
            user.Step = RegistrationStep.Completed;
            await _db.SaveChangesAsync(ct);

            await _botClient.SendMessage(tgId,
                $"📍 Место зафиксировано ({user.Latitude:F3}, {user.Longitude:F3}). Ищу ближайшие вакансии...",
                cancellationToken: ct);
            await ShowMatches(tgId, ct);
            break;

        case RegistrationStep.Completed:
            // Если регистрация завершена, любой ввод (кроме команд) заново ищет вакансии по его району
            await ShowMatches(tgId, ct);
            break;
    }
}

}