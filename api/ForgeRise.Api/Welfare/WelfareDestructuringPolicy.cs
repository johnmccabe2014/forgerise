using System.Diagnostics.CodeAnalysis;
using Serilog.Core;
using Serilog.Events;

namespace ForgeRise.Api.Welfare;

/// <summary>
/// Last line of defence: anything Serilog destructures gets walked, and any
/// property name appearing in <see cref="RawWelfareFields.Names"/> is replaced
/// with "[REDACTED]". Coach-safe categories (e.g. "readiness") are untouched.
///
/// Master prompt §9, §11.
/// </summary>
public sealed class WelfareDestructuringPolicy : IDestructuringPolicy
{
    public bool TryDestructure(object value, ILogEventPropertyValueFactory factory, [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        if (value is null || value is string || value.GetType().IsPrimitive)
        {
            result = null;
            return false;
        }

        // Only intervene for objects we recognise as carrying welfare-shaped properties.
        var props = value.GetType().GetProperties();
        if (!props.Any(p => RawWelfareFields.Names.Contains(p.Name)))
        {
            result = null;
            return false;
        }

        var members = props.Select(p =>
        {
            var raw = SafeGet(p, value);
            var safe = RawWelfareFields.Names.Contains(p.Name)
                ? new ScalarValue("[REDACTED]")
                : factory.CreatePropertyValue(raw, destructureObjects: true);
            return new LogEventProperty(p.Name, safe);
        });

        result = new StructureValue(members, value.GetType().Name);
        return true;
    }

    private static object? SafeGet(System.Reflection.PropertyInfo p, object instance)
    {
        try { return p.GetValue(instance); }
        catch { return null; }
    }
}
