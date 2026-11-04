using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What the agent says of itself as its turns go: the largest context the session held, its account's windows, the models its
/// turns spent tokens on, and its config options where it changed them itself.
/// </summary>
/// <remarks>
/// <para>🔴 <b>The high-water mark, not the last reading.</b> Context drops when a session compacts, so the final number would report
/// a session that nearly filled its window as having used very little, which is exactly backwards for the person deciding whether
/// to split work.</para>
///
/// <para><b>A frame that is not a measurement is ignored, never believed.</b> Somebody else's protocol version is free to change its
/// shape, and a malformed update must become an absence rather than a zero on a person's screen, nor may it take the turn down.</para>
/// </remarks>
[TurnStage("measure", TurnPhase.Update)]
internal sealed class AcpMeasure(ITurnWiring wiring) : ITurnStage
{
    // The usage is read off the reader's thread, by the session's own readers and its outcome; one lock guards it and the models.
    private readonly object _gate = new();
    private AcpUsage? _usage;

    // The models the turn's own updates named, and the response's split as this process last said it.
    private readonly List<string?> _named = [];
    private IReadOnlyDictionary<string, TurnTokens>? _split;

    /// <summary>The largest context reading the session reported, or null where it reported none.</summary>
    public AcpUsage? Usage
    {
        get { lock (_gate) return _usage; }
    }

    public ValueTask<bool> RunAsync(TurnFrame frame)
    {
        var update = frame.Update;
        Measure(update);

        // A model it fell back to, a switch it made: what it says now replaces what was kept, as an answer to a change does.
        if (AcpRender.Kind(update) == "config_option_update" && update.TryGetProperty("configOptions", out var changed)
            && changed.ValueKind == JsonValueKind.Array)
        {
            wiring.KeepOptions(AcpConfigOption.Read(changed));
        }

        return ValueTask.FromResult(false);
    }

    private void Measure(JsonElement update)
    {
        if (AcpRender.Kind(update) != "usage_update") return;

        // The account's windows ride beside the context, whether or not the context reads: an object, or nothing.
        if (update.TryGetProperty("_meta", out var meta) && meta.ValueKind == JsonValueKind.Object
            && meta.TryGetProperty("_claude/rateLimit", out var limits) && limits.ValueKind == JsonValueKind.Object)
        {
            try
            {
                wiring.SayLimits(limits.Clone());
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                wiring.Say($"[what the session said about its account's windows could not be kept: {error.Message}]");
            }
        }

        // The model answering the turn, for the usage history alone: Claude Code's adapter says it beside the context.
        if (meta.ValueKind == JsonValueKind.Object && Json.Text(meta, "_claude/model") is { Length: > 0 } model)
        {
            lock (_gate) _named.Add(model);
        }

        if (Json.Long(update, "used") is not { } used || Json.Long(update, "size") is not { } size) return;

        lock (_gate)
        {
            if (_usage is null || used > _usage.Used) _usage = new AcpUsage(used, size);
        }
    }

    /// <summary>
    /// Which models a turn's tokens were spent on, for the usage history alone: the models the turn's updates named, and the response's
    /// <c>_meta.quota</c> by model where it agrees with the turn's <c>usage</c>. A small model the harness called on its own is counted
    /// in the quota and not in <c>usage</c>, so its tokens are kept beside the total. The next turn's names are its own.
    /// </summary>
    public TurnModels ModelsOf(JsonElement result, TurnTokens? tokens)
    {
        JsonElement raw = default;
        var split = result.ValueKind == JsonValueKind.Object
                    && result.TryGetProperty("_meta", out var meta) && meta.ValueKind == JsonValueKind.Object
                    && meta.TryGetProperty("quota", out var quota) && quota.ValueKind == JsonValueKind.Object
                    && quota.TryGetProperty("model_usage", out var byModel)
            ? TurnModels.Read(raw = byModel)
            : null;
        lock (_gate)
        {
            var models = TurnModels.Of(tokens, _named, TurnModels.FromUpdates, split, _split, TurnModels.FromQuota, TurnModels.Names(raw));
            _named.Clear();
            if (split is { Count: > 0 }) _split = split;
            return models;
        }
    }

    /// <summary>An error answered the prompt, and the turn its updates named ends without a response: its names are not the next's.</summary>
    public void Forget()
    {
        lock (_gate) _named.Clear();
    }
}
