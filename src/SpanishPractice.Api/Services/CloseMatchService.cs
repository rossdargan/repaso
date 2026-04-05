namespace SpanishPractice.Api.Services;

public class CloseMatchService(EditDistanceService editDistanceService)
{
    public (bool IsClose, int Distance) Evaluate(string submitted, string expected)
    {
        var distance = editDistanceService.Calculate(submitted, expected);
        var maxLength = Math.Max(submitted.Length, expected.Length);

        if (maxLength <= 4)
        {
            return (distance <= 1, distance);
        }

        if (maxLength <= 8)
        {
            return (distance <= 2, distance);
        }

        return (distance <= 3, distance);
    }
}
