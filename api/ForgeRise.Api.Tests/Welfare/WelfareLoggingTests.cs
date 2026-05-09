using ForgeRise.Api.Welfare;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace ForgeRise.Api.Tests.Welfare;

public class WelfareLoggingTests
{
    private sealed class CaptureSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private static (ILogger logger, CaptureSink sink) BuildLogger()
    {
        var sink = new CaptureSink();
        var logger = new LoggerConfiguration()
            .Destructure.With(new WelfareDestructuringPolicy())
            .WriteTo.Sink(sink)
            .CreateLogger();
        return (logger, sink);
    }

    [Fact]
    public void Welfare_field_names_are_redacted_in_destructured_payload()
    {
        var (logger, sink) = BuildLogger();
        var payload = new
        {
            sleepHours = 4.2,
            injuryNotes = "torn meniscus",
            note = "fine",
        };

        logger.Information("welfare {@Payload}", payload);

        Assert.Single(sink.Events);
        var rendered = sink.Events[0].RenderMessage();
        Assert.DoesNotContain("torn meniscus", rendered);
        Assert.DoesNotContain("4.2", rendered);
        Assert.Contains("REDACTED", rendered);
        // Non-welfare property survives.
        Assert.Contains("fine", rendered);
    }

    [Fact]
    public void Plain_strings_with_welfare_words_are_not_modified()
    {
        // Policy targets property names on destructured objects, not free-form text.
        var (logger, sink) = BuildLogger();
        logger.Information("welfare.checkin.recorded {Category}", SafeCategory.Monitor);

        Assert.Single(sink.Events);
        Assert.Contains("Monitor", sink.Events[0].RenderMessage());
    }
}
