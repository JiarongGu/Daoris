const PERSON_STAGES = new Set(['accepted', 'landed']);

/** One verdict per expectation: pass, fail, or `person` where the case stopped at a word only a person gives. */
export function verdicts(caseDef, log) {
  const seen = new Map(log.stages.map(s => [s.stage, s]));
  return caseDef.expect.map(name => {
    const hit = seen.get(name);
    if (hit) {
      const departed = name === 'requirement-answered' && log.departed?.length;
      return departed
        ? { name, verdict: 'fail', afterFiledMs: hit.afterFiledMs, detail: `the done departed from a requirement: ${log.departed.join('; ')}` }
        : { name, verdict: 'pass', afterFiledMs: hit.afterFiledMs };
    }
    if (log.outcome === 'waiting-on-person' && PERSON_STAGES.has(name)) return { name, verdict: 'person', detail: 'waits on the person\'s Accept; run with --with-word where the person\'s word is given' };
    const refused = ['could-not-create', 'set-up-declined', 'set-up-timeout'].includes(log.outcome) ? log.creation : log.outcome === 'could-not-file' ? log.filing?.said : null;
    return { name, verdict: 'fail', detail: refused ? `not reached (${log.outcome}): ${refused}` : `not reached (${log.outcome})` };
  });
}

/** The receipt: the case, each stage's time, each expectation's verdict, every intervention, and the overall result. */
export function receiptOf({ caseDef, repository, workspace, service, log, withWord }) {
  const expectations = verdicts(caseDef, log);
  const failed = expectations.some(e => e.verdict === 'fail');
  const waiting = expectations.some(e => e.verdict === 'person');
  const result = failed ? 'failed' : log.interventions.length ? 'passed-with-interventions' : waiting ? 'waiting-on-person' : 'passed';
  return {
    case: caseDef.id,
    title: caseDef.title,
    result,
    outcome: log.outcome,
    filedAt: log.filedAt,
    repository,
    workspace: workspace ?? null,
    service: service ?? null,
    ask: log.ask ?? null,
    quest: log.quest ?? null,
    withWord,
    stages: log.stages,
    expectations,
    interventions: log.interventions,
    words: log.words,
    creation: log.creation ?? null,
    filing: log.filing ?? null,
    reads: log.reads,
    workflowRun: log.finalRun,
    metrics: log.ask ? `node tools/dev-metrics.mjs --home <home> --service <url> --case ${log.ask} --json` : null,
  };
}

const seconds = (ms) => (ms / 1000).toFixed(1).padStart(7) + ' s';

export function receiptText(r) {
  const lines = [`Real case ${r.case}: ${r.title}`, `result: ${r.result} (${r.outcome})`, `repository: ${r.repository}   ask: ${r.ask ?? '-'}   quest: ${r.quest ?? '-'}`, '', 'stages (from the filing, to the poll that saw them):'];
  for (const s of r.stages) lines.push(`  ${seconds(s.afterFiledMs)}  ${s.stage}`);
  lines.push('', 'expectations:');
  for (const e of r.expectations) lines.push(`  ${e.verdict.toUpperCase().padEnd(6)} ${e.name}${e.detail ? ` - ${e.detail}` : ''}`);
  lines.push('', `interventions: ${r.interventions.length || 'none'}`);
  for (const i of r.interventions) lines.push(`  ${i.act} (${i.why})`);
  if (r.words.length) { lines.push('', 'the person\'s word given:'); for (const w of r.words) lines.push(`  ${w.act}${w.detail ? ` - ${w.detail}` : ''}`); }
  if (r.metrics) lines.push('', `exact per-stage rows: ${r.metrics}`);
  return lines.join('\n') + '\n';
}
