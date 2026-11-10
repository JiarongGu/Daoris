import type { TFunction } from 'i18next';
import type { ReactNode } from 'react';
import type { Quest, Session } from '../../data/api';
import { isHelp } from '../identity';
import type { DetailRow } from './DetailsFold';
import type { HeadLink } from './headRows';

/** The ask a session came from, as the frame holds it: its id, and its words where the list has the record. */
export type AskOrigin = { id: string; words?: string | null };

/** One place a session came from: what kind it is, what it is called, and the press that opens it where something can. */
export type Origin = { kind: 'quest' | 'ask' | 'earlier' | 'room' | 'repository'; text: string; open?: () => void };

/** What a session started with, and which of the records it is, so the page can say so plainly. */
export type Starting = { kind: 'requirements' | 'questWords' | 'askWords' | 'firstMessage'; items: string[] };

type Doors = {
  onOpenQuest?: (quest: Quest) => void;
  onOpenAsk?: (ask: string) => void;
  onOpenSession?: (session: Session) => void;
};

const firstLine = (words: string): string => words.split('\n').find((line) => line.trim()) ?? words;

/**
 * Where a session's work came from, by its kind: a driven session's quest and the ask it came from, an intake's ask, a
 * conversation's repository, Ask Daoris's room, and the earlier session a hand-over or a go-on continues. A name the
 * frame does not hold is the id; a press only where something can open it.
 */
export function originsOf(
  t: TFunction, session: Session, quest: Quest | null | undefined, ask: AskOrigin | null | undefined,
  continues: HeadLink | null | undefined, doors: Doors,
): Origin[] {
  if (isHelp(session)) return [{ kind: 'room', text: session.workspace ?? '' }];
  const found: Origin[] = [];
  if (session.from) {
    const earlier = continues && continues.session.id === session.from ? continues : null;
    found.push({
      kind: 'earlier',
      text: earlier?.title ?? session.from,
      open: earlier && doors.onOpenSession ? () => doors.onOpenSession!(earlier.session) : undefined,
    });
  }
  if (session.quest) {
    found.push({
      kind: 'quest',
      text: quest?.title ?? `#${session.quest}`,
      open: quest && doors.onOpenQuest ? () => doors.onOpenQuest!(quest) : undefined,
    });
  }
  if (ask) {
    found.push({
      kind: 'ask',
      text: ask.words ? firstLine(ask.words) : t('work.origin.askId', { id: ask.id }),
      open: doors.onOpenAsk ? () => doors.onOpenAsk!(ask.id) : undefined,
    });
  }
  if (found.length === 0 && session.kind === 'chat') found.push({ kind: 'repository', text: session.repository });
  return found;
}

/**
 * The requirement a session started with: the quest's requirements where it has them, else the quest's words, else the
 * ask's words (an intake's, or a line rooted in an ask), else a conversation's first message. Null where the record holds none.
 */
export function startingOf(
  quest: Quest | null | undefined, ask: AskOrigin | null | undefined, session: Session, opening: string | null | undefined,
): Starting | null {
  const requirements = (quest?.requirements ?? []).map((each) => each.quote).filter(Boolean);
  if (requirements.length > 0) return { kind: 'requirements', items: requirements };
  const words = quest?.body || quest?.title;
  if (words) return { kind: 'questWords', items: [words] };
  if (ask?.words) return { kind: 'askWords', items: [ask.words] };
  if (session.kind === 'chat' && !isHelp(session) && opening) return { kind: 'firstMessage', items: [opening] };
  return null;
}

const DOOR = 'cursor-pointer border-0 bg-transparent p-0 text-left font-sans text-small text-accent hover:underline';

/** The origins, each its kind and its name, the name a press where it opens. */
export function OriginLinks({ origins, t }: { origins: readonly Origin[]; t: TFunction }): ReactNode {
  return (
    <>
      {origins.map((origin) => (
        <span key={`${origin.kind}:${origin.text}`} className="inline-flex min-w-0 flex-wrap items-baseline gap-x-1.5">
          <span className="text-ink-faint">{t(`work.origin.kind.${origin.kind}`)}</span>
          {origin.open
            ? <button type="button" onClick={origin.open} className={DOOR}>{origin.text}</button>
            : <span>{origin.text}</span>}
        </span>
      ))}
    </>
  );
}

/** The requirement whole, under the words that say which record it is: a list of requirements, or the words as written. */
export function StartingWords({ starting, t }: { starting: Starting; t: TFunction }): ReactNode {
  return (
    <span className="grid min-w-0 gap-1">
      <span className="text-ink-faint">{t(`work.origin.with.${starting.kind}`)}</span>
      {starting.kind === 'requirements'
        ? <ol className="m-0 grid list-decimal gap-1 pl-5">{starting.items.map((item, index) => <li key={index} className="whitespace-pre-wrap">{item}</li>)}</ol>
        : <span className="whitespace-pre-wrap">{starting.items[0]}</span>}
    </span>
  );
}

/** *Details*' two rows: where it started from, and what it started with. Absent where the record names none. */
export function startedRows(t: TFunction, origins: readonly Origin[], starting: Starting | null): DetailRow[] {
  return [
    { label: t('work.origin.from'), value: origins.length > 0 ? <OriginLinks origins={origins} t={t} /> : null },
    { label: t('work.origin.started'), value: starting ? <StartingWords starting={starting} t={t} /> : null },
  ];
}
