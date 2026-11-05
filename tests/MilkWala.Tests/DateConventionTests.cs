using System.Reflection;
using MilkWala.Models;
using SQLite;
using Xunit;

namespace MilkWala.Tests;

/// <summary>
/// Guards the date convention documented at the top of Models/Entities.cs:
/// business timestamps default to local time, sync/audit metadata to UTC.
/// </summary>
public class DateConventionTests
{
    // UTC metadata, as "Entity.Property". UpdatedAt is UTC on every entity.
    private static readonly HashSet<string> UtcFields = new()
    {
        "User.CreatedAt",
        "LoginSession.LoginTime",
        "SyncQueueItem.CreatedAt",
        "SyncLog.SyncTime",
    };

    private static bool ExpectsUtc(Type entity, PropertyInfo property) =>
        property.Name == "UpdatedAt" || UtcFields.Contains($"{entity.Name}.{property.Name}");

    private static IEnumerable<Type> Entities() =>
        typeof(User).Assembly.GetTypes()
            .Where(t => t.Namespace == "MilkWala.Models" && t.GetCustomAttribute<TableAttribute>() != null);

    [Fact]
    public void Entities_were_discovered()
    {
        Assert.True(Entities().Count() >= 15);
    }

    [Fact]
    public void Defaults_follow_the_convention()
    {
        var violations = new List<string>();

        foreach (var entity in Entities())
        {
            var instance = Activator.CreateInstance(entity)!;
            foreach (var property in entity.GetProperties().Where(p => p.PropertyType == typeof(DateTime)))
            {
                var value = (DateTime)property.GetValue(instance)!;
                if (value == default)
                    continue; // no default: the caller supplies the business date

                var expected = ExpectsUtc(entity, property) ? DateTimeKind.Utc : DateTimeKind.Local;
                if (value.Kind != expected)
                    violations.Add($"{entity.Name}.{property.Name} defaults to {value.Kind}, expected {expected}");
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }
}
