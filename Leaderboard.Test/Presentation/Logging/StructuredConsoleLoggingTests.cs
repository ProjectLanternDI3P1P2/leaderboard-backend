using Leaderboard.Presentation.Extensions.LogExtension;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Leaderboard.Test.Presentation.Logging;

public sealed class StructuredConsoleLoggingTests
{
    [Theory]
    [InlineData(LogEventLevel.Verbose, "debug")]
    [InlineData(LogEventLevel.Debug, "debug")]
    [InlineData(LogEventLevel.Information, "information")]
    [InlineData(LogEventLevel.Warning, "warning")]
    [InlineData(LogEventLevel.Error, "error")]
    [InlineData(LogEventLevel.Fatal, "fatal")]
    public void LevelsUseTheDocumentedNames(LogEventLevel level, string expected)
    {
        Assert.Equal(expected, level.ToShortString());
    }

    [Fact]
    public void SensitiveFieldsAreRemovedWhileCorrelationAndUsefulPropertiesRemain()
    {
        var sink = new CaptureSink();
        using var logger = new LoggerConfiguration()
            .Enrich.With<SensitiveDataEnricher>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger
            .ForContext("CorrelationId", "cid-42")
            .ForContext("Password", "private-password")
            .ForContext(
                "Details",
                new
                {
                    Status = "ok",
                    ApiKey = "private-key",
                    Nested = new { Cookie = "private-cookie", Note = "Bearer private-bearer" },
                },
                destructureObjects: true
            )
            .ForContext("Items", new[] { "Bearer private-item" }, destructureObjects: true)
            .ForContext(
                "Metadata",
                new Dictionary<string, string>
                {
                    ["token"] = "private-token",
                    ["safe"] = "Bearer private-value",
                },
                destructureObjects: true
            )
            .Information("Completed {OperationId}", "op-42");

        LogEvent logEvent = Assert.IsType<LogEvent>(sink.Captured);
        Assert.Equal("\"cid-42\"", logEvent.Properties["CorrelationId"].ToString());
        Assert.Equal("\"op-42\"", logEvent.Properties["OperationId"].ToString());
        Assert.False(logEvent.Properties.ContainsKey("Password"));
        string values = string.Join(
            " ",
            logEvent.Properties.Values.Select(value => value.ToString())
        );
        Assert.DoesNotContain("private-", values);
        Assert.Contains("Status", values);
        Assert.Contains("safe", values);
    }

    private sealed class CaptureSink : ILogEventSink
    {
        public LogEvent? Captured { get; private set; }

        public void Emit(LogEvent logEvent) => Captured = logEvent;
    }
}
