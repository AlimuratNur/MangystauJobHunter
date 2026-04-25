using MangystauJobHuntPlatform.DB;
using MangystauJobHuntPlatform.Interface;
using MangystauJobHuntPlatform.Models;
using MangystauJobHuntPlatform.Services;
using MangystauJobHuntPlatform.Strategies;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);



builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddHttpClient(); // Важно для IHttpClientFactory

// AI и стратегия
builder.Services.AddScoped<IMatchingStrategy, CosineMatchingStrategy>();
builder.Services.AddHttpClient<AiGeocodingService>();
builder.Services.AddScoped<AiGeocodingService>();

// База данных
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));;


// РЕГИСТРАЦИЯ БОТОВ
builder.Services.AddKeyedSingleton<ITelegramBotClient>("WorkerBot", (sp, key) => 
    new TelegramBotClient("8296938940:AAFa3S59LR7zVmQK7HNyXRGdkbF8Fb2Vjos"));

builder.Services.AddKeyedSingleton<ITelegramBotClient>("EmployerBot", (sp, key) => 
    new TelegramBotClient("8262319697:AAGWpnSAB7kbxvXlNxRwNpCLBl123wRHYTQ"));

// ДВИЖКИ (Scoped)
builder.Services.AddScoped<TelegramBotService>();
builder.Services.AddScoped<EmployerBotService>();

// ФОНОВАЯ СЛУЖБА (Singleton)
builder.Services.AddHostedService<BotBackgroundService>();

var app = builder.Build();
// ... дальше стандартно


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();




app.Run();

