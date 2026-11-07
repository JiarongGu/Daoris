using static Daoris.Knowledge.HelpProposalFile;

namespace Daoris.Knowledge;

/// <summary>
/// A place's quick set-up a <c>place</c> proposal asks: the delivery preset, the other repositories beside the proposal's own,
/// the second opinion and the person's look, adoption with what it builds, and general driving. Shape alone is checked in the box.
/// </summary>
public sealed record HelpSetupPlace(
    string Preset, IReadOnlyList<string>? Repositories = null, bool Opinion = false, bool Look = false, bool Adoption = false, string? Builds = null,
    bool Drive = false);

/// <summary>
/// The <c>setup</c> kind's writer: a place's quick set-up of one or several registered repositories, its one door
/// <c>place</c>.
/// </summary>
/// <remarks>
/// The file keeps its <c>door</c> and its common <c>workspace</c>, written null, so the doors added to this kind
/// (<c>register</c>, and a workspace's plan with its pause, resume and stop) are a door and a field of the same file.
/// </remarks>
internal sealed class HelpSetupProposalWriter(HelpProposalFile file)
{
    /// <summary>
    /// Write one proposal to set repositories up: the repository as the registry names it, its <c>target</c>, and the place's others
    /// make the ask, which the person's page plans and applies (<c>daoris place … --adoption</c>'s twin: one quest to each
    /// session, and the rule it needs to run the doctrine tool). A set-up needs no path, so none is written. Whether the apply would
    /// publish (registered here, a tree of its own, the doctrine tool found, none open) is the driver's to judge, by the plan's refusals.
    /// </summary>
    public (string? Id, string Message) Propose(string repository, string why, string? session, DateTimeOffset at, HelpSetupPlace? place = null)
    {
        // A set-up is always a place's, whose delivery is named.
        place ??= new HelpSetupPlace("");
        var named = Blank(repository);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var repositories = new[] { named }.Concat(place?.Repositories ?? []).Select(Blank).OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var preset = Blank(place?.Preset);
        var refused = named is null ? "a set-up names the repository to set up, as Repositories lists it."
            : preset is null ? "a set-up names its delivery, the `preset` (`keep` leaves how its work lands as it is)."
            : Word(preset, "a delivery") is { } delivery ? delivery
            : Blank(place.Builds) is { } builds && Word(builds, "what it builds") is { } shape ? shape
            : repositories.Any(each => each.IndexOfAny(['/', '\\']) >= 0 || Path.IsPathRooted(each))
                ? "a repository is named as Repositories lists it, never by its folder: a set-up needs no path."
            : repositories.Select(each => Word(each, "a repository")).FirstOrDefault(problem => problem is not null);
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return file.Write(writer =>
        {
            writer.WriteString("kind", "setup");
            writer.WriteString("door", "place");
            writer.WriteString("target", named);
            writer.WriteNull("workspace");
            writer.WriteNull("value");
            writer.WriteNull("sentence");
            writer.WriteStartObject("place");
            writer.WriteString("preset", preset);
            writer.WriteStartArray("repositories");
            foreach (var each in repositories) writer.WriteStringValue(each);
            writer.WriteEndArray();
            writer.WriteBoolean("opinion", place.Opinion);
            writer.WriteBoolean("look", place.Look);
            writer.WriteBoolean("adoption", place.Adoption);
            Nullable(writer, "builds", Blank(place.Builds));
            writer.WriteBoolean("drive", place.Drive);
            writer.WriteEndObject();
        }, why, session, at);
    }
}
