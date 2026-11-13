import { readFileSync } from 'node:fs';
import { missing } from './records.mjs';

export const BUDGET_FILE = 'tools/dev-metrics/budgets.json';
export const budgets = JSON.parse(readFileSync(new URL('./budgets.json', import.meta.url), 'utf8'));

export function judge(report, logs, mergeRecords) {
  const checks = [];
  const check = (metric, value, budget, unit, identity = {}, minimum = false) => {
    checks.push({ metric, ...identity, value, budget, unit, verdict: typeof value !== 'number' ? 'not recorded' : (minimum ? value < budget : value > budget) ? 'defect' : 'pass' });
  };
  const looks = logs.filter(e => e.event === 'watch.looked').map(e => Date.parse(e.time)).sort((a, b) => a - b);
  const lookCount = (from, to) => {
    const a = Date.parse(from), b = Date.parse(to);
    if (!Number.isFinite(a) || !Number.isFinite(b) || b < a || looks[0] > a || looks.at(-1) < b || !looks.length) return missing('watch.looked log covering both endpoints to count completed looks');
    return looks.filter(t => t > a && t <= b).length;
  };
  for (const key of ['medianMs', 'p90Ms', 'maxMs']) check(`look.${key}`, report.friction.lookMs?.[key.replace('Ms', '')] ?? missing('watch.looked data.ms'), budgets.look[key], 'ms', { scope: 'global loop' });
  for (const q of report.lifecycle) {
    check('quest.publishedToTaken', lookCount(q.stages.published, q.stages.taken), budgets.quest.publishedToTakenLooks, 'looks', { quest: q.quest });
    check('quest.takenToStarted', q.elapsedMs.takenToStarted, budgets.quest.takenToStartedMs, 'ms', { quest: q.quest });
    check('quest.acceptedToLanded', lookCount(q.stages.accepted, q.stages.landed), budgets.quest.acceptedToLandedLooks, 'looks', { quest: q.quest });
  }
  for (const run of report.workflow) for (const step of run.steps) {
    if (['held', 'unknown'].includes(step.state)) check('workflow.unnamedWaiter', step.waiter?.startsWith('not recorded:') ? missing('workflow hold waiter identity; an unknown value cannot establish a violation') : 0, budgets.workflow.unnamedWaiters, 'items', { run: run.run, node: step.node });
    if (!['review', 'go-ahead', 'opinion', 'claimed'].includes(step.kind)) check('workflow.mechanicalWork', lookCount(step.startedAt, step.endedAt), budgets.workflow.mechanicalWorkLooks, 'looks', { run: run.run, node: step.node });
  }
  for (const [kind, rows] of Object.entries(report.communication).filter(([, v]) => Array.isArray(v))) for (const row of rows) {
    check(`communication.${kind}`, lookCount(row.postedAt ?? row.askedAt ?? row.saidAt ?? row.respondedAt, row.readAt ?? row.deliveredAt), budgets.communication.deliveryLooks, 'looks', { id: row.message ?? row.helperAsk ?? row.word ?? row.quest });
  }
  check('communication.lostWords', report.communication.lostWords, budgets.communication.lostWords, 'items');
  for (const item of report.personQueue.items) check('personQueue.unseenAge', item.unseen === true ? item.ageMs : missing('needs-you item seen acknowledgement; age alone does not establish unseen'), budgets.personQueue.unseenAgeMs, 'ms', { id: item.session ?? item.quest ?? item.run ?? item.ask });
  for (const row of report.actors) check('actors.untypedFailures', row.untypedFailures, budgets.actors.untypedFailures, 'items', { role: row.role });
  check('actors.silentFailures', missing('failure/outcome ledger proving a session failed silently'), budgets.actors.silentFailures, 'items');
  for (const row of report.askDaoris.replies) check('askDaoris.firstReplyMs', row.firstReplyMs, budgets.askDaoris.firstReplyMs, 'ms', { session: row.session });
  for (const row of mergeRecords) {
    check('development.mergeWallMs', row.wallMs ?? missing('merge-record wallMs'), budgets.development.mergeWallMs, 'ms', { branch: row.branch });
    check('development.gatesOverBudget', row.overBudget?.gates?.length ?? missing('merge-record overBudget.gates'), budgets.development.gatesOverBudget, 'gates', { branch: row.branch });
  }
  const branches = [...new Set(mergeRecords.map(r => r.branch))];
  check('development.firstPassPercent', branches.length ? branches.filter(b => mergeRecords.some(r => r.branch === b && r.firstPass)).length / branches.length * 100 : missing('merge-record firstPass per branch'), budgets.development.firstPassPercent, '%', {}, true);
  check('development.fullSetWallMs', missing('full-set gate run wall-time receipt'), budgets.development.fullSetWallMs, 'ms');
  check('browser.openMs', report.browser.openMs, budgets.browser.openMs, 'ms');
  return { checks, defects: checks.filter(c => c.verdict === 'defect') };
}
