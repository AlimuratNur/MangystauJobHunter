using MangystauJobHuntPlatform.DB;
using MangystauJobHuntPlatform.Interface;
using MangystauJobHuntPlatform.Models;
using MangystauJobHuntPlatform.Services;
using MangystauJobHuntPlatform.Strategies;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// 1. Фикс для работы с датами в PostgreSQL (обязательно!)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddHttpClient();

// AI и стратегия
builder.Services.AddScoped<IMatchingStrategy, CosineMatchingStrategy>();
builder.Services.AddHttpClient<AiGeocodingService>();
builder.Services.AddScoped<AiGeocodingService>();

// 2. Настройка базы данных (берет из Railway или appsettings.json)
// 1. Пытаемся взять стандартную переменную Railway для Postgres
// 1. Ищем строку везде: в переменных Railway, в секретах или в appsettings
var rawConnectionString = Environment.GetEnvironmentVariable("DATABASE_URL") 
                          ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(rawConnectionString))
{
    throw new Exception("❌ КРИТИЧЕСКАЯ ОШИБКА: Строка подключения к БД не найдена! Проверь Variables в Railway.");
}

string finalConnectionString = rawConnectionString;

// 2. Если Railway дал строку вида postgres://user:pass@host:port/db, переделываем её для .NET
if (rawConnectionString.StartsWith("postgres://"))
{
    var databaseUri = new Uri(rawConnectionString);
    var userInfo = databaseUri.UserInfo.Split(':');

    finalConnectionString = $"Host={databaseUri.Host};Port={databaseUri.Port};Database={databaseUri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
}

// 3. Подключаем БД
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(finalConnectionString));

// 3. РЕГИСТРАЦИЯ БОТОВ (Читаем токены из переменных окружения для безопасности)
builder.Services.AddKeyedSingleton<ITelegramBotClient>("WorkerBot", (sp, key) => 
{
    var token = builder.Configuration["WorkerBotToken"] ?? "8625228162:AAEXByIhbY5OmsddjkGbngDv8hKd61U07oA";
    return new TelegramBotClient(token);
});

builder.Services.AddKeyedSingleton<ITelegramBotClient>("EmployerBot", (sp, key) => 
{
    var token = builder.Configuration["EmployerBotToken"] ?? "8262319697:AAGWpnSAB7kbxvXlNxRwNpCLBl123wRHYTQ";
    return new TelegramBotClient(token);
});

// ДВИЖКИ (Scoped)
builder.Services.AddScoped<TelegramBotService>();
builder.Services.AddScoped<EmployerBotService>();

// ФОНОВАЯ СЛУЖБА
builder.Services.AddHostedService<BotBackgroundService>();

var app = builder.Build();

// 4. АВТО-МИГРАЦИЯ (Выполняется при каждом запуске на Railway)
using (var scope = app.Services.CreateScope())
{
    try 
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
        Console.WriteLine("✅ Миграции успешно применены.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Ошибка миграции: {ex.Message}");
    }
}

// 5. ДОБАВЛЯЕМ ЭТО, ЧТОБЫ RAILWAY НЕ УБИВАЛ БОТА
app.MapGet("/", () => $"Mangystau Job Hunt Platform is Running! Worker: {DateTime.Now}");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// 6. ПРИВЯЗКА К ПОРТУ (Railway сам назначит порт через переменную PORT)
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");