namespace SpanishPractice.Api.Services;

public class EditDistanceService
{
    public int Calculate(string source, string target)
    {
        if (string.IsNullOrEmpty(source)) return target.Length;
        if (string.IsNullOrEmpty(target)) return source.Length;

        var dp = new int[source.Length + 1, target.Length + 1];

        for (var i = 0; i <= source.Length; i++) dp[i, 0] = i;
        for (var j = 0; j <= target.Length; j++) dp[0, j] = j;

        for (var i = 1; i <= source.Length; i++)
        {
            for (var j = 1; j <= target.Length; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[source.Length, target.Length];
    }
}
