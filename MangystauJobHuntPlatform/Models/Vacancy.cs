namespace MangystauJobHuntPlatform.Models;

public class Vacancy
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string District { get; set; } // Например, "14 мкр" 
    public string RequiredSkills { get; set; }
    public long OwnerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}