using System.IO;
using System.Linq;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Xunit;
using ForgeRise.Api.Welfare;

namespace ForgeRise.Api.Tests.Welfare;

public class WelfareDestructuringPolicyTests
{
    private sealed record Snapshot(string PlayerId, int SleepHours, int SorenessScore, string Readiness);

    [Fact]
    public void Redacts_raw_welfare_fields_when_destructured_by_serilog()
    {
        using var sw = new StringWriter();
        var log = new LoggerConfiguration()
            .Destructure.With(new WelfareDestructuringPolicy())
            .WriteTo.Sink(new TextWriterSink(sw))
            .MinimumLevel.Verbose()
            .CreateLogger();

        log.Information("snapshot {@Snapshot}", new Snapshot("p_1", 4, 7, "monitor"));

        var output = sw.ToString();
        Assert.Contains("\"playerId\"", output, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", output);
        Assert.DoesNotContain("\"sleepHours\":4", output, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"sorenessScore\":7", output, System.StringComparison.OrdinalIgnoreCase);
        // Coach-safe field passes through.
        Assert.Contains("monitor", output);
    }

    [Fact]
    public void Raw_field_set_matches_master_prompt()
    {
        var expected = new[]
        {
            "sleepHours", "sorenessScore", "menstrualPhase", "menstrualSymptoms",
            "moodScore", "stressScore", "fatigueScore", "injuryNotes", "medicalNotes",
        };
        foreach (var name in expected)
        {
            Assert.Contains(name, RawWelfareFields.Names);
        }
    }

    private sealed class TextWriterSink : Serilog.Core.ILogEventSink
    {
        private readonly TextWriter _writer;
        private readonly CompactJsonFormatter _formatter = new();
        public TextWriterSink(TextWriter writer) => _writer = writer;
        public void Emit(LogEvent logEvent) => _formatter.Format(logEvent, _writer);
    }
}
