using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The presses that waited when Daoris last stopped, brought back (CONFIRMWAIT1a-ii, the confirm-wait design §7.2): the pipe's
/// <see cref="InstancePipe.Restore"/>. It reaches the pipe's state (<see cref="PipeState"/>) under its gate and the waiting records
/// (<see cref="PipeWaitingKeep"/>) under theirs, never both at once.
/// </summary>
internal sealed class PipeRestore(InstancePipe pipe)
{
    private readonly PipeState _state = pipe.State;
    private readonly PipeWaitingKeep _keep = pipe.Keep;
    private readonly MachineLog _log = pipe.Log;
    private readonly PipeCensus _census = pipe.Census;
    private readonly PipeLimits _limits = pipe.Limits;
    private int _restored;

    private PipeConfirmations Confirmations => pipe.Confirmations;

    /// <summary>Once, before the pipe serves, on the home's lease, which makes this instance the one writer of its records.</summary>
    public void Run()
    {
        if (Interlocked.Exchange(ref _restored, 1) == 1) return;
        lock (_state.Gate)
        {
            _state.Journal ??= new PipeJournal(DaorisHome.Entry(pipe.Home, HomeEntry.Pipe), _limits.Kept);
        }

        int waiting = 0, lost = 0, newer = 0;
        var files = _keep.Read().OrderBy(each => each.Record?.Asked ?? DateTimeOffset.MaxValue).ThenBy(each => each.Name, StringComparer.Ordinal).ToList();
        // CONFIRMWAIT1 (the confirm-wait design §15.2 item 7): each terminal id is reserved across the whole set before any card is told.
        // Records of one id that are the same press collapse into the first; records that differ are each lost.
        var byRequest = files
            .Where(each => each.Record is { State: PipeWaitingRecord.Waiting } record && record.Confirmation == each.Name && PipeWaiting.IsName(each.Name))
            .GroupBy(each => each.Record!.Request, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(each => each.Record!).ToList(), StringComparer.Ordinal);
        foreach (var file in files)
        {
            if (!PipeWaiting.IsName(file.Name))
            {
                PipeWaiting.Delete(file.Path);
                _log.Warn("pipe.restore", ("state", "misnamed"));
                continue;
            }

            if (file.Newer)
            {
                newer++;
                _log.Warn("pipe.restore", ("state", "newer"));
                continue;
            }

            if (file.Record is not { } record || record.Confirmation != file.Name)
            {
                lost++;
                PipeWaiting.Delete(file.Path);
                _log.Warn("pipe.restore", ("state", "unread"));
                continue;
            }

            switch (record.State)
            {
                case PipeWaitingRecord.Begun:
                    Journaled(record, IpcJson.Serialize(IpcResponse.CreateError(record.Request, InstancePipe.Interrupted, null,
                        "Daoris stopped while this request was under way, so whether it was done is not known; it was not run again.")));
                    _keep.Remove(record.Confirmation);
                    continue;
                case PipeWaitingRecord.Ended when record.Answer is { } answer:
                    Journaled(record, answer);
                    _keep.Remove(record.Confirmation);
                    continue;
            }

            var twins = byRequest.GetValueOrDefault(record.Request) ?? [];
            if (twins.Count > 1 && twins.All(each => Same(each, twins[0])) && !ReferenceEquals(record, twins[0]))
            {
                // The same press kept twice: the first stands for it, and this file goes.
                _keep.Remove(record.Confirmation);
                _log.Warn("pipe.restore", ("state", "duplicate"));
                continue;
            }

            var alone = twins.Count <= 1 || twins.All(each => Same(each, twins[0]));
            if (record.State == PipeWaitingRecord.Waiting && !record.Keyed && alone && Rejudged(record)) waiting++;
            else
            {
                lost++;
                Journaled(record, IpcJson.Serialize(IpcResponse.CreateError(record.Request, InstancePipe.Lost, null, record.Keyed
                    ? "Daoris restarted while this waited, and it carries a key, so it does not wait across a restart; nothing was done. Send it again."
                    : "Daoris restarted while this waited and could not bring it back, so it was not done. Send it again.")));
                _keep.Remove(record.Confirmation);
            }
        }

        _log.Info("pipe.restored", ("waiting", waiting), ("lost", lost), ("newer", newer));
        Confirmations.Restore(waiting, lost, newer);
    }

    /// <summary>
    /// A waiting record judged again as <see cref="PipeAdmission.Accept"/> judges a press (§7.2 step 6), and waiting again where it passes:
    /// its card made from its payload, its grounds the record's, no terminal on its line. False, and nothing asked, where it fails.
    /// </summary>
    private bool Rejudged(PipeWaitingRecord record)
    {
        if (record.Request is not { Length: > 0 and <= 128 } id || record.Payload is not { } text) return false;
        JsonElement? payload;
        try
        {
            payload = text.Length == 0 ? null : JsonDocument.Parse(text).RootElement.Clone();
        }
        catch (JsonException)
        {
            return false;
        }

        if (_census.Of(record.Module, record.Route, payload) != PipeClass.Person
            || record.Binding.Request != $"{record.Module}.{record.Route}"
            || record.Binding.Payload != PipeBinding.Fingerprint(text)
            || PipeConfirmations.FieldsOf(payload) is not ({ } fields, null)
            || _census.Targets.Row(record.Module, record.Route, payload) is not { } row)
        {
            return false;
        }

        // CONFIRMWAIT1 (§15.2 item 7): its grounds are what its row reads for its payload, by kind and subject, none missing and none
        // added, so a baseline that lacks a ground protects nothing it omitted.
        if (!row.Expected(payload).Select(each => $"{each.Kind}|{each.Subject}").Order(StringComparer.Ordinal)
                .SequenceEqual(record.Grounds.Select(each => $"{each.Kind}|{each.Subject}").Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            return false;
        }

        lock (_state.Gate)
        {
            if (_state.Running.ContainsKey(id) || _state.Journal!.Find(id) is not null) return false;
        }

        if (Confirmations.Hold() is not { } place) return false;
        var request = new IpcRequest { Id = id, Module = record.Module, Type = record.Route, Payload = payload };
        var line = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _keep.Hold(record);

        var asked = record.Grounds;
        var shown = Confirmations.Ask(
            place, record.Module, record.Route, fields, record.Asker,
            judge: row.Grounds.Count == 0 ? null : ct => pipe.Asking.JudgeAsync(row, payload, asked, ct), checkWithin: row.Grounds.Count == 0 ? null : row.Bound,
            @as: new PipeAsAsked(record.Confirmation, record.Asked, record.ShownAt, Restored: true, WaitedOn: false));
        lock (_state.Gate)
        {
            _state.Running[id] = (record.Binding, line.Task);
            _state.WaitingFor[id] = shown.Shown.Id;
            _state.Lines[id] = line;
        }

        _ = pipe.Asking.ConfirmThenRunAsync(request, id, record.Binding, line, shown.Ended, record.Confirmation);
        return true;
    }

    /// <summary>Whether two records of one terminal id keep the same press: its binding, route, payload, key and grounds alike.</summary>
    private static bool Same(PipeWaitingRecord one, PipeWaitingRecord other) =>
        one.Binding == other.Binding && one.Module == other.Module && one.Route == other.Route && one.Payload == other.Payload
        && one.Keyed == other.Keyed && one.Grounds.SequenceEqual(other.Grounds);

    /// <summary>
    /// <paramref name="record"/>'s ending journaled as <paramref name="answer"/> where its terminal's id reads and the journal holds no line
    /// for it (§7.2), so <c>RESULT</c> and the same id sent again say it.
    /// </summary>
    private void Journaled(PipeWaitingRecord record, string answer)
    {
        if (record.Request is not { Length: > 0 and <= 128 } id) return;
        lock (_state.Gate)
        {
            if (_state.Running.ContainsKey(id) || _state.Journal!.Find(id) is not null) return;
        }

        if (_state.Journal!.Begin(id, record.Binding)) _state.Journal.End(id, record.Binding, answer);
    }
}
