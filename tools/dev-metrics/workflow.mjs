import { elapsed, earliest, missing, moment } from './records.mjs';

export function workflowRows(home, scope, logs, inRange, now) {
  const scoped = scope.requested.length > 0;
  const runs = home.runs.filter(r => !scoped || scope.quests.includes(r.run) || scope.asks.includes(r.ask) || scope.sessions.includes(r.run?.replace(/^chat-/, '')));
  const result = [];
  for (const run of runs) {
    const graph = home.definitions.find(d => d.id === run.workflow)?.versions?.find(v => v.version === run.version)?.graph;
    const declared = graph?.nodes ?? [];
    const records = home.units.filter(u => u.unit === `run:${run.run}` || Object.values(u.statuses ?? {}).some(s => (s.scope?.run ?? s.acceptance?.run) === run.run));
    if (!records.some(r => r.unit === `run:${run.run}`)) records.push({ unit: `run:${run.run}` });
    const steps = [];
    for (const record of records) {
      const nodes = new Set([...Object.keys(record.statuses ?? {}), ...(record.attempts ?? []).map(a => a.node), ...(record.history ?? []).map(h => h.was?.node), ...(record.unit === `run:${run.run}` ? declared.map(n => n.id) : [])]);
      for (const node of nodes) {
        const status = record.statuses?.[node] ?? {};
        const declaration = declared.find(n => n.id === node);
        const kind = declaration?.kind ?? run.roles?.find(r => r.step === node)?.kind;
        const attempts = (record.attempts ?? []).filter(a => a.node === node);
        const histories = (record.history ?? []).filter(h => h.was?.node === node);
        const holds = logs.filter(e => e.event === 'workflow.held' && e.data?.node === node && (e.data.run === run.run || home.tables.sessions.some(s => s.id === e.data.session && (s.quest === run.run || s.tookQuest === run.run))));
        const observedHeldAt = earliest(holds.map(e => e.time)) ?? (status.state === 'active' && ['review', 'go-ahead'].includes(kind) ? status.since : undefined);
        for (const a of attempts.length ? attempts : [null]) {
          const current = !a || a.attempt === status.attempt;
          const history = histories.find(h => h.was.attempt === a?.attempt && ['active', 'held', 'unknown'].includes(h.was.state));
          const start = a?.recordedAt ?? status.since;
          const revision = a?.call?.subjectRevision ?? status.acceptance?.revision ?? status.binding?.revision;
          const observations = (record.observations ?? []).filter(o => o.node === node && (!revision || o.commit === revision) && ['reached', 'succeeded', 'completed', 'merged', 'released'].includes(o.state) && Date.parse(o.at) >= Date.parse(start));
          const observation = observations.length === 1 ? observations[0] : null;
          const end = a?.resolvedAt ?? observation?.at ?? (history?.was.state === 'active' ? history.at : undefined);
          const readiness = histories.filter(h => h.why === 'person-again' && Date.parse(h.at) <= Date.parse(start)).at(-1)?.at
            ?? attempts.filter(previous => previous.attempt < a?.attempt && Date.parse(previous.retryAt) <= Date.parse(start)).at(-1)?.retryAt;
          const holdHistory = histories.filter(h => ['held', 'unknown'].includes(h.was.state)).map(h => ({ attempt: h.was.attempt, reason: h.was.holds ?? missing('workflow hold reason'), waiter: missing('historical workflow hold owner; holds keeps a reason'), clearedAt: moment(h.at, 'node history at'), clearedBy: h.why?.startsWith('person-') ? 'person' : missing('hold-clear actor beside node history why'), why: h.why }));
          const row = {
            unit: record.unit, node, kind: kind ?? a?.call?.step ?? missing('workflow graph node kind'), attempt: a?.attempt ?? status.attempt ?? missing('workflow node attempt number'),
            reach: declaration?.reach ?? missing('workflow graph node reach'),
            state: current ? status.state ?? missing('workflow node status') : a.outcome ?? missing('workflow historical attempt outcome'), attemptState: a?.state ?? missing('workflow dispatched attempt state'), startedAt: moment(start, 'workflows/units status.since or attempt.recordedAt'),
            endedAt: moment(end, 'workflow outcome timestamp (node status keeps start only; history is bounded)'),
            waitMs: elapsed(readiness, start, 'workflow ready timestamp (person-again history or retry due) and attempt start'), workMs: elapsed(start, end, 'workflow attempt outcome timestamp beside recordedAt'),
            workClock: a?.resolvedAt ? 'person resolution observed at resolvedAt' : observation ? 'plugin observation at' : 'node history clearing an active attempt; otherwise missing',
            holds: holdHistory,
            waiting: Boolean(current && (status.holds || ['held', 'unknown'].includes(a?.state) || status.state === 'active' && ['review', 'go-ahead'].includes(kind) || status.state === 'failed' && !status.transient && !a?.resolution)),
            holdReason: status.holds ?? missing('workflow node holds reason'),
            waiter: a?.state === 'held' || ['held'].includes(status.state) || ['review', 'go-ahead'].includes(kind) && status.state === 'active' || status.state === 'failed' && !status.transient && !a?.resolution ? 'person'
              : a?.plugin && a.state === 'sent' ? `plugin:${a.plugin}` : missing('workflow wait owner identity; status.holds is a reason, not a person'),
            clearedBy: a?.resolution ? 'person' : history?.why?.startsWith('person-') ? 'person' : missing('workflow hold-clear actor in node history or attempt.resolution'),
            holdObservedAt: moment(observedHeldAt, 'workflow.held log with node and session/run, or active pressed review/go-ahead status.since'),
            ageMs: elapsed(observedHeldAt, now, 'workflow.held first observation timestamp; status.since clocks the attempt, not the hold'),
          };
          if (!inRange(start) && !inRange(end) && !inRange(history?.at) && !['held', 'unknown', 'active'].includes(status.state) && !(declared.some(n => n.id === node) && inRange(run.at))) continue;
          steps.push(row);
        }
      }
    }
    if (steps.length || inRange(run.at)) result.push({ run: run.run, boundAt: moment(run.at, 'workflows/runs at'), ended: records.find(r => r.unit === `run:${run.run}`)?.ended ?? missing('workflows/units run ended record'), steps, coverage: missing('complete node transition log; workflows/units history keeps cleared states only, newest 200') });
  }
  return result;
}

export function personQueue(home, sessions, quests, workflow, logs, now, scope) {
  const items = [];
  for (const s of sessions.filter(s => s.state?.toLowerCase().replace('awaitingperson', 'awaiting-person') === 'awaiting-person')) {
    items.push({ kind: 'session', session: s.id, since: moment(s.updated, 'knowledge.db sessions.updated'), ageMs: elapsed(s.updated, now, 'sessions.updated awaiting-person timestamp') });
  }
  for (const q of quests.filter(q => q.held && !q.accepted)) items.push({ kind: 'quest', quest: q.id, since: moment(q.updated, 'quests.updated held'), ageMs: elapsed(q.updated, now, 'quests.updated held') });
  for (const r of workflow) for (const s of r.steps.filter(s => s.waiting && s.waiter === 'person')) {
    items.push({ kind: 'workflow', run: r.run, node: s.node, waiter: s.waiter, since: s.holdObservedAt, ageMs: s.ageMs, ageBasis: ['review', 'go-ahead'].includes(s.kind) ? 'active pressed node status.since' : 'first workflow.held observation; exact hold entry is not recorded' });
  }
  const selectedAsks = home.tables.asks.filter(a => !scope.requested.length || scope.asks.includes(a.id));
  for (const a of selectedAsks.filter(a => ['Open', 'Proposed'].includes(a.state))) {
    const intake = sessions.find(s => s.id === a.intake);
    if (intake && ['queued', 'starting', 'working', 'awaitingperson', 'awaiting-person'].includes(intake.state?.toLowerCase())) continue;
    items.push({ kind: 'proposal', ask: a.id, since: moment(a.asked, 'asks.asked'), ageMs: elapsed(a.asked, now, 'asks.asked') });
  }
  for (const a of selectedAsks.filter(a => a.state !== 'Closed')) for (const goAhead of (a.go_aheads ?? []).filter(g => g.state === 'asked')) {
    const since = earliest((goAhead.asked ?? []).map(ask => ask.at));
    items.push({ kind: 'go-ahead', ask: a.id, number: goAhead.number, since: moment(since, 'asks.go_aheads asked[].at'), ageMs: elapsed(since, now, 'asks.go_aheads asked[].at') });
  }
  for (const h of home.helpers.filter(h => (!scope.requested.length || scope.sessions.includes(h.asker)) && h.state?.state === 'waiting')) items.push({ kind: 'helper', helperAsk: h.id, session: h.asker, since: moment(h.state.at, 'helper state waiting at'), ageMs: elapsed(h.state.at, now, 'helpers/<id>.state.json waiting at') });
  for (const item of items) {
    item.surfacedAt = missing('needs-you row presentation/read acknowledgement with item identity and timestamp');
    item.unseen = missing('needs-you row presentation/read acknowledgement; absence is not proof nobody saw it');
  }
  return { items, coverage: missing('complete needs-you snapshot (NeedsAPerson.Rows); records above cover session parks, quest holds, proposals, go-aheads, helper approvals and workflow holds'), readLog: logs.filter(e => /needs.you/.test(e.event)).length ? 'present, without per-item acknowledgement' : missing('needs-you per-item read log') };
}
