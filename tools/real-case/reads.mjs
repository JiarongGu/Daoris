// What the installed command prints, read into facts. Each reader answers null or a neutral value where the words are not there,
// so a changed print surfaces as an expectation that cannot be judged, never as a throw.

export function parseAsk(stdout) {
  return {
    ask: /^\s*ask\s+#(\S+)/m.exec(stdout)?.[1] ?? null,
    quest: /^\s*quest\s+#(\S+)/m.exec(stdout)?.[1] ?? null,
    message: stdout.split('\n')[0]?.trim() ?? '',
  };
}

export function parseList(stdout) {
  try {
    const value = JSON.parse(stdout);
    return Array.isArray(value) ? value : null;
  } catch {
    return null;
  }
}

const lower = (v) => String(v ?? '').toLowerCase();

/** The steps of `workflow run`'s print: a marker, the step's name, two or more spaces, its state's words. */
export function parseRun(text) {
  const steps = [];
  for (const line of text.split('\n')) {
    const m = /^\s{2}(\S)\s+(.+?)\s{2,}(\S.*)$/.exec(line);
    if (m) steps.push({ marker: m[1], name: m[2].trim(), state: m[3].trim() });
  }
  const accept = /daoris workflow accept ([^\s`]+) ([^\s`]+)/.exec(text);
  return { steps, accept: accept ? { run: accept[1], node: accept[2] } : null };
}

const reached = (s) => /^reached/i.test(s.state);
const stepNamed = (run, pattern) => run.steps.find(s => pattern.test(s.name));

/**
 * Where a case stands, from one round of reads: which stages are true now. `required` is the requirement count the ask carries,
 * so a quest that lost them reads false.
 */
export function standing({ quest, sessions, run, required = 0 }) {
  const mine = sessions.filter(s => quest && s.quest === quest.id);
  const review = stepNamed(run, /look|review|accept/i);
  const landing = stepNamed(run, /landing/i);
  const answers = quest?.answers ?? [];
  const requirements = quest?.requirements ?? [];
  return {
    filed: Boolean(quest),
    taken: Boolean(quest) && (/^(taken|done)$/i.test(quest.status ?? '') || mine.length > 0),
    started: mine.some(s => !/^(queued|pending)$/i.test(s.state ?? '')),
    done: Boolean(quest) && (lower(quest.status) === 'done' || Boolean(quest.submission)),
    'review-reached': Boolean(review) && (reached(review) || /waiting|hold|needs you/i.test(review.state)),
    accepted: Boolean(review) && reached(review),
    landed: Boolean(landing) && reached(landing),
    'requirement-kept': required > 0 && requirements.length >= required,
    'requirement-answered': required > 0 && requirements.length > 0 && answers.length >= requirements.length,
    failedSession: mine.find(s => /^(failed|stopped)$/i.test(s.state ?? '')) ?? null,
    declined: lower(quest?.status) === 'declined',
    departed: answers.filter(a => a.departed).map(a => a.departed),
  };
}
