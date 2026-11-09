using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One choice of the person's for a path read as written: its scope (<c>machine</c> or a repository's name) and its wording.</summary>
public sealed record FilesWording(string Scope, string Path, bool Whole);

/// <summary>
/// The person's file moved from the first form to git's (the tree-pool design §38.1), by its one writer under its lock, the form read
/// again under that lock so two readers rewrite it once: on its own where every rule of it translates exactly and reads, its first
/// form set aside beside it; and where a path is read as written, only by the person's wording of every such path in one write.
/// </summary>
internal static class PersonFilesForm
{
    internal const string SetAsideName = "files.first-form.json";

    /// <summary>Whether a first-form file translates exactly and whole, so its reader rewrites it.</summary>
    internal static bool Due(PersonFilesRead read) => read is { FirstForm: true, Unread: null, Keeps: false, Problems.Count: 0 };

    /// <summary>The file rewritten in git's form where it is still due under the lock; true where it wrote.</summary>
    internal static bool Rewrite(string home)
    {
        try
        {
            return Write(home, (read, root) => Due(read) ? Translated(root, read) : null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    /// <summary>The file rewritten in git's form with each path read as written worded as <paramref name="wordings"/> say.</summary>
    /// <exception cref="DriverException">The file does not read, a rule of it does not, or a path read as written has no wording.</exception>
    internal static void Settle(string home, IReadOnlyList<FilesWording> wordings)
    {
        var written = Write(home, (read, root) =>
        {
            if (read.Unread is { } why) throw new DriverException($"{PersonFiles.FileName} does not read ({why}), so nothing was written over it.");
            return read is { FirstForm: true, Keeps: true } ? Translated(root, read, wordings) : null;
        });
        if (!written) throw new DriverException($"{PersonFiles.FileName} holds no path read as written: nothing to word.");
    }

    /// <summary>
    /// <paramref name="root"/> in git's form: <c>form</c> written and each scope's rules replaced by its entries, every other key kept.
    /// </summary>
    /// <exception cref="DriverException">A rule does not read, or a path read as written has no wording.</exception>
    internal static JsonObject Translated(JsonObject root, PersonFilesRead read, IReadOnlyList<FilesWording>? wordings = null)
    {
        if (read.Problems.Count > 0)
        {
            throw new DriverException($"{PersonFiles.FileName} is read in its first form and {read.Problems[0]}: fix that rule, then make the change again.");
        }

        var moved = (JsonObject)root.DeepClone();
        moved["form"] = FileRuleFile.Form;
        if (moved["machine"] is JsonObject machine) machine["rules"] = Entries("machine", read.Machine, wordings);
        if (moved["repositories"] is JsonObject repositories)
        {
            foreach (var (name, scope) in repositories.ToList())
            {
                if (scope is JsonObject each) each["rules"] = Entries(name, read.For(name), wordings);
            }
        }

        return moved;
    }

    private static JsonArray Entries(string scope, FileScope read, IReadOnlyList<FilesWording>? wordings)
    {
        var entries = new List<FileRule>();
        foreach (var rule in read.Rules)
        {
            if (!rule.FirstForm)
            {
                entries.Add(rule);
                continue;
            }

            foreach (var path in rule.Paths)
            {
                var chosen = wordings?.FirstOrDefault(each => string.Equals(each.Scope, scope, StringComparison.OrdinalIgnoreCase) && each.Path == path)
                    ?? throw new DriverException($"the {scope} rule's '{path}' is read as written, and git's syntax states it only as you word it: choose a wording for it first.");
                entries.AddRange(RuleFileFirstForm.Worded(rule, path, chosen.Whole));
            }
        }

        return [.. entries.Select(entry => (JsonNode)FileRuleFile.Written(entry))];
    }

    /// <summary>Under the file's lock, read again, then written as <paramref name="moved"/> answers, its first form set aside beside it.</summary>
    private static bool Write(string home, Func<PersonFilesRead, JsonObject, JsonObject?> moved)
    {
        var path = PersonFiles.PathOf(home);
        using var held = RecordLock.Take(RecordLock.PathOf(home, PersonFiles.LockName));
        if (!File.Exists(path)) return false;
        var text = AtomicFile.ReadText(path);
        if (moved(PersonFiles.Parse(text), JsonNode.Parse(text.TrimStart('﻿')) as JsonObject ?? []) is not { } root) return false;
        AtomicFile.WriteText(Path.Combine(Path.GetDirectoryName(path)!, SetAsideName), text);
        AtomicFile.WriteText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n") + "\n");
        return true;
    }
}
