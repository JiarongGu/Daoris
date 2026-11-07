import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import { Pill, SectionTitle, SESSION_TONE, type ShownState } from '../../design/ui';
import type { Consideration } from '../../data/signals';
import type { ChannelItem } from '../channel';
import type { AtWorkAskA, AtWorkAskEntry, AtWorkHeld } from './types';
import { type AtWorkChannel, Failed, QuestRows, Word } from './AtWorkView';

export type AtWorkAskViewProps = {
  /** What the ask's read holds; undefined before its first answer, which draws as reading. */
  held: AtWorkHeld | undefined;
  /** A query in error (the bridge refused): its words, drawn failed where nothing is held and stale over an answer. */
  error?: string | null;
  /** Open a line of work's own view: the part its row opens, attended with *At work* shown. */
  onOpenEntry?: (session: string) => void;
  /** Open a quest's page. */
  onOpenQuest?: (quest: string) => void;
  /** Why each open quest is sitting, as the driver's last look said it (the `considerations` slice), read beside the answer. */
  considered?: readonly Consideration[];
  onRetry?: () => void;
  /** The ask page's own channel read, its newest three messages: each on the line of work that posted it. */
  channel?: AtWorkChannel;
};

/**
 * Who is at work on an ask: its lines of work, each folded to a count that opens its own
 * view, and the quests it asked as a session's view draws them, with the channel's last words. It replaces nothing on the ask's page: the
 * sessions, the work and the channel keep their presses, and this section sits above them. Reading is never *nothing*; an ask with
 * nothing at work says so.
 */
export function AtWorkAskView({ held, error, onOpenEntry, onOpenQuest, considered, onRetry, channel }: AtWorkAskViewProps) {
  const { t } = useTranslation();
  if (!held || 'reading' in held) {
    if (!held && error) return <Failed why={error} onRetry={onRetry} />;
    return (
      <div aria-busy="true" className="grid gap-2">
        <p className="m-0 text-small text-ink-faint">{t('work.atWork.reading')}</p>
        <div className="h-4 w-2/3 rounded-control bg-line/60" />
      </div>
    );
  }
  if ('failed' in held) return <Failed why={held.failed.why || (error ?? '')} onRetry={onRetry} />;
  if (!('askAnswer' in held)) return null;

  const stale = held.stale?.why ?? error ?? null;
  const answer = held.askAnswer;
  return (
    <div className="grid min-w-0 gap-3">
      {stale !== null && (
        <p role="status" className="m-0 flex flex-wrap items-baseline gap-2 text-small text-ink-soft">
          {t('work.atWork.stale', { why: stale })}
          {onRetry && <button type="button" className="text-accent underline" onClick={onRetry}>{t('work.atWork.retry')}</button>}
        </p>
      )}
      <div className={cn('grid min-w-0 gap-3', stale !== null && 'opacity-60')}>
        <Entries answer={answer} onOpenEntry={onOpenEntry} channel={channel} />
        <QuestRows quests={answer.quests} more={answer.moreQuests ?? 0} considered={considered ?? []} onOpenQuest={onOpenQuest} />
        {channel && <AskChannel channel={channel} entries={answer.entries} />}
      </div>
    </div>
  );
}

/** A line of work's messages among the channel's last words: those its parts posted, by the line's key. */
const postedBy = (channel: AtWorkChannel | undefined, entry: AtWorkAskEntry): ChannelItem[] =>
  (channel?.words ?? []).filter((item) => item.entry === entry.entry);

function Entries({ answer, onOpenEntry, channel }: { answer: AtWorkAskA; onOpenEntry?: (session: string) => void; channel?: AtWorkChannel }) {
  const { t } = useTranslation();
  if (answer.entries.length === 0) {
    return <p className="m-0 text-small text-ink-faint">{t('work.atWork.ask.none')}</p>;
  }
  return (
    <section className="grid min-w-0 gap-1">
      <ol className="m-0 grid list-none gap-0.5 p-0">
        {answer.entries.map((entry) => (
          <li key={entry.entry}>
            <button
              type="button"
              onClick={() => onOpenEntry?.(entry.opens)}
              className="flex w-full min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5 rounded-control border-l-[3px] border-l-transparent px-2 py-1 text-left text-small text-ink-soft hover:bg-accent-soft/50"
            >
              <span className="min-w-0 text-ink [overflow-wrap:anywhere]">{entry.repository}</span>
              {/* A word this page has none for is said as the driver wrote it. */}
              <Pill tone={SESSION_TONE[entry.shown as ShownState] ?? 'neutral'}>{t(`sessionState.${entry.shown}`, { defaultValue: entry.shown })}</Pill>
              <span className="text-meta text-ink-faint">{t('work.atWork.ask.parts', { count: entry.parts, working: entry.working })}</span>
              {entry.subagents > 0 && (
                <span className="text-meta text-ink-faint">{t('work.atWork.part.children', { count: entry.subagents, working: entry.subagentsWorking })}</span>
              )}
              {entry.elsewhere && <span className="basis-full text-meta text-ink-faint">{t('work.atWork.elsewhere')}</span>}
            </button>
            {postedBy(channel, entry).length > 0 && (
              <ol className="m-0 mt-0.5 grid list-none gap-0.5 p-0 ps-4">
                {postedBy(channel, entry).map((item) => <Word key={item.id ?? item.at} item={item} />)}
              </ol>
            )}
          </li>
        ))}
      </ol>
      {(answer.moreEntries ?? 0) > 0 && <p className="m-0 text-meta text-ink-faint">{t('work.atWork.ask.moreEntries', { count: answer.moreEntries })}</p>}
    </section>
  );
}

/** The channel's last words that sit on no line of work listed, and *All messages* down to the ask's own channel section. */
function AskChannel({ channel, entries }: { channel: AtWorkChannel; entries: readonly AtWorkAskEntry[] }) {
  const { t } = useTranslation();
  const keys = new Set(entries.map((entry) => entry.entry));
  const loose = channel.words.filter((item) => !item.entry || !keys.has(item.entry));
  return (
    <section className="grid min-w-0 gap-1">
      <SectionTitle level={3}>{t('work.atWork.group.channel')}</SectionTitle>
      {channel.words.length === 0 && <p className="m-0 text-small text-ink-faint">{t('work.atWork.channel.none')}</p>}
      {loose.length > 0 && <ol className="m-0 grid list-none gap-1 p-0">{loose.map((item) => <Word key={item.id ?? item.at} item={item} />)}</ol>}
      {channel.onOpen && (
        <div>
          <button type="button" className="text-small text-accent underline underline-offset-2" onClick={channel.onOpen}>{t('work.atWork.channel.all')}</button>
        </div>
      )}
    </section>
  );
}
