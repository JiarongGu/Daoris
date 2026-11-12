namespace Daoris.Driver;

/// <summary>The closed names of what a door can do, each the name of a <see cref="AbilitiesPart"/> member.</summary>
public static class AbilityName
{
    public const string Interactive = nameof(AbilitiesPart.Interactive);
    public const string Resumes = nameof(AbilitiesPart.Resumes);
    public const string TakesSettings = nameof(AbilitiesPart.TakesSettings);
    public const string ServersInherit = nameof(AbilitiesPart.ServersInherit);
    public const string Structured = nameof(AbilitiesPart.Structured);
    public const string WordsAtNextStep = nameof(AbilitiesPart.WordsAtNextStep);
    public const string Steering = nameof(AbilitiesPart.Steering);
    public const string StopKeepsConversation = nameof(AbilitiesPart.StopKeepsConversation);
    public const string OneTaskStopped = nameof(AbilitiesPart.OneTaskStopped);
    public const string ResumeFailsClosed = nameof(AbilitiesPart.ResumeFailsClosed);
    public const string RequestsHeld = nameof(AbilitiesPart.RequestsHeld);
    public const string ServersOnWire = nameof(AbilitiesPart.ServersOnWire);
    public const string LeavesOwnServersOut = nameof(AbilitiesPart.LeavesOwnServersOut);

    public static IReadOnlyList<string> All { get; } =
    [
        Interactive, Resumes, TakesSettings, ServersInherit, Structured, WordsAtNextStep, Steering, StopKeepsConversation,
        OneTaskStopped, ResumeFailsClosed, RequestsHeld, ServersOnWire, LeavesOwnServersOut,
    ];
}
