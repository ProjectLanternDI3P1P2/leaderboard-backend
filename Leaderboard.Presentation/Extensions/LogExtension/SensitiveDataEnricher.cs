using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace Leaderboard.Presentation.Extensions.LogExtension;

/// <summary>Remove credential and personal-data fields before the console formatter sees them.</summary>
public sealed class SensitiveDataEnricher : ILogEventEnricher
{
    private static readonly Regex SensitiveKey = new(
        "password|secret|token|authorization|cookie|apikey|connectionstring|payload|requestbody|email",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100)
    );

    private static readonly Regex SensitiveText = new(
        @"\b(?:Bearer\s+\S+|eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+|[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100)
    );

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (
            KeyValuePair<string, LogEventPropertyValue> property in logEvent.Properties.ToArray()
        )
        {
            if (SensitiveKey.IsMatch(property.Key))
            {
                logEvent.RemovePropertyIfPresent(property.Key);
                continue;
            }

            LogEventPropertyValue safeValue = Redact(property.Value);
            if (!ReferenceEquals(safeValue, property.Value))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(property.Key, safeValue));
            }
        }
    }

    private static LogEventPropertyValue Redact(LogEventPropertyValue value) =>
        value switch
        {
            ScalarValue { Value: string text } => new ScalarValue(
                SensitiveText.Replace(text, "[redacted]")
            ),
            StructureValue structure => new StructureValue(
                structure
                    .Properties.Where(property => !SensitiveKey.IsMatch(property.Name))
                    .Select(property => new LogEventProperty(
                        property.Name,
                        Redact(property.Value)
                    )),
                structure.TypeTag
            ),
            SequenceValue sequence => new SequenceValue(sequence.Elements.Select(Redact)),
            DictionaryValue dictionary => new DictionaryValue(
                dictionary
                    .Elements.Where(entry =>
                        entry.Key.Value is not string key || !SensitiveKey.IsMatch(key)
                    )
                    .Select(entry => new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                        entry.Key,
                        Redact(entry.Value)
                    ))
            ),
            _ => value,
        };
}
