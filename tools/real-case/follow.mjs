import { parseAsk, parseList, parseRun, standing } from './reads.mjs';

/** The words of the filing: only the installed command's own flags, one ask published at once to its repository. */
export function askArguments({ caseDef, repository, workspace, workflow, through }) {
  const args = ['ask'];
  if (workspace) args.push('--workspace', workspace);
  args.push('--to', repository);
  const flow = workflow ?? caseDef.ask.workflow;
  if (flow) args.push('--workflow', flow);
  for (const [quote, check] of caseDef.ask.require ?? []) args.push('--require', quote, check);
  if (through) args.push('--through', through);
  args.push(caseDef.ask.words);
  return args;
}

/**
 * File one case's ask through the installed command and follow it by the command's reads until every expectation holds, the work
 * waits on a person's word, or the time is out. It presses nothing a person decides unless `withWord`, and makes only one act of its
 * own, a retry of a quest whose session failed; each such act is an intervention in the receipt, a defect to fix.
 */
export async function follow({ caseDef, repository, workspace, workflow, installed, withWord = false, through, clock = Date.now, sleep, pollMs = 5000, timeoutMs = 20 * 60_000, maxRetries = 1 }) {
  const t0 = clock();
  const log = { filedAt: new Date(t0).toISOString(), stages: [], words: [], interventions: [], reads: 0 };
  const seen = new Set();
  const mark = (stage, detail) => {
    if (seen.has(stage)) return;
    seen.add(stage);
    log.stages.push({ stage, at: new Date(clock()).toISOString(), afterFiledMs: clock() - t0, ...(detail ? { detail } : {}) });
  };

  const filing = installed(askArguments({ caseDef, repository, workspace, workflow, through }));
  log.filing = { code: filing.code, said: filing.stdout.trim() || filing.stderr.trim() };
  const ids = parseAsk(filing.stdout);
  if (filing.code !== 0 || !ids.ask) return { ...log, outcome: 'could-not-file', ask: ids.ask, quest: ids.quest, finalRun: '', required: 0 };
  mark('filed');

  const required = (caseDef.ask.require ?? []).length;
  let quest = ids.quest;
  let retries = 0;
  let pressed = false;
  let finalRun = '';
  let outcome = 'timeout';
  for (;;) {
    log.reads += 1;
    const quests = parseList(installed(['quest', 'list', '--repository', repository, '--json']).stdout) ?? [];
    if (!quest) quest = quests.filter(q => String(q.from ?? '').includes(ids.ask) || q.to === repository).at(-1)?.id ?? null;
    const row = quests.find(q => q.id === quest) ?? null;
    const sessions = parseList(installed(['session', 'list', '--repository', repository, '--json']).stdout) ?? [];
    const runRead = quest ? installed(['workflow', 'run', '--quest', quest]) : { stdout: '' };
    finalRun = runRead.stdout;
    const run = parseRun(runRead.stdout);
    const now = standing({ quest: row, sessions, run, required });
    for (const stage of ['taken', 'started', 'done', 'review-reached', 'accepted', 'landed', 'requirement-kept', 'requirement-answered']) {
      if (now[stage]) mark(stage);
    }
    if (now.departed.length) log.departed = now.departed;

    if (now.declined) { outcome = 'declined'; break; }
    if (now.failedSession && retries < maxRetries) {
      retries += 1;
      const done = installed(['quest', 'retry', quest]);
      log.interventions.push({ at: new Date(clock()).toISOString(), act: `daoris quest retry ${quest}`, why: `session ${now.failedSession.id} ended ${now.failedSession.state}`, code: done.code });
    }
    if (now['review-reached'] && !now.accepted && !pressed) {
      if (withWord) {
        pressed = true;
        if (run.accept) {
          const said = installed(['workflow', 'accept', run.accept.run, run.accept.node]);
          log.words.push({ at: new Date(clock()).toISOString(), act: `daoris workflow accept ${run.accept.run} ${run.accept.node}`, code: said.code });
        } else {
          log.words.push({ at: new Date(clock()).toISOString(), act: 'accept', code: null, detail: 'the run names no accept verb to press' });
        }
      } else {
        outcome = 'waiting-on-person';
        break;
      }
    }
    if (caseDef.expect.every(e => seen.has(e))) { outcome = 'complete'; break; }
    if (clock() - t0 > timeoutMs) break;
    await sleep(pollMs);
  }
  return { ...log, outcome, ask: ids.ask, quest, required, finalRun, askWords: ids.message };
}
