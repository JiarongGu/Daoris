import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { cn } from '../../lib/cn';
import { ago, clockOf } from '../../locales/format';
import { whoOf } from '../../asks/ChannelSection';
import type { ChannelItem } from '../channel';
import { Dot, Inline, SectionTitle } from '../../design/ui';
import { ExternalLink } from '../../design/links';
import { type Consideration, sittingBecause, sittingSentence } from '../../data/signals';
import type { AtWorkA, AtWorkChild, AtWorkHeld, AtWorkPart, AtWorkQuest, AtWorkSubagents } from './types';
import { askerLine, CHILD_STATE_KEY, childTone, HINT_KEY, joinedLine, LANES_GUIDE, LAST_SEEN, partStateKey, waitLine, waitWhat } from './words';

/**
 * The newest messages of the page's own channel read (COWORKVIEW1c, the design §3.3): the lines the session's panel already holds, so the
 * view asks the channel for nothing. Absent for a session with no ask, which has no channel and then no block.
 */
export type AtWorkChannel = {
  words: readonly ChannelItem[];
  /** *All messages*: opens the panel that holds the channel. */
  onOpen?: () => void;
};

export type AtWorkViewProps = {
  /** The channel's last words, each placed on the part whose session posted it. */
  channel?: AtWorkChannel;
  /** What the read holds; undefined before its first answer, which draws as reading. */
  held: AtWorkHeld | undefined;
  /** A query in error (the bridge refused): its words, drawn failed where nothing is held and stale over an answer. */
  error?: string | null;
  /** Open a part's page. */
  onOpen?: (session: string) => void;
  /** Open a quest's page. */
  onOpenQuest?: (quest: string) => void;
  /**
   * Why each open quest is sitting, as the driver's last look said it (the `considerations` slice): read beside the answer, never
   * composed into it. Empty before the first look, when an untaken quest says only that it is not taken yet.
   */
  considered?: readonly Consideration[];
  onRetry?: () => void;
  /** The console stream the window holds for a child, by its native id; a child with none offers no console. */
  streamOf?: (child: string) => string | undefined;
  /** Open a stream's tab in the console. */
  onOpenStream?: (stream: string) => void;
  /** The clock a time is read against; the window's now by default. */
  now?: Date;
};

/**
 * Who is at work on a session (COWORKVIEW1a, the cowork view design §5.2): the session's parts as the strip lists them, the
 * attended one first, and its subagents, each with its state and its end, read from Daoris's records and never from the
 * conversation's text. Reading is never *nobody*, a failure says its words, and a stale answer keeps its rows dimmed.
 */
export function AtWorkView({ held, error, onOpen, onOpenQuest, considered, onRetry, streamOf, onOpenStream, now, channel }: AtWorkViewProps) {
  const { t } = useTranslation();
  if (!held || 'reading' in held) {
    if (!held && error) return <Failed why={error} onRetry={onRetry} />;
    return (
      <div aria-busy="true" className="grid gap-2 p-3">
        <p className="m-0 text-small text-ink-faint">{t('work.atWork.reading')}</p>
        <div className="h-4 w-2/3 rounded-control bg-line/60" />
        <div className="h-4 w-1/2 rounded-control bg-line/60" />
      </div>
    );
  }
  if ('failed' in held) return <Failed why={held.failed.why || (error ?? '')} onRetry={onRetry} />;

  const stale = held.stale?.why ?? error ?? null;
  const { answer } = held;
  return (
    <div className="grid min-w-0 gap-3 p-3">
      {stale !== null && (
        <p role="status" className="m-0 flex flex-wrap items-baseline gap-2 text-small text-ink-soft">
          {t('work.atWork.stale', { why: stale })}
          {onRetry && <button type="button" className="text-accent underline" onClick={onRetry}>{t('work.atWork.retry')}</button>}
        </p>
      )}
      <div className={cn('grid min-w-0 gap-3', stale !== null && 'opacity-60')}>
        <WaitingOn answer={answer} />
        <Parts answer={answer} onOpen={onOpen} channel={channel} />
        <Subagents answer={answer} quiet={stale !== null} now={now} streamOf={streamOf} onOpenStream={onOpenStream} />
        <Quests answer={answer} considered={considered ?? []} onOpenQuest={onOpenQuest} />
        <AskedBy answer={answer} onOpenQuest={onOpenQuest} />
        {channel && <ChannelWords channel={channel} parts={listsParts(answer, t) ? answer.parts : []} />}
        <Hint answer={answer} />
      </div>
    </div>
  );
}

function Failed({ why, onRetry }: { why: string; onRetry?: () => void }) {
  const { t } = useTranslation();
  return (
    <p role="alert" className="m-0 flex flex-wrap items-baseline gap-2 p-3 text-small text-ink-soft">
      {t('work.atWork.failed', { why })}
      {onRetry && <button type="button" className="text-accent underline" onClick={onRetry}>{t('work.atWork.retry')}</button>}
    </p>
  );
}

/**
 * The attended part's own wait, first (the design §R11.3 row 3): *Waiting on #q7 in core (docs lane)*. Where it waits on nothing and the
 * session published quests, and no part of the entry waits, *published 3, waits on none*.
 */
function WaitingOn({ answer }: { answer: AtWorkA }) {
  const { t } = useTranslation();
  const wait = answer.parts.find((part) => part.key === answer.attended)?.waits;
  if (wait) return <p data-wait-line className="m-0 text-small text-ink">{t('work.atWork.waitingOn', { what: waitWhat(t, wait) })}</p>;
  if (!answer.published || answer.parts.some((part) => part.waits)) return null;
  return <p className="m-0 text-small text-ink-soft">{t('work.atWork.published', { count: answer.published })}</p>;
}

/** Whether the list says anything a lone part's page does not: a wait, how it joined, or parts folded away. */
function worthListing(answer: AtWorkA, t: TFunction): boolean {
  return answer.parts.some((part) => part.waits || (part.joined && joinedLine(t, part.joined) !== null)) || !!answer.earlier;
}

/** Whether the parts list is drawn: more than the one part, or something the one part's page does not say. */
function listsParts(answer: AtWorkA, t: TFunction): boolean {
  const more = Object.values(answer.moreParts ?? {}).reduce((sum, count) => sum + count, 0);
  return !(answer.parts.length < 2 && more === 0 && !worthListing(answer, t));
}

/** One message of the channel's last words, as the channel draws it: who, when, and the words as written. */
function Word({ item }: { item: ChannelItem }) {
  const { t } = useTranslation();
  const first = (item.text ?? '').trim();
  return (
    <li className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5 text-small text-ink-soft" title={first}>
      <span className="font-semibold text-ink">{whoOf(t, item)}</span>
      <span className="text-meta text-ink-faint">{ago(item.at)}</span>
      <span className="line-clamp-2 min-w-0 flex-1 basis-40 [overflow-wrap:anywhere]">{first}</span>
    </li>
  );
}

/**
 * The channel's last words that sit on no listed part (COWORKVIEW1c, the design §3.3): the person's, another line of work's, a session's
 * that this list does not hold; then *All messages*, which opens the panel the whole channel is in.
 */
function ChannelWords({ channel, parts }: { channel: AtWorkChannel; parts: readonly AtWorkPart[] }) {
  const { t } = useTranslation();
  const keys = new Set(parts.map((part) => part.key));
  const loose = channel.words.filter((item) => !item.session || !keys.has(item.session));
  return (
    <section className="grid min-w-0 gap-1">
      <SectionTitle level={3}>{t('work.atWork.group.channel')}</SectionTitle>
      {channel.words.length === 0 && <p className="m-0 text-small text-ink-faint">{t('work.atWork.channel.none')}</p>}
      {loose.length > 0 && <ol className="m-0 grid list-none gap-1 p-0">{loose.map((item) => <Word key={item.id ?? item.at} item={item} />)}</ol>}
      {channel.onOpen && (
        <div>
          <button type="button" className="text-small text-accent underline underline-offset-2" onClick={channel.onOpen}>
            {t('work.atWork.channel.all')}
          </button>
        </div>
      )}
    </section>
  );
}

/**
 * The entry's parts, each one button opening its part: its number, agent, its record's own state, what it waits on as the strip places
 * it, how it joined, and its subagents' count. A waiting part is never folded; the attended part's wait is the line above, not repeated.
 */
function Parts({ answer, onOpen, channel }: { answer: AtWorkA; onOpen?: (session: string) => void; channel?: AtWorkChannel }) {
  const { t } = useTranslation();
  const more = Object.values(answer.moreParts ?? {}).reduce((sum, count) => sum + count, 0);
  if (!listsParts(answer, t)) return null;
  return (
    <section className="grid min-w-0 gap-1">
      <SectionTitle level={3}>{t('work.atWork.group.parts')}</SectionTitle>
      <ol className="m-0 grid list-none gap-0.5 p-0">
        {answer.parts.map((part) => {
          const joined = part.joined ? joinedLine(t, part.joined) : null;
          return (
            <li key={part.key}>
              <button
                type="button"
                aria-current={part.key === answer.attended || undefined}
                onClick={() => onOpen?.(part.key)}
                className={cn(
                  'flex w-full min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5 rounded-control border-l-[3px] px-2 py-1 text-left text-small',
                  'hover:bg-accent-soft/50',
                  part.key === answer.attended ? 'border-l-accent bg-accent-soft text-ink' : 'border-l-transparent text-ink-soft',
                )}
              >
                {part.number !== undefined && <span className="font-mono text-meta text-ink-faint">{part.number}</span>}
                {part.agent && <span className="font-mono text-meta text-ink-faint">{part.agent}</span>}
                {part.state && <Dot tone={part.state === 'working' ? 'live' : 'ended'} label={t(partStateKey(part.state))} />}
                {part.waits && part.key !== answer.attended && <Dot tone="idle" label={waitLine(t, part.waits)} className="whitespace-normal" />}
                <PartChildren t={t} part={part} />
                {(joined || part.alongside) && (
                  <span className="basis-full text-meta text-ink-faint">
                    {[joined, part.alongside ? t('work.atWork.part.alongside') : null].filter(Boolean).join(' · ')}
                  </span>
                )}
              </button>
              {(channel?.words ?? []).some((item) => item.session === part.key) && (
                <ol className="m-0 mt-0.5 grid list-none gap-0.5 p-0 ps-4">
                  {channel!.words.filter((item) => item.session === part.key).map((item) => <Word key={item.id ?? item.at} item={item} />)}
                </ol>
              )}
            </li>
          );
        })}
      </ol>
      {(answer.earlier ?? 0) > 0 && <p className="m-0 text-meta text-ink-faint">{t('work.atWork.part.earlier', { count: answer.earlier })}</p>}
      {more > 0 && <p className="m-0 text-meta text-ink-faint">{t('work.atWork.part.more', { count: more })}</p>}
    </section>
  );
}

function PartChildren({ t, part }: { t: TFunction; part: AtWorkPart }) {
  const { children } = part;
  if (children.elsewhere) return <span className="text-meta text-ink-faint">{t('work.atWork.elsewhere')}</span>;
  if (children.unreadable) return <span className="text-meta text-ink-faint">{t('work.atWork.unreadable')}</span>;
  if (!children.count) return null;
  return (
    <span className="text-meta text-ink-faint">
      {t('work.atWork.part.children', { count: children.count, working: children.working ?? 0 })}
    </span>
  );
}

/** Whether *Nobody else is working on this* may be said (§5.2, rule 16): only where every source was read and holds nobody. */
export function nobody(answer: AtWorkA): boolean {
  const { subagents, parts } = answer;
  return answer.parts.length === 1 && !answer.moreParts && !answer.earlier && !answer.published && !answer.quests?.length && !answer.parts[0]!.waits
    && (subagents.read === 'read' || subagents.read === 'none')
    && subagents.children.length === 0 && !subagents.observed && !subagents.unplaced && !subagents.seenByCalls
    && parts.every((part) => !part.children.unreadable && !part.children.elsewhere);
}

/** The attended part's subagents as a tree, the bound's counts under them. */
function Subagents({ answer, quiet, now, streamOf, onOpenStream }: {
  answer: AtWorkA; quiet: boolean; now?: Date; streamOf?: AtWorkViewProps['streamOf']; onOpenStream?: AtWorkViewProps['onOpenStream'];
}) {
  const { t } = useTranslation();
  const subagents: AtWorkSubagents = answer.subagents;
  if (subagents.read === 'elsewhere') return <p className="m-0 text-small text-ink-faint">{t('work.atWork.elsewhere')}</p>;
  if (!quiet && nobody(answer)) return <p className="m-0 text-small text-ink-faint">{t('work.atWork.empty')}</p>;

  const more = Object.values(subagents.more ?? {}).reduce((sum, count) => sum + count, 0);
  const moreWorking = subagents.more?.working ?? 0;
  return (
    <section className="grid min-w-0 gap-1">
      <div className="flex flex-wrap items-baseline gap-x-2">
        <SectionTitle level={3}>{t('work.atWork.group.subagents')}</SectionTitle>
        {subagents.working > 0 && <span className="text-meta text-accent">{t('work.atWork.subagentsWorking', { count: subagents.working })}</span>}
        {subagents.turnWaits && <span className="text-meta text-ink-soft">{t('work.atWork.subagentsTurnWaits')}</span>}
      </div>
      <p className="m-0 text-meta text-ink-faint">{t('work.atWork.subagentsNote')}</p>
      {subagents.read === 'unreadable' && <p className="m-0 text-small text-ink-soft">{t('work.atWork.unreadable')}</p>}
      {subagents.seenByCalls && <p className="m-0 text-small text-ink-faint">{t('work.atWork.subagentsByCalls')}</p>}
      {(subagents.observed ?? 0) > 0 && <p className="m-0 text-meta text-ink-faint">{t('work.atWork.observed', { count: subagents.observed })}</p>}
      {(subagents.unplaced ?? 0) > 0 && <p className="m-0 text-meta text-ink-faint">{t('work.atWork.unplaced', { count: subagents.unplaced })}</p>}
      {subagents.children.length > 0 && (
        <ol className="m-0 grid list-none gap-0.5 p-0">
          {subagents.children.map((child) => (
            <ChildRow key={child.key} child={child} now={now} stream={streamOf?.(child.key)} onOpenStream={onOpenStream} />
          ))}
        </ol>
      )}
      {(subagents.folded ?? 0) > 0 && <p className="m-0 text-meta text-ink-faint">{t('work.atWork.child.folded', { count: subagents.folded })}</p>}
      {more > 0 && (
        <p className="m-0 text-meta text-ink-faint">
          {moreWorking > 0 ? t('work.atWork.moreWorking', { count: more, working: moreWorking }) : t('work.atWork.more', { count: more })}
        </p>
      )}
    </section>
  );
}

/**
 * The quests the entry's parts asked that opened no part of it (the design §3.1): one button each, opening the quest, with who took it
 * and what the taker waits on. An untaken quest says why beside it from the driver's last look, none before the first. A chain's step
 * is set in under the quest before it.
 */
function Quests({ answer, considered, onOpenQuest }: {
  answer: AtWorkA; considered: readonly Consideration[]; onOpenQuest?: (quest: string) => void;
}) {
  const { t } = useTranslation();
  const quests = answer.quests;
  if (!quests?.length) return null;
  const more = answer.moreQuests ?? 0;
  return (
    <section className="grid min-w-0 gap-1">
      <SectionTitle level={3}>{t('work.atWork.group.quests')}</SectionTitle>
      <ol className="m-0 grid list-none gap-0.5 p-0">
        {quests.map((quest) => (
          <li key={quest.id} style={quest.step ? { paddingInlineStart: '12px' } : undefined}>
            <button
              type="button"
              onClick={() => onOpenQuest?.(quest.id)}
              className="flex w-full min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5 rounded-control border-l-[3px] border-l-transparent px-2 py-1 text-left text-small text-ink-soft hover:bg-accent-soft/50"
            >
              <span className="font-mono text-meta text-ink-faint">#{quest.id}</span>
              <span className="min-w-0 text-ink [overflow-wrap:anywhere]">{quest.title}</span>
              {quest.of && <span className="text-meta text-ink-faint">{t('work.atWork.quest.of', { repository: quest.of })}</span>}
              {quest.lanes?.length ? (
                <span className="text-meta text-ink-faint">{t('work.atWork.quest.lanes', { lanes: quest.lanes.join(', '), count: quest.lanes.length })}</span>
              ) : null}
              <QuestTaker t={t} quest={quest} considered={considered} />
            </button>
          </li>
        ))}
      </ol>
      {more > 0 && <p className="m-0 text-meta text-ink-faint">{t('work.atWork.quest.more', { count: more })}</p>}
    </section>
  );
}

/** Who took a quest and how it stands: its own line under the quest's name. */
function QuestTaker({ t, quest, considered }: { t: TFunction; quest: AtWorkQuest; considered: readonly Consideration[] }) {
  const { taker } = quest;
  const sitting = taker?.untaken ? sittingBecause(considered, quest.id) : null;
  return (
    <span className="flex basis-full flex-wrap items-baseline gap-x-2 gap-y-0.5 text-meta text-ink-faint">
      {taker?.session !== undefined && (
        <>
          {taker.agent && <span className="font-mono">{taker.agent}</span>}
          {taker.state && <Dot tone={taker.state === 'working' ? 'live' : 'ended'} label={t(partStateKey(taker.state))} />}
        </>
      )}
      {taker?.conversation !== undefined && <span>{t('work.atWork.quest.takenBy', { conversation: taker.conversation })}</span>}
      {taker?.elsewhere && <span>{t('work.atWork.quest.elsewhere')}</span>}
      {taker?.unrecorded && <span>{t('work.atWork.quest.unrecorded')}</span>}
      {taker?.untaken && (
        <span>{sitting ? t('work.atWork.quest.untakenWhy', { why: sittingSentence(sitting) }) : t('work.atWork.quest.untaken')}</span>
      )}
      {!taker && quest.status === 'Declined' && <span>{t('work.atWork.quest.declined')}</span>}
      {quest.waits && <Dot tone="idle" label={waitLine(t, quest.waits)} className="whitespace-normal" />}
      {quest.step && <span>{t('work.atWork.quest.nextStep', { quest: quest.step })}</span>}
    </span>
  );
}

/** What asked the attended part's quest (the design §3.1): one row, opening that quest. */
function AskedBy({ answer, onOpenQuest }: { answer: AtWorkA; onOpenQuest?: (quest: string) => void }) {
  const { t } = useTranslation();
  const { askedBy } = answer;
  if (!askedBy) return null;
  return (
    <section className="grid min-w-0 gap-1">
      <SectionTitle level={3}>{t('work.atWork.group.askedBy')}</SectionTitle>
      <button
        type="button"
        onClick={() => onOpenQuest?.(askedBy.quest)}
        className="w-full min-w-0 rounded-control border-l-[3px] border-l-transparent px-2 py-1 text-left text-small text-ink-soft hover:bg-accent-soft/50"
      >
        {askerLine(t, askedBy, answer.parts)}
      </button>
    </section>
  );
}

/**
 * Where the repository's way to hand rows on is not open (the design §6.4 point 3): the standing the driver judged, said where the
 * session ran subagents, with the guide's lanes section behind it. A fact about the repository and this machine, never a verdict on
 * the agent's choice.
 */
function Hint({ answer }: { answer: AtWorkA }) {
  const { t } = useTranslation();
  if (!answer.hint) return null;
  return (
    <p data-hint={answer.hint} className="m-0 text-small text-ink-soft">
      <Inline text={t(HINT_KEY[answer.hint])} />{' '}
      <ExternalLink href={LANES_GUIDE} className="text-accent underline underline-offset-2">{t('work.atWork.hint.guide')}</ExternalLink>
    </p>
  );
}

/** How far a row is set in: at most two steps, a deeper child saying its depth. */
const STEPS = 2;

function ChildRow({ child, now, stream, onOpenStream }: {
  child: AtWorkChild; now?: Date; stream?: string; onOpenStream?: AtWorkViewProps['onOpenStream'];
}) {
  const { t, i18n } = useTranslation();
  const time = (iso: string | undefined) => (iso ? clockOf(iso, now, i18n.language) : '');
  const depth = child.depth ?? 1;
  const tone = childTone(child.state);
  const word = t(CHILD_STATE_KEY[child.state]);
  const said = (() => {
    if (child.state === 'unsaid') return `${t('work.atWork.child.firstSeen', { time: time(child.since) })} · ${word}`;
    if (LAST_SEEN.has(child.state)) return child.lastSeen ? `${word} · ${t('work.atWork.child.lastSeen', { time: time(child.lastSeen) })}` : word;
    if (child.state === 'working') return child.startSaid || !child.since ? word : `${word} · ${t('work.atWork.child.firstSeen', { time: time(child.since) })}`;
    return child.ended ? `${word} ${time(child.ended)}` : word;
  })();
  return (
    <li
      className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5 py-0.5 text-small"
      style={{ paddingInlineStart: `${Math.min(Math.max(depth - 1, 0), STEPS) * 12}px` }}
    >
      {child.type && <span className="text-ink">{child.type}</span>}
      {child.label && <span className="min-w-0 truncate text-ink-soft" title={child.label.text}>{child.label.text}</span>}
      {!child.label && !child.type && <span className="font-mono text-meta text-ink-faint">{child.key}</span>}
      {tone === 'live'
        ? <Dot tone="live" label={said} />
        : <span className={cn('text-meta', tone === 'warn' ? 'text-warn' : tone === 'failed' ? 'text-ink-danger' : 'text-ink-soft')}>{said}</span>}
      {stream !== undefined && onOpenStream && (
        <button type="button" className="text-meta text-accent underline" onClick={() => onOpenStream(stream)}>
          {t('work.atWork.child.console')}
        </button>
      )}
      {child.parentKey === undefined && child.depth === undefined && (
        <span className="text-meta text-ink-faint">{t('work.atWork.child.noParent')}</span>
      )}
      {depth - 1 > STEPS && <span className="text-meta text-ink-faint">{t('work.atWork.child.depth', { depth })}</span>}
    </li>
  );
}
