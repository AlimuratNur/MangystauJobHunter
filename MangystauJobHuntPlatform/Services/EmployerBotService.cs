using MangystauJobHuntPlatform.DB;
using MangystauJobHuntPlatform.Models;

namespace MangystauJobHuntPlatform.Services;

using Telegram.Bot;
using Telegram.Bot.Types;
using System.Collections.Concurrent;

public class EmployerBotService
{
    private readonly AppDbContext _db;
    private readonly ITelegramBotClient _bot;
    private readonly AiGeocodingService _aiGeocoding;
    
    // Временное хранилище черновиков вакансий в памяти
    private static readonly ConcurrentDictionary<long, VacancyDraft> _drafts = new();

    public EmployerBotService(AppDbContext db, [FromKeyedServices("EmployerBot")]ITelegramBotClient bot, AiGeocodingService aiGeocoding)
    {
        _db = db;
        _bot = bot;
        _aiGeocoding = aiGeocoding;
    }

    public async Task HandleUpdate(Update update)
    {
        if (update.Message is not { Text: not null } msg) return;

        var chatId = msg.Chat.Id;

        // Если это новая вакансия или сброс
        if (msg.Text == "/start" || msg.Text == "/add")
        {
            _drafts[chatId] = new VacancyDraft { Step = VacancyCreationStep.Title };
            await _bot.SendMessage(chatId, "🏗 Создание вакансии.\nВведите название (например: Кассир в супермаркет):");
            return;
        }

        if (!_drafts.TryGetValue(chatId, out var draft))
        {
            await _bot.SendMessage(chatId, "Чтобы создать вакансию, введите /add");
            return;
        }

        switch (draft.Step)
        {
            case VacancyCreationStep.Title:
                draft.Title = msg.Text;
                draft.Step = VacancyCreationStep.Description;
                await _bot.SendMessage(chatId, "Краткое описание обязанностей:");
                break;

            case VacancyCreationStep.Description:
                draft.Description = msg.Text;
                draft.Step = VacancyCreationStep.Address;
                await _bot.SendMessage(chatId, "Укажите адрес или микрорайон (например: 11 мкр, 23 дом):");
                break;

            case VacancyCreationStep.Address:
                await _bot.SendMessage(chatId, "🤖 AI определяет координаты...");
    
                var (lat, lon) = await _aiGeocoding.GeocodeAktauAsync(msg.Text);

                var vacancy = new Vacancy
                {
                    Title = draft.Title,
                    Description = draft.Description,
                    District = msg.Text,
                    Latitude = lat,
                    Longitude = lon,
                    OwnerId = chatId, // Это TelegramId работодателя
                    RequiredSkills = "Общие навыки",
                    CreatedAt = DateTime.UtcNow 
                };

                _db.Vacancies.Add(vacancy);
                await _db.SaveChangesAsync(); // PostgreSQL сохранит OwnerId

                _drafts.TryRemove(chatId, out _);
                await _bot.SendMessage(chatId, $"✅ Вакансия '{vacancy.Title}' опубликована!\nТеперь соискатели увидят её в поиске.");
                break;
        }
    }
}

// Вспомогательный класс для хранения состояния
public class VacancyDraft
{
    public string Title { get; set; }
    public string Description { get; set; }
    public VacancyCreationStep Step { get; set; }
}

public enum VacancyCreationStep { Title, Description, Address }