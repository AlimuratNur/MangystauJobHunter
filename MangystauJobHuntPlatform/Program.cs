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
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");

// 2. Если её нет, ищем в конфигурации (Variables на Railway или appsettings.json)
if (string.IsNullOrEmpty(connectionString))
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
}

// 3. Если это строка в формате postgres:// (стандарт Railway), её нужно привести к формату .NET
if (connectionString != null && connectionString.StartsWith("postgres://"))
{
    var uri = new Uri(connectionString);
    var userInfo = uri.UserInfo.Split(':');
    connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
}

Console.WriteLine($"[DB Debug] Итоговая строка подключения: {connectionString?.Split(';')[0]}..."); // Выведет только хост для безопасности

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

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