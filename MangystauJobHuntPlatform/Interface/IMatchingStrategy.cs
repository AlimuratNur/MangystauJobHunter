namespace MangystauJobHuntPlatform.Interface;

public interface IMatchingStrategy
{
    // Возвращает score от 0.0 до 1.0
    double CalculateScore(string userSkills, string jobRequirements);
}