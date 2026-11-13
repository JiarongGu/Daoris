using System.Data.Common;

namespace Daoris.Driver;

/// <summary>Binding a statement's <c>$name</c> parameters on the standard library's command, which has no engine's shortcut for it.</summary>
internal static class StoreCommands
{
    /// <summary>Each value bound under its name; a null is the database's null.</summary>
    internal static void Bind(this DbCommand command, params (string Name, object? Value)[] values)
    {
        foreach (var (name, value) in values) command.Bind(name, value);
    }

    /// <summary>One value bound under <paramref name="name"/>; a null is the database's null.</summary>
    internal static void Bind(this DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
