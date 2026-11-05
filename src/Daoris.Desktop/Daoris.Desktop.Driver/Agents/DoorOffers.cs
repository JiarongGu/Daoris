namespace Daoris.Driver;

/// <summary>
/// What a door offers for one start, in two scopes kept apart (the agent-provider design §2.8, §4.2, row n): what a <b>session</b> may carry,
/// which the door's open last listed on this account, and what the account's file may <b>default</b> to, which the provider's settings schema
/// states. An effort a session may carry is not always one a default may hold (<c>max</c> lives for one session only), and neither list is
/// the other's, so a caller names the scope it reads.
/// </summary>
/// <param name="Door">The door the offers are of.</param>
/// <param name="Version">The version of the program they were listed at.</param>
/// <param name="Account">The account they were listed on, or null for the tool's own sign-in.</param>
/// <param name="Session">What an open of this door, version and account last listed.</param>
/// <param name="Defaults">What the account's file may default to; empty where the provider states no schema.</param>
public sealed record DoorOffers(string Door, string? Version, string? Account, SessionOffers Session, AccountDefaultOffers Defaults);

/// <summary>
/// The session scope: the values the door's open listed on the account, and the models it refused there, each only while it is this start's
/// version, since an older or other-version reading is a hint and the account is tried (D205 §9.2).
/// </summary>
/// <param name="Models">The model values listed, or null where the open listed none or no reading stands at this version.</param>
/// <param name="Efforts">The effort values listed, as <paramref name="Models"/>.</param>
/// <param name="Refused">The models refused at an open within the day that passes an account.</param>
public sealed record SessionOffers(IReadOnlyList<string>? Models, IReadOnlyList<string>? Efforts, IReadOnlyList<string> Refused)
{
    /// <summary>No reading stands: every account is tried.</summary>
    public static SessionOffers None { get; } = new(null, null, []);

    /// <summary>When the account last refused a model at an open, where it has; what a refused start names as its moment.</summary>
    public DateTimeOffset? RefusedAt { get; init; }

    /// <summary>
    /// Why this account passes <paramref name="model"/> (<see cref="AgentOffers.NotOffered"/> where its list lacks it, <see cref="AgentOffers.RefusedCode"/>
    /// where the list offered it and the open refused it anyway), or null where the account is tried. A list that lacks a model is never a pass
    /// by itself (MODELROLE1a): only the agent's own refusal at an open passes an account.
    /// </summary>
    public string? Judge(string? model)
    {
        if (string.IsNullOrWhiteSpace(model) || !Refused.Contains(model, StringComparer.OrdinalIgnoreCase)) return null;
        return Models?.Contains(model, StringComparer.OrdinalIgnoreCase) == true ? AgentOffers.RefusedCode : AgentOffers.NotOffered;
    }
}

/// <summary>The account-default scope: the efforts a default in the account's own file may hold.</summary>
/// <param name="Efforts">The efforts the provider's settings schema keeps.</param>
public sealed record AccountDefaultOffers(IReadOnlyList<string> Efforts)
{
    /// <summary>A provider that states no settings schema offers no default.</summary>
    public static AccountDefaultOffers None { get; } = new([]);
}
