namespace Daoris.Driver;

/// <summary>One live decision, whole; only ever read or changed under its session's lock, held in <see cref="SupervisorBook.Live"/>.</summary>
internal sealed class SupervisorLiveDecision
{
    public required string Id { get; init; }
    public required string Session { get; init; }
    public required string Repository { get; init; }
    public required ParkQuestion Question { get; init; }
    public required string Choice { get; init; }

    /// <summary>A defer's bound (<c>left</c>), its choice empty; null for an answer.</summary>
    public string? Bound { get; init; }

    public required IReadOnlyList<SupervisorReason> Reasons { get; init; }
    public required SupervisorHolder Holder { get; init; }
    public required string Generation { get; init; }
    public required DateTimeOffset KeptAt { get; init; }
    public required DateTimeOffset GoesOnAt { get; init; }
    public required string Written { get; init; }
    public List<string> Lines { get; } = [];
    public SupervisorState State { get; set; } = SupervisorState.Held;
    public DateTimeOffset StateAt { get; set; }
    public string? Why { get; set; }
    public string? Receipt { get; set; }
    public string? Word { get; set; }

    /// <summary>Whether the word's take-back is called: a stale decision's word not yet taken back is the next sweep's.</summary>
    public bool TakenBack { get; set; }

    public SupervisorTicket? Ticket { get; set; }
}
