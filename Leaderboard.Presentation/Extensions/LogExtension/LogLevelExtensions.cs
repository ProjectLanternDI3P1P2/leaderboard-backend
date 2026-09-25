using Serilog.Events;

namespace Leaderboard.Presentation.Extensions.LogExtension;

public static class LogLevelExtensions
{
    public static string ToShortString(this LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose => "debug",
            LogEventLevel.Debug => "debug",
            LogEventLevel.Information => "info",
            LogEventLevel.Warning => "warn",
            LogEventLevel.Error => "error",
            LogEventLevel.Fatal => "fatal",
            _ => level.ToString().ToLowerInvariant()
        };
    }
}
