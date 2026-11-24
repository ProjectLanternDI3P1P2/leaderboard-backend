namespace Leaderboard.Presentation.Health;

public static class ReadinessProbe
{
    private const string Url = "http://127.0.0.1:8080/health/ready";

    public static async Task<bool> IsReadyAsync(HttpClient client)
    {
        try
        {
            using HttpResponseMessage response = await client.GetAsync(Url);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }
}
