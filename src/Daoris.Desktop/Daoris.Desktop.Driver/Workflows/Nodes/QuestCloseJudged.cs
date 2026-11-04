using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What a close node may do now (<see cref="QuestClosing.Judge"/>).</summary>
/// <param name="Named">The quests it closes, its chain's in this repository oldest first, each with its current submission; empty when it waits.</param>
/// <param name="Waits">Why nothing is sent now, or null.</param>
/// <param name="Overridden">Every quest it would close is Done already: by its chain's close, or by the person (§E5's override).</param>
public sealed record QuestCloseJudged(IReadOnlyList<QuestCloseItem> Named, string? Waits, bool Overridden);
