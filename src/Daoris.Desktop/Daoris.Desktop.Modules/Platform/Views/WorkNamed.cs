using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>The ask or the quest a pause or a resume names, found among the service's records.</summary>
internal readonly record struct WorkNamed(string Scope, string Id)
{
    /// <summary>The read of the work's plan that names it, as the route reads it.</summary>
    public object Payload => Scope == "ask" ? new { ask = Id } : new { quest = Id };

    /// <summary>The <c>daoris</c> command that presses <paramref name="verb"/> (<c>pause</c> or <c>resume</c>) for it.</summary>
    public string Command(string verb) => Scope == "ask"
        ? $"daoris ask --{verb} {ShellWord.Of(Id, ShellWord.Name)}"
        : $"daoris quest {verb} {ShellWord.Of(Id, ShellWord.Name)}";

    /// <summary>The one ask or quest the payload names; none, with the words, where it names neither, both, or one the service lacks.</summary>
    public static async Task<(WorkNamed? Named, string? Refusal)> ReadAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var (ask, quest) = (PressPayload.Text(payload, "ask").TrimStart('#'), PressPayload.Text(payload, "quest").TrimStart('#'));
        if (ask.Length == 0 == (quest.Length == 0)) return (null, "a press names one ask or one quest, by its id.");
        if (ask.Length > 0)
        {
            return await world.AskAsync(ask, cancellationToken).ConfigureAwait(false) is null
                ? (null, $"there is no ask `#{ask}` on this machine.")
                : (new WorkNamed("ask", ask), null);
        }

        return await world.QuestAsync(quest, cancellationToken).ConfigureAwait(false) is null
            ? (null, $"there is no quest `#{quest}` on this machine.")
            : (new WorkNamed("quest", quest), null);
    }
}
