using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A session's config options: as the agent last said them, the list its open began with, the person's change of one, and
/// the actor's choice carried onto the open session. The session hands it a line for its console, the one request door and
/// the id the agent gave it.
/// </summary>
internal sealed class AcpOptions(
    Action<string> say,
    Action<IReadOnlyList<AcpConfigOption>>? told,
    Func<string, object, CancellationToken, Task<JsonElement>> request,
    Func<string?> sessionId)
{
    private readonly object _gate = new();
    private IReadOnlyList<AcpConfigOption> _options = [];

    /// <summary>What this start's actor asked: set on the open session by category; null sets nothing.</summary>
    public ActorAsked? Carry { get; set; }

    /// <summary>Told what is carried where it differs from <see cref="Carry"/>, so the record never says a choice is taken that is not.</summary>
    public Action<ActorAsked>? Carried { get; set; }

    /// <summary>Asked where the agent does not offer, or refuses, the model at the open: the re-ask, or null to end the open.</summary>
    public IChoiceReask? ModelRefused { get; set; }

    /// <summary>Told the options the open listed, before the posture or any set.</summary>
    public Action<IReadOnlyList<AcpConfigOption>>? OffersRead { get; set; }

    /// <summary>The options as the open listed them, never overwritten: what a refused attempt's categories are set back to.</summary>
    public IReadOnlyList<AcpConfigOption> OpeningOptions { get; private set; } = [];

    /// <summary>The refusal that ended the open before any prompt, or null.</summary>
    public ModelRefusal? NotOffered { get; private set; }

    /// <summary>Only the list the last setter actually returned; null when it returned none.</summary>
    public IReadOnlyList<AcpConfigOption>? LastAcknowledged { get; private set; }

    /// <summary>The options as the agent last said them, the mode included; empty before the open or where it offers none.</summary>
    public IReadOnlyList<AcpConfigOption> Current
    {
        get { lock (_gate) return _options; }
    }

    /// <summary>Keep what the agent said its options are now, and tell whoever asked.</summary>
    public void Keep(IReadOnlyList<AcpConfigOption> options)
    {
        lock (_gate) _options = options;
        try
        {
            told?.Invoke(options);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // A listener's failure is its own: the options are kept, and the turn goes on.
            say($"[the session's options could not be told: {error.Message}]");
        }
    }

    /// <summary>
    /// Change one option with <c>session/set_config_option</c>: the agent's answer is its whole list after the change (an effort's levels
    /// follow the model), kept and returned. An answer with no list leaves the others as they were and this one at the value asked for.
    /// Which options a person may set is the caller's judgement.
    /// </summary>
    /// <exception cref="DriverException">The session is not open, or the agent refused the change — in its words.</exception>
    public async Task<IReadOnlyList<AcpConfigOption>> SetAsync(string configId, string value, CancellationToken ct)
    {
        if (sessionId() is not { } id) throw new DriverException("this ACP session is not open — nothing can be set on it.");

        var answer = await request("session/set_config_option", new { sessionId = id, configId, value }, ct).ConfigureAwait(false);
        var returned = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("configOptions", out var list)
                    && list.ValueKind == JsonValueKind.Array
            ? AcpConfigOption.Read(list)
            : null;
        LastAcknowledged = returned;
        // Preserve the composer's existing reading; provenance below never mistakes this local projection for an acknowledgement.
        var after = returned ?? [.. Current.Select(option => option.Id == configId ? option with { Current = value } : option)];
        Keep(after);
        return after;
    }

    /// <summary>The opening's snapshot, kept once, and told as the agent's offer reading.</summary>
    public void KeepOpening()
    {
        OpeningOptions = Current;
        try
        {
            OffersRead?.Invoke(OpeningOptions);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            say($"[the session's offers could not be kept: {error.Message}]");
        }
    }

    /// <summary>
    /// <see cref="Carry"/> set on the session: the model first, then the effort, whose levels follow the model, each found by
    /// its category (Codex's effort id is <c>reasoning_effort</c>, Claude's <c>effort</c>). Whether anything is set, so the
    /// posture is set again after it: a model change can move the mode (<c>haiku</c> moves <c>auto</c> to
    /// <c>acceptEdits</c>).
    /// </summary>
    /// <remarks>
    /// Where <see cref="ModelRefused"/> is given, a model the open's list does not offer, a set the agent refuses, or one answered with another
    /// value it offers is a refusal of that choice on this account: the re-ask answers a later choice to set on this session, each
    /// category the refused attempt touched and the later choice leaves at account default first set back to its opening value; or the
    /// open ends before its prompt. An effort not offered runs at account default effort, said as not carried, never a refusal.
    /// </remarks>
    /// <exception cref="AcpChoiceNotOffered">The choice is refused and nothing else may run on this session: no prompt is sent.</exception>
    public async Task<bool> CarryAsync(CancellationToken ct)
    {
        var asked = Carry;
        var set = false;
        var touched = new HashSet<string>(StringComparer.Ordinal);
        while (asked is not null)
        {
            ModelRefusal? refusal = null;
            var model = await OneAsync("model", asked.Model, judged: ModelRefused is not null).ConfigureAwait(false);
            if (refusal is { } refused)
            {
                NotOffered = refused;
                var later = await ModelRefused!.ReaskAsync(refused).ConfigureAwait(false);
                if (later is null) throw new AcpChoiceNotOffered(refused);

                // The same agent on the same account: what the refused attempt moved is set back where the later
                // choice leaves it at account default, then the later choice is carried on this same session, no respawn.
                foreach (var category in touched)
                {
                    var leaves = category == "model" ? later.Model : later.Effort;
                    if (leaves.Source is "entry" or "override") continue;
                    if (OpeningOptions.FirstOrDefault(option => option.Category == category) is not { Current: { } opening } option) continue;
                    try
                    {
                        await SetAsync(option.Id, opening, ct).ConfigureAwait(false);
                    }
                    catch (DriverException back)
                    {
                        say($"[{category} could not be set back to `{opening}`: {back.Message}]");
                        throw new AcpChoiceNotOffered(refused);
                    }

                    if (LastAcknowledged?.FirstOrDefault(each => each.Category == category) is { } now && now.Current != opening)
                    {
                        throw new AcpChoiceNotOffered(refused);
                    }
                }

                NotOffered = null;
                touched.Clear();
                asked = later;
                continue;
            }

            var effort = await OneAsync("thought_level", asked.Effort, judged: false).ConfigureAwait(false);
            if (model != asked.Model || effort != asked.Effort) Carried?.Invoke(asked with { Model = model, Effort = effort });
            return set;

            async Task<ActorValue> OneAsync(string category, ActorValue value, bool judged)
            {
                if (value is not { Source: "entry" or "override", Value: { } wanted, Carried: true }) return value;
                if (Current.FirstOrDefault(option => option.Category == category) is not { } option) return value with { Carried = false };
                if (option.Current == wanted) return value;
                // Always asked, never judged by the list alone: Claude's door lists aliases and takes a full id it does not list,
                // and refuses a model it does not offer at the set, before any prompt. A refusal of a value the list named is `refused`; of
                // one it did not, `not-offered`.
                var listed = option.Choices.Any(choice => choice.Value == wanted);
                try
                {
                    await SetAsync(option.Id, wanted, ct).ConfigureAwait(false);
                    // What the attempt moved, which a same-account move sets back where it leaves the category at account default.
                    touched.Add(category);
                    set = true;
                }
                // Only the agent's own refusal: a stream that ended with the set pending refused nothing, and ends the open.
                catch (AcpRefusal refused)
                {
                    say($"[{category} `{wanted}` was not set: {refused.Message}]");
                    if (judged) refusal = new ModelRefusal(category, wanted, listed ? AgentOffers.RefusedCode : AgentOffers.NotOffered);
                    return value with { Carried = false };
                }

                // A returned value other than the one asked is a refusal only where it is another value the list offers: an asked id the agent
                // answers with its own alias is taken (`claude-sonnet-5-5` asked, `sonnet` returned).
                var now = LastAcknowledged?.FirstOrDefault(each => each.Category == category);
                if (now is not null && now.Current != wanted && now.Choices.Any(choice => choice.Value == wanted))
                {
                    if (judged) refusal = new ModelRefusal(category, wanted, AgentOffers.RefusedCode);
                    return value with { Carried = false };
                }

                return value;
            }
        }

        return set;
    }
}
