using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using static Daoris.Driver.AccountFileNodes;

namespace Daoris.Driver;

/// <summary>
/// One window as a reader sees it, for the usage history: more than the walk keeps of it, read by
/// a parse beside the walk's own, which it leaves unchanged.
/// </summary>
/// <param name="Window">
/// The window's identity as its maker names it: Claude's key (<c>five_hour</c>, <c>seven_day</c>, <c>seven_day_opus</c>), a
/// row of <c>get_usage</c>'s <c>limits[]</c> by its kind (<c>limits:weekly_scoped</c>), or Codex's by its length
/// (<c>10080-minute</c>), never its slot.
/// </param>
/// <param name="Walk">Daoris's name for it where the walk reads it (<c>session</c>, <c>weekly</c>), else null.</param>
/// <param name="Scope">What it counts where that is not the whole plan: a model family, or Codex's limit; null for the plan's.</param>
/// <param name="Minutes">Its length where its maker says or names it; null where unknown.</param>
/// <param name="Used">How much of it is used, from 0 to 1, or null where no number is said: absent is never zero.</param>
/// <param name="Precision">
/// The least step the number shows as its maker wrote it, as a fraction (0.01 for a whole percent): a floor on what the maker
/// resolves, since a number's trailing zeros are not written. Null with no number, or one written with an exponent.
/// </param>
/// <param name="Reset">When it turns over, or null where none is said or made up.</param>
/// <param name="ResetFrom">
/// Where the reset came from: <see cref="UsageHistory.ResetSaid"/>, or <see cref="UsageHistory.ResetMadeUp"/> for a window
/// not yet opened at zero, whose reset the walk sets a window's length ahead (no boundary to calibrate on); null with none.
/// </param>
public sealed record WindowSeen(
    string Window, string? Walk, string? Scope, int? Minutes, double? Used, double? Precision, DateTimeOffset? Reset, string? ResetFrom);
