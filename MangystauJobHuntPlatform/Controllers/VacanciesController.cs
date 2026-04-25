using MangystauJobHuntPlatform.DB;
using MangystauJobHuntPlatform.Interface;
using MangystauJobHuntPlatform.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace MangystauJobHuntPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VacanciesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMatchingStrategy _aiEngine;

    public VacanciesController(AppDbContext db, IMatchingStrategy aiEngine)
    {
        _db = db;
        _aiEngine = aiEngine;
    }

    [HttpGet("match/{tgId}")]
    public async Task<IActionResult> GetRecommendedVacancies(long tgId)
    {
        // Поиск соискателя
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.TelegramId == tgId);
        if (user == null) return NotFound("Пользователь не найден");

        // Загрузка вакансий из БД 
        var vacancies = await _db.Vacancies.AsNoTracking().ToListAsync();

        // Ранжирование через AI-модуль 
        var result = vacancies
            .Select(v => new
            {
                Vacancy = v,
                Score = _aiEngine.CalculateScore(user.Skills, v.RequiredSkills)
            })
            .OrderByDescending(r => r.Score)
            .ToList();

        return Ok(result);
    }

    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] ApplicationRequest request)
    {
        // Реализация отклика 
        var application = new Application
        {
            VacancyId = request.VacancyId,
            CandidateId = request.UserId,
            Status = "Sent",
            CreatedAt = DateTime.UtcNow
        };

        _db.Applications.Add(application);
        await _db.SaveChangesAsync(); // Сохранение в SQLite 

        return Ok(new { Message = "Отклик успешно отправлен!" });
    }
}

public record ApplicationRequest(int VacancyId, int UserId);