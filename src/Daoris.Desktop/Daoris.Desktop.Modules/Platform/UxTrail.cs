using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The person's own UX trail, which measures how long finding the press takes: the pages they open and the presses they make, and
/// when a step began waiting on them, kept on this desktop only in the home's entry <see cref="HomeEntry.UxTrail"/>, one file a day, and only while they
/// turned it on (Settings → Driver). Never a field's value or typing: an entry keeps only the fields this class names, each
/// a string clipped to <see cref="Longest"/>, and a kind it does not name is not kept. <c>tools/ux-trail.mjs</c> prints a trail's summary.
/// </summary>
/// <remarks>
/// <para><b>A store of its own.</b> No older build reads the folder, and no record another reads is changed by it, so a build without
/// it opens this home as before. Off is the absence of <c>on</c>; turning it off keeps the files, which the person may delete.</para>
/// <para><b>Bounded.</b> A day's file stops at <see cref="DayCap"/> bytes.</para>
/// </remarks>
public sealed class UxTrail(string home)
{
    /// <summary>The most a day's file holds.</summary>
    public const long DayCap = 2 * 1024 * 1024;

    /// <summary>The longest string an entry keeps.</summary>
    public const int Longest = 120;

    /// <summary>What an entry may be: the window's start, a page opened, a press, a row waiting on the person, a row whose question changed, a row that cleared, the window hidden or shown.</summary>
    public static readonly IReadOnlySet<string> Kinds = new HashSet<string>(StringComparer.Ordinal) { "start", "page", "press", "wait", "changed", "cleared", "hidden", "shown" };

    private const string OnFile = "on";
    private static readonly Lock Gate = new();

    private string Root => DaorisHome.Entry(home, HomeEntry.UxTrail);

    /// <summary>Whether the person turned it on.</summary>
    public bool On => File.Exists(Path.Combine(Root, OnFile));

    /// <summary>Turns it on or off; off keeps the files.</summary>
    public void Set(bool on)
    {
        lock (Gate)
        {
            if (on)
            {
                Directory.CreateDirectory(Root);
                File.WriteAllText(Path.Combine(Root, OnFile), "");
            }
            else if (File.Exists(Path.Combine(Root, OnFile)))
            {
                File.Delete(Path.Combine(Root, OnFile));
            }
        }
    }

    /// <summary>Keeps each entry of a kind it names, with only its named fields, in the day's file; how many it kept, none while off.</summary>
    public int Append(IEnumerable<UxTrailEntry> entries, DateTimeOffset now)
    {
        var lines = new List<string>();
        foreach (var entry in entries)
        {
            if (!Kinds.Contains(entry.Kind)) continue;
            var kept = new JsonObject();
            void Put(string field, string? said)
            {
                if (said is { Length: > 0 }) kept[field] = said.Length > Longest ? said[..Longest] : said;
            }

            Put("at", entry.At ?? now.ToString("O"));
            Put("kind", entry.Kind);
            Put("page", entry.Page);
            Put("from", entry.From);
            Put("press", entry.Press);
            Put("row", entry.Row);
            Put("since", entry.Since);
            lines.Add(kept.ToJsonString());
        }

        lock (Gate)
        {
            if (!On || lines.Count == 0) return 0;
            var day = Path.Combine(Root, $"{now:yyyy-MM-dd}.jsonl");
            if (File.Exists(day) && new FileInfo(day).Length >= DayCap) return 0;
            File.AppendAllLines(day, lines);
            return lines.Count;
        }
    }
}

/// <summary>
/// One entry of the trail as a window sends it: the only fields kept, so a value or typing a window sent has nowhere to land.
/// </summary>
/// <param name="Kind">One of <see cref="UxTrail.Kinds"/>.</param>
/// <param name="At">When, as the window's clock said it; the desktop's where none.</param>
/// <param name="Page">The page it is on, or opens.</param>
/// <param name="From">The page a page opened came from.</param>
/// <param name="Press">A press's control's words.</param>
/// <param name="Row">The <i>What needs you</i> row a press is made in, or a wait is.</param>
/// <param name="Since">Since when a row has waited, as the driver judged it.</param>
public sealed record UxTrailEntry(string Kind, string? At = null, string? Page = null, string? From = null, string? Press = null, string? Row = null, string? Since = null);
