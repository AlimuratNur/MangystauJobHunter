namespace MangystauJobHuntPlatform.Models;

public class Application
{
    public int Id { get; set; }
    public int VacancyId { get; set; }
    public int CandidateId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
}