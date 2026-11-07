import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, CheckField, Prose } from '../../design/ui';
import type { GitRead } from './RepositoryGit';

/** One line of a hunk as the driver read it: `' '` context, `'+'` added, `'-'` removed, `'\\'` git's no-newline marker. */
export type HunkLineRead = { kind: string; text: string };

/** What a hunk list shows and what a press sends back (the bridge's `GitFileHunks`, handed as it came). */
export type HunksValue = {
  head: string | null;
  index: string;
  source: string;
  binary: boolean;
  hunks: readonly { id: string; header: string; lines: readonly HunkLineRead[] }[];
};

export type HunkPress = { picks: { id: string; lines?: number[] }[]; faces: { head: string | null; index: string; source: string } };

/**
 * **A file's hunks in the Changes tab**: each hunk as git prints it, a hunk pressed whole or
 * the lines chosen in it (`Stage` on the index → worktree side, `Unstage` on HEAD → index). What it sends back is the ids it reads
 * with, so the driver refuses a hunk whose file moved since. A binary file has no hunk: it is pressed whole above.
 *
 * @remarks **A molecule**: the read arrives as a prop; `busy` holds every press while an act runs.
 */
export function ChangeHunks({ read, verb, busy, onPress }: {
  read: GitRead<HunksValue>;
  verb: 'stage' | 'unstage';
  busy: boolean;
  onPress: (press: HunkPress) => void;
}) {
  const { t } = useTranslation();
  if (read.state === 'reading') return <Prose className="text-small text-ink-soft">{t('projects.git.changes.hunks.reading')}</Prose>;
  if (read.state === 'refused') return <Prose className="text-small text-ink-soft">{read.sentence}</Prose>;
  if (read.state === 'noCheckout') return null;
  const { binary, hunks, head, index, source } = read.value;
  if (binary) return <Prose className="text-small text-ink-soft">{t('projects.git.changes.hunks.binary')}</Prose>;
  if (hunks.length === 0) return <Prose className="text-small text-ink-soft">{t('projects.git.changes.hunks.none')}</Prose>;
  return (
    <div className="grid min-w-0 gap-2">
      {hunks.map((hunk) => (
        <HunkBlock
          key={hunk.id}
          hunk={hunk}
          verb={verb}
          busy={busy}
          onPress={(lines) => onPress({ picks: [{ id: hunk.id, ...(lines ? { lines } : {}) }], faces: { head, index, source } })}
        />
      ))}
    </div>
  );
}

function HunkBlock({ hunk, verb, busy, onPress }: {
  hunk: { id: string; header: string; lines: readonly HunkLineRead[] };
  verb: 'stage' | 'unstage';
  busy: boolean;
  onPress: (lines?: number[]) => void;
}) {
  const { t } = useTranslation();
  const [chosen, setChosen] = useState<ReadonlySet<number>>(new Set());
  const toggle = (at: number, on: boolean) => setChosen((now) => {
    const next = new Set(now);
    if (on) next.add(at); else next.delete(at);
    return next;
  });
  return (
    <div className="grid min-w-0 gap-1 rounded-control border border-line p-2" data-hunk={hunk.id}>
      <code className="min-w-0 font-mono text-meta text-ink-faint [overflow-wrap:anywhere]">{hunk.header}</code>
      <ul className="m-0 grid min-w-0 list-none p-0">
        {hunk.lines.map((line, at) => {
          const change = line.kind === '+' || line.kind === '-';
          return (
            <li key={at} className="flex min-w-0 items-start gap-2">
              {change
                ? <CheckField checked={chosen.has(at)} onChange={(on) => toggle(at, on)} disabled={busy} hideLabel label={t('projects.git.changes.line.choose', { text: line.text })} />
                : <span aria-hidden className="size-4 shrink-0" />}
              <code
                className={`min-w-0 whitespace-pre-wrap font-mono text-small [overflow-wrap:anywhere] ${line.kind === '+' ? 'text-ink-done' : line.kind === '-' ? 'text-ink-danger' : 'text-ink-soft'}`}
              >
                {line.kind}{line.text}
              </code>
            </li>
          );
        })}
      </ul>
      <div className="flex flex-wrap gap-2">
        <Button disabled={busy} onClick={() => onPress()}>{t(`projects.git.changes.hunk.${verb}`)}</Button>
        <Button disabled={busy || chosen.size === 0} onClick={() => onPress([...chosen].sort((a, b) => a - b))}>
          {t(`projects.git.changes.lines.${verb}`, { count: chosen.size })}
        </Button>
      </div>
    </div>
  );
}
