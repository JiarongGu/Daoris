import { elapsed, earliest, missing, moment } from './records.mjs';

export function workflowRows(home, scope, logs, inRange, now) {
  const scoped = scope.requested.length > 0;
  const runs = home.runs.filter(r => !scoped || scope.quests.includes(r.run) || scope.asks.includes(r.ask) || scope.sessions.includes(r.run?.replace(/^chat-/, '')));
  const result = [];
  for (const run of runs) {
    const records = home.units.filter(u => u.unit === `run:${run.run}` || Object.values(u.statuses ?? {}).some(s => (s.scope?.run ?? s.acceptance?.run) === run.run));
    const steps = [];
    for (const record of records) {
      const nodes = new Set([...Object.keys(record.statuses ?? {}), ...(record.attempts ?? []).map(a => a.node), ...(record.history ?? []).map(h => h.was?.node)]);
      for (const node of nodes) {
        const status = record.statuses?.[node] ?? {};
        const attempts = (record.attempts ?? []).filter(a => a.node === node);
        const histories = (record.history ?? []).filter(h => h.was?.node === node);
        const holds = logs.filter(e => e.event === 'workflow.held' && e.data?.node === node && (e.data.run === run.run || scope.sessions.includes(e.data.session) && scope.quests.length === 1));
        const observedHeldAt = earliest(holds.map(e => e.time));
        for (const a of attempts.length ? attempts : [null]) {
          const history = histories.find(h => h.was.attempt === a?.attempt && ['running', 'held', 'unknown'].includes(h.was.state));
          const start = a?.recordedAt ?? status.since;
          const observation = (record.observations ?? []).find(o => o.node === node && o.commit === a?.call?.subjectRevision && Date.parse(o.at) >= Date.parse(start));
          const end = a?.resolvedAt ?? observation?.at ?? (history?.was.state === 'running' ? history.at : undefined);
          const waitStart = missing('workflow node-ready/blocked transition timestamp in workflow events or node history');
          const row = {
            unit: record.unit, node, kind: a?.call?.step ?? missing('workflow graph node kind'), attempt: a?.attempt ?? status.attempt,
            state: a?.state ?? status.state, startedAt: moment(start, 'workflows/units status.since or attempt.recordedAt'),
            endedAt: moment(end, 'workflow outcome timestamp (node status keeps start only; history is bounded)'),
            waitMs: waitStart, workMs: elapsed(start, end, 'workflow attempt outcome timestamp beside recordedAt'),
            waiter: status.holds ?? (status.state === 'unknown' ? 'person' : missing('workflow node holds/waiter')),
            clearedBy: a?.resolution ? 'person' : history?.why?.startsWith('person-') ? 'person' : missing('workflow hold-clear actor in node history or attempt.resolution'),
            holdObservedAt: moment(observedHeldAt, 'workflow.held log with node and session/run'),
            ageMs: elapsed(observedHeldAt, now, 'workflow.held first observation timestamp; status.since clocks the attempt, not the hold'),
          };
          if (!inRange(start) && !inRange(end) && !inRange(history?.at) && !['held', 'unknown', 'running'].includes(status.state)) continue;
          steps.push(row);
        }
      }
    }
    if (steps.length || inRange(run.at)) result.push({ run: run.run, boundAt: moment(run.at, 'workflows/runs at'), steps, coverage: missing('complete node transition log; workflows/units history keeps cleared states only, newest 200') });
  }
  return result;
}

export function personQueue(home, sessions, quests, workflow, logs, now, scope) {
  const items = [];
  for (const s of sessions.filter(s => s.state?.toLowerCase().replace('awaitingperson', 'awaiting-person') === 'awaiting-person')) {
    items.push({ kind: 'session', session: s.id, since: moment(s.updated, 'knowledge.db sessions.updated'), ageMs: elapsed(s.updated, now, 'sessions.updated awaiting-person timestamp') });
  }
  for (const q of quests.filter(q => q.held && !q.accepted)) items.push({ kind: 'quest', quest: q.id, since: moment(q.updated, 'quests.updated held'), ageMs: elapsed(q.updated, now, 'quests.updated held') });
  for (const r of workflow) for (const s of r.steps.filter(s => ['held', 'unknown'].includes(s.state))) {
    items.push({ kind: 'workflow', run: r.run, node: s.node, waiter: s.waiter, since: s.holdObservedAt, ageMs: s.ageMs, ageBasis: 'first workflow.held observation; exact hold entry is not recorded' });
  }
  const selectedAsks = home.tables.asks.filter(a => !scope.requested.length || scope.asks.includes(a.id));
  for (const a of selectedAsks.filter(a => ['Open', 'Proposed'].includes(a.state))) {
    const intake = sessions.find(s => s.id === a.intake);
    if (intake && ['queued', 'starting', 'working', 'awaitingperson', 'awaiting-person'].includes(intake.state?.toLowerCase())) continue;
    items.push({ kind: 'proposal', ask: a.id, since: moment(a.asked, 'asks.asked'), ageMs: elapsed(a.asked, now, 'asks.asked') });
  }
  for (const item of items) {
    item.surfacedAt = missing('needs-you row presentation/read acknowledgement with item identity and timestamp');
    item.unseen = missing('needs-you row presentation/read acknowledgement; absence is not proof nobody saw it');
  }
  return { items, coverage: missing('complete needs-you snapshot (NeedsAPerson.Rows); records above cover session parks, quest holds, proposals and workflow holds'), readLog: logs.filter(e => /needs.you/.test(e.event)).length ? 'present, without per-item acknowledgement' : missing('needs-you per-item read log') };
}
