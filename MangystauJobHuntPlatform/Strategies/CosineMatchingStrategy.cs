using MangystauJobHuntPlatform.Interface;

namespace MangystauJobHuntPlatform.Strategies;

public class CosineMatchingStrategy : IMatchingStrategy
{
    public double CalculateScore(string userSkills, string jobRequirements)
    {
        if (string.IsNullOrWhiteSpace(userSkills) || string.IsNullOrWhiteSpace(jobRequirements))
            return 0;

        var s1 = userSkills.ToLower().Split(',', StringSplitOptions.TrimEntries);
        var s2 = jobRequirements.ToLower().Split(',', StringSplitOptions.TrimEntries);

        var allWords = s1.Union(s2).Distinct().ToList();

        var v1 = allWords.Select(w => s1.Contains(w) ? 1.0 : 0.0).ToArray();
        var v2 = allWords.Select(w => s2.Contains(w) ? 1.0 : 0.0).ToArray();

        return ComputeCosineSimilarity(v1, v2);
    }

    private double ComputeCosineSimilarity(double[] v1, double[] v2)
    {
        double dotProduct = v1.Zip(v2, (a, b) => a * b).Sum();
        double mag1 = Math.Sqrt(v1.Sum(a => a * a));
        double mag2 = Math.Sqrt(v2.Sum(b => b * b));

        if (mag1 == 0 || mag2 == 0) return 0;
        return dotProduct / (mag1 * mag2);
    }
}