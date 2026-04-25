using MangystauJobHuntPlatform.DB;
using MangystauJobHuntPlatform.Interface;
using MangystauJobHuntPlatform.Models;
using MangystauJobHuntPlatform.Services;
using MangystauJobHuntPlatform.Strategies;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
// Если захочешь сменить AI — просто замени CosineMatchingStrategy на другую реализацию
builder.Services.AddScoped<IMatchingStrategy, CosineMatchingStrategy>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=mangystau_jobs.db"));

builder.Services.AddControllers();

builder.Services.AddHttpClient("tgwebhook")
    .AddTypedClient<ITelegramBotClient>(httpClient => 
        new TelegramBotClient("8296938940:AAFa3S59LR7zVmQK7HNyXRGdkbF8Fb2Vjos", httpClient));
builder.Services.AddHttpClient("tgwebhook")
    .AddTypedClient<ITelegramBotClient>(httpClient => 
        new TelegramBotClient("8262319697:AAGWpnSAB7kbxvXlNxRwNpCLBl123wRHYTQ", httpClient));

// Регистрируем наш движок бота
builder.Services.AddScoped<TelegramBotService>();

// Фоновая служба, которая слушает сообщения
builder.Services.AddHostedService<BotBackgroundService>();

// Сначала HTTP клиент (нужен для AI)
builder.Services.AddHttpClient<AiGeocodingService>();

// AI сервис
builder.Services.AddScoped<AiGeocodingService>();


var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();




app.Run();

