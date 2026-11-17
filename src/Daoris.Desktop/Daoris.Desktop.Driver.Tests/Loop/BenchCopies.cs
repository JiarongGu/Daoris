using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The bench's ended records past the few it opens: copies of one record's row, each a record of its own, on a line and a landing entry
/// of its own, with no tree, last moved days ago, as a machine keeps its ended chats.
/// </summary>
/// <remarks>
/// A copy that kept the template's line would be a part of the template's line, and the list answers each record its line's turns, so
/// thousands of copies on one line made the list's answer grow with the square of their count.
/// </remarks>
internal static class BenchCopies
{
    /// <summary><paramref name="count"/> copies of <paramref name="template"/>'s row in <paramref name="connection"/>'s <c>sessions</c>, in one transaction.</summary>
    public static void Insert(SqliteConnection connection, string template, int count, DateTimeOffset now)
    {
        var columns = new List<string>();
        using (var info = connection.CreateCommand())
        {
            info.CommandText = "PRAGMA table_info(sessions)";
            using var read = info.ExecuteReader();
            while (read.Read()) columns.Add(read.GetString(1));
        }

        static string Value(string column) => column switch
        {
            "id" or "line" => "$id",
            "entry" => "REPLACE(entry, $template, $id)",
            "revision" => "(SELECT MAX(revision) FROM sessions) + 1",
            "tree" or "base_commit" => "NULL",
            "created" or "updated" => "$at",
            _ => column,
        };

        using var transaction = connection.BeginTransaction();
        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = $"INSERT INTO sessions ({string.Join(", ", columns)}) SELECT {string.Join(", ", columns.Select(Value))} FROM sessions WHERE id = $template";
        var id = insert.Parameters.Add("$id", SqliteType.Text);
        var at = insert.Parameters.Add("$at", SqliteType.Text);
        insert.Parameters.AddWithValue("$template", template);
        for (var each = 0; each < count; each++)
        {
            id.Value = $"{each:x8}";
            at.Value = now.AddDays(-3).AddMinutes(-each).ToString("O");
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }
}
