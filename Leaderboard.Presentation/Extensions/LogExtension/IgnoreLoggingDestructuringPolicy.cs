using Serilog.Core;
using Serilog.Events;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Leaderboard.Presentation.Extensions.LogExtension;

[AttributeUsage(AttributeTargets.Property)]
public sealed class IgnoreLoggingAttribute : Attribute;

public sealed class IgnoreLoggingDestructuringPolicy : IDestructuringPolicy
{
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        var type = value.GetType();
        var ignoredProperties = type.GetProperties()
            .Where(property => Attribute.IsDefined(property, typeof(IgnoreLoggingAttribute)))
            .ToDictionary(property => property.Name, property => property.GetValue(value));

        if (ignoredProperties.Count == 0)
        {
            result = new StructureValue([]);
            return false;
        }

        var logEventProperties = new List<LogEventProperty>();

        foreach (PropertyInfo propertyInfo in type.GetTypeInfo().DeclaredProperties)
        {
            bool shouldHideProperty = ignoredProperties.GetValueOrDefault(propertyInfo.Name) is not null;
            object? valueToLog = shouldHideProperty
                ? nameof(IgnoreLoggingAttribute)
                : propertyInfo.GetValue(value);

            logEventProperties.Add(new LogEventProperty(
                propertyInfo.Name,
                propertyValueFactory.CreatePropertyValue(valueToLog, true)));
        }

        result = new StructureValue(logEventProperties);
        return true;
    }
}
