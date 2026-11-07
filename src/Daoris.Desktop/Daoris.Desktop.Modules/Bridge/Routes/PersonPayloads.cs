namespace Daoris.Desktop;

/// <summary>The person's Confirm or Refuse on a press; <c>Source</c> is <c>service</c> for one waiting at the service's door.</summary>
public sealed record PersonConfirmPress(string Id, bool Confirm, string? Source);

/// <summary>A confirmation shown to the person.</summary>
public sealed record PersonConfirmShown(string Id);

/// <summary>The role whose terminal starts; <c>Share</c> is the choice while a holder lives.</summary>
public sealed record RoleTerminalPress(string Role, string? Share);
