import type { CommandArgs } from './types.ts';
import type { ExitCode } from './errors.ts';
import { existsSync } from 'node:fs';
import { basename, join, resolve } from 'node:path';
import { readText, sha256, writeTextAtomic } from './fsx.ts';
import { DaorisError } from './errors.ts';

/**
 * A **quest** is work one repository posts for another to take.
 *
 * Named a quest rather than a task or a request because every repository here already has a backlog
 * full of tasks, and a word that collided with those would be ambiguous in exactly the file where the
 * distinction matters. It also carries the right semantics on its own: a quest is *taken*, not
 * assigned — which is the property that keeps declining a real answer.
 *
 * The lifecycle is deliberately small. Anything finer would be status for its own sake, and the
 * receiving repository already has a backlog for the details.
 */
export const STATUSES: readonly QuestStatus[] = ['open', 'taken', 'done', 'declined'];

/** The heading quests land under, fixed so both a person and the index can find them. */
export const QUESTS_HEADING = '## Quests from other repositories';

const BACKLOG = 'TASKS.md';

/**
 * A short, stable handle.
 *
 * Derived from who asked, what for, and when — so the same quest posted twice collides rather than
 * multiplying, and so the id can be quoted in a commit message or another repository's notes without
 * anyone having to look it up.
 */
export function questId({ from, title, date }: { from: string; title: string; date: string }): string {
  return sha256(`${from} ${title.trim()} ${date}`).slice(0, 6);
}

/**
 * One quest, as a checklist item.
 *
 * The **checkbox is the coarse state** every backlog in this family already reads: unticked means
 * outstanding. The italic line carries the rest. That split is what lets a repository which knows
 * nothing about Daoris still handle one correctly — it looks like an ordinary task, because it is one.
 */
export interface QuestFields {
  from: string;
  title: string;
  body: string;
  date: string;
  status?: QuestStatus;
  note?: string | null;
  id?: string | null;
}

/** Where a quest is in its life. Four states, because anything finer is status for its own sake. */
export type QuestStatus = 'open' | 'taken' | 'done' | 'declined';

/** One quest as it appears in a backlog, parsed back out of it. */
export interface Quest {
  id: string | null;
  title: string;
  from: string | null;
  status: QuestStatus;
  text: string;
}

export function renderQuest(
  { from, title, body, date, status = 'open', note = null, id = null }: QuestFields,
): string {
  const handle = id ?? questId({ from, title, date });
  const detail = body.trim().split('\n').map((l) => (l.trim() ? `  ${l.trim()}` : '')).join('\n');
  const box = status === 'done' || status === 'declined' ? 'x' : ' ';
  const tail = note ? ` — ${note.trim()}` : '';

  return `- [${box}] **${title.trim()}** \`#${handle}\`\n${detail}\n\n`
       + `  _Quest from \`${from}\` · filed ${date} · **${status}**${tail}._\n`;
}

/** Every quest in a backlog, parsed back out of it. */
export function readQuests(text: string): Quest[] {
  const at = text.indexOf(QUESTS_HEADING);
  if (at < 0) return [];

  const after = at + QUESTS_HEADING.length;
  const next = text.indexOf('\n## ', after);
  const section = next < 0 ? text.slice(after) : text.slice(after, next);

  const quests: Quest[] = [];
  let current: string[] | null = null;
  for (const line of section.split('\n')) {
    if (line.startsWith('- [')) {
      if (current) quests.push(finish(current));
      current = [line];
    } else if (current) {
      current.push(line);
    }
  }
  if (current) quests.push(finish(current));
  return quests;

  function finish(lines: string[]): Quest {
    const body = lines.join('\n').trimEnd();
    return {
      id: body.match(/`#([0-9a-f]{6})`/)?.[1] ?? null,
      title: body.match(/\*\*(.+?)\*\*/)?.[1] ?? '',
      from: body.match(/Quest from `([^`]+)`/)?.[1] ?? null,
      status: (body.match(/·\s\*\*(\w+)\*\*/)?.[1] ?? 'open') as QuestStatus,
      text: body,
    };
  }
}

/**
 * Post a quest into another repository's backlog.
 *
 * @remarks
 * **It appends; it never restructures.** Every backlog here is shaped differently — some group by
 * theme, one says in its own header to add a line anywhere. A tool that tried to file in the *right*
 * section would need to understand each of them, and would be wrong in someone's repository the day
 * they reorganised.
 */
export function planPost(
  { targetRoot, from, title, body, date }:
  { targetRoot: string; from: string; title: string; body: string; date: string },
): { backlog: string; id: string; content: string } {
  const backlog = join(targetRoot, BACKLOG);
  if (!existsSync(backlog)) {
    throw new DaorisError(
      `'${targetRoot}' has no ${BACKLOG} — a quest goes in the receiving repository's backlog, and this `
      + 'one has none. Create it there, or raise it however that repository actually tracks work.');
  }

  const current = readText(backlog);
  const id = questId({ from, title, date });
  if (readQuests(current).some((quest) => quest.id === id)) {
    throw new DaorisError(`that quest is already posted there as #${id} — nothing written.`);
  }

  const entry = renderQuest({ from, title, body, date, id });

  if (current.includes(QUESTS_HEADING)) {
    const at = current.indexOf(QUESTS_HEADING) + QUESTS_HEADING.length;
    const next = current.indexOf('\n## ', at);
    const end = next === -1 ? current.length : next;
    const section = `${current.slice(at, end).replace(/\s+$/, '')}\n\n${entry}`;
    return { backlog, id, content: `${current.slice(0, at)}${section}\n${current.slice(end)}` };
  }

  const preamble =
    `\n${QUESTS_HEADING}\n\n`
    + '_Posted by other repositories in this family, which do not edit this one. Take one with\n'
    + '`daoris quest take <id>`, finish it with `done`, or turn it down with `decline` — declining is a\n'
    + 'real answer, and the reason is what the asker can actually act on._\n\n';

  return { backlog, id, content: `${current.replace(/\s+$/, '')}\n${preamble}${entry}` };
}

/** Move a quest already in THIS repository's backlog to a new status. */
export function planStatus(
  { root, id, status, note, date }:
  { root: string; id: string; status: QuestStatus; note: string | null; date: string },
): { backlog: string; content: string; quest: Quest } {
  if (!STATUSES.includes(status)) {
    throw new DaorisError(`unknown status '${status}' — one of: ${STATUSES.join(', ')}`);
  }

  const backlog = join(root, BACKLOG);
  if (!existsSync(backlog)) throw new DaorisError(`no ${BACKLOG} here — this repository holds no quests.`);

  const current = readText(backlog);
  const quests = readQuests(current);
  const quest = quests.find((candidate) => candidate.id === id);
  if (!quest) {
    const held = quests.map((candidate) => `#${candidate.id}`).join(', ') || 'none';
    throw new DaorisError(`no quest #${id} in ${BACKLOG} — this repository holds: ${held}`);
  }

  const box = status === 'done' || status === 'declined' ? 'x' : ' ';
  const tail = note ? ` — ${note.trim()}` : '';
  const updated = quest.text
    .replace(/^- \[.\]/, `- [${box}]`)
    .replace(/·\s\*\*\w+\*\*.*?\._$/s, `· **${status}**${tail} · ${date}._`);

  return { backlog, content: current.replace(quest.text, updated), quest };
}

function parseArgs(
  argv: readonly string[], valued: readonly string[] = ['title', 'body', 'reason'],
): { flags: Record<string, string | true | null>; positional: string[] } {
  const flags: Record<string, string | true | null> = {};
  const positional: string[] = [];
  for (let at = 0; at < argv.length; at++) {
    const token = argv[at]!;
    if (!token.startsWith('--')) { positional.push(token); continue; }
    const name = token.slice(2);
    if (valued.includes(name)) flags[name] = argv[++at] ?? null;
    else flags[name] = true;
  }
  return { flags, positional };
}

const USAGE = `usage:
  daoris quest post <repo> --title '<one line>' --body '<why>'   post a quest to another repo
  daoris quest take <id>                                         accept one posted here
  daoris quest done <id> [--reason '<what changed>']             finish it
  daoris quest decline <id> --reason '<why not>'                 turn it down
  daoris quest list                                              quests posted to this repo`;

export function commandQuest(
  { root, argv, write }: Pick<CommandArgs, 'root' | 'argv' | 'write'>,
): ExitCode {
  const { flags, positional } = parseArgs(argv);
  const [verb, subject] = positional;
  const today = new Date().toISOString().slice(0, 10);

  if (!verb) { write(USAGE); return 2; }

  if (verb === 'list') {
    const backlog = join(root, BACKLOG);
    const quests = existsSync(backlog) ? readQuests(readText(backlog)) : [];
    if (!quests.length) { write('daoris: no quests posted to this repository.'); return 0; }
    for (const quest of quests) {
      write(`  ${quest.status.padEnd(9)} #${quest.id}  ${quest.title}  (from ${quest.from ?? '?'})`);
    }
    return 0;
  }

  if (verb === 'post') {
    const { title, body } = flags;
    if (!subject || typeof title !== 'string' || typeof body !== 'string') {
      throw new DaorisError(USAGE);
    }

    const targetRoot = resolve(root, subject);
    if (targetRoot === resolve(root)) {
      throw new DaorisError('that is this repository — a quest is work for someone else. Use your own backlog.');
    }

    const from = basename(resolve(root));
    const plan = planPost({ targetRoot, from, title, body, date: today });

    if (flags['dry-run']) {
      write(renderQuest({ from, title, body, date: today, id: plan.id }));
      write(`daoris: would post #${plan.id} to ${plan.backlog}`);
      return 0;
    }

    writeTextAtomic(plan.backlog, plan.content);
    write(`daoris: posted quest #${plan.id} to ${plan.backlog}`);
    // Said every time, because the tempting next step is to go and do the work yourself.
    write('  It is uncommitted there, for that repository to review. Do not make the change from here.');
    return 0;
  }

  if (['take', 'done', 'decline'].includes(verb)) {
    if (!subject) throw new DaorisError(USAGE);
    if (verb === 'decline' && typeof flags.reason !== 'string') {
      throw new DaorisError('declining needs --reason: the reason is the part the asker can act on.');
    }

    const status: QuestStatus = verb === 'take' ? 'taken' : verb === 'done' ? 'done' : 'declined';
    const plan = planStatus({
      root,
      id: subject.replace(/^#/, ''),
      status,
      note: typeof flags.reason === 'string' ? flags.reason : null,
      date: today,
    });

    if (flags['dry-run']) { write(`daoris: would mark #${plan.quest.id} ${status}`); return 0; }

    writeTextAtomic(plan.backlog, plan.content);
    write(`daoris: quest #${plan.quest.id} is now ${status}`);
    return 0;
  }

  throw new DaorisError(`unknown quest verb '${verb}'\n${USAGE}`);
}
