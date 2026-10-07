import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { figure, list, moment } from '../format';
import { cn } from '../lib/cn';
import { ExternalLink } from '../links';
import type { AccountNamer } from '../tools';
import { Icon, type IconName, Inline, Pill, QUEST_TONE, SESSION_TONE } from '../ui';
import { HandedAccount } from './HandedAccount';
import {
  short, type TraceAnswer, type TraceAskLink, type TraceBranch, type TraceDue, type TraceKind, type TraceQuestLink,
  type TraceSessionLink, type TraceState, traceStory,
} from './trace';

/** What the page holds of the trace: the fold, and the read once asked. */
export type TraceDoor = {
  /** Whether the section is open: folded by default, and nothing is read until it opens. */
  open: boolean;
  onToggle: () => void;
  /** The route's answer, once read. */
  answer?: TraceAnswer | null;
  /** A first read is on its way. */
  reading?: boolean;
  /** The route's refusal, in its own sentence. */
  refusal?: string | null;
};

/**
 * **How this came to be** (TRACE1b, D143, D50): the screen's door to `daoris-driver trace`, at the foot of a session's page
 * and a quest's. The ask and the person's words, the quest with what they required and how its done answered, then each
 * session: what ran it, its tree, what it was handed, its rules, where its work landed, and what stood when it started.
 *
 * @remarks
 * - **Folded by default**, so a page reads nothing it was not asked for: the route reads every session record and quest. Once
 *   read, the folded line tells the story on one line, from the ask to the sessions.
 * - **Each link says where it was kept, and a link nothing keeps says so** (D143 point 3), marked *missing* where it would
 *   have been. Nothing is rebuilt from what stands now.
 * - **Worded from codes where Daoris speaks** (D142): every line here is the catalogue's, in the reader's language. The
 *   person's words, a requirement's quote and a done's answer are someone's words, shown as written; the driver's own notes
 *   (a start's account choice, an acceptance) are its English, marked *shown as recorded*.
 * - **No path**: a tree is its folder's name, a store is named by what it is (D47 §4).
 * - **No measure of its own** (D141): every block takes the page's width.
 *
 * A molecule: handed the answer and the fold, it reads the catalogue and nothing else.
 */
export function HowItCameToBe({
  kind, id, open, onToggle, answer, reading = false, refusal, onSession, onQuest, onAsk, className, nameOf,
}: TraceDoor & {
  /** What a person calls an account (ACCTNAME1, D152 §4.2), from the roster; absent, each record's id is said. */
  nameOf?: AccountNamer;
  /** What the page is: a session's or a quest's, and its id, which is not a door to itself. */
  kind: TraceKind;
  id: string;
  /** Attend another session, where Sessions can. */
  onSession?: (id: string) => void;
  /** Open a quest's page. */
  onQuest?: (id: string) => void;
  /** Open an ask's page. */
  onAsk?: (id: string) => void;
  /** Its place among the page's blocks: the page's own spacing. */
  className?: string;
}) {
  const { t } = useTranslation();
  const chain = answer?.chain ?? null;
  const doors: Doors = {
    session: (other) => (kind === 'session' && other === id ? undefined : onSession && (() => onSession(other))),
    quest: (other) => (kind === 'quest' && other === id ? undefined : onQuest && (() => onQuest(other))),
    ask: (other) => onAsk && (() => onAsk(other)),
  };

  return (
    <section aria-label={t('work.trace.title')} className={cn('min-w-0', className)}>
      <button
        type="button"
        aria-expanded={open}
        onClick={onToggle}
        className="flex w-full min-w-0 cursor-pointer items-start gap-1.5 border-0 bg-transparent p-0 text-left text-small text-ink-faint hover:text-ink"
      >
        <Icon name={open ? 'chevronDown' : 'chevronRight'} size={12} className="mt-1 shrink-0" />
        <span className="min-w-0 [overflow-wrap:anywhere]">
          <span className="font-semibold">{t('work.trace.title')}</span>
          {chain && <Story chain={chain} />}
        </span>
      </button>
      {open && (
        <div className="mt-2 grid min-w-0 gap-3 pl-[18px]">
          <p className="m-0 text-small text-ink-faint">{t('work.trace.hint')}</p>
          {reading && !answer && <p className="m-0 text-small text-ink-soft" role="status">{t('work.trace.reading')}</p>}
          {refusal && !answer && <p className="m-0 text-small text-ink-soft"><Inline text={refusal} /></p>}
          {answer && answer.unread.map((store) => (
            <Gap key={store.store} text={t(`work.trace.unread.${store.store}`)} />
          ))}
          {answer && !chain && <p className="m-0 text-small text-ink-soft">{t(`work.trace.nothing.${kind}`)}</p>}
          {chain && (
            <ol className="m-0 grid min-w-0 list-none gap-4 p-0">
              {chain.links.map((link, index) => (
                <li key={index} className="min-w-0">
                  {link.ask && <AskStep ask={link.ask} doors={doors} />}
                  {link.quest && <QuestStep quest={link.quest} doors={doors} />}
                  {link.session && <SessionStep session={link.session} doors={doors} nameOf={nameOf} />}
                  {link.unrecorded && (
                    <Step icon="frameWork" head={<span>{t('work.trace.session.head', { id: link.unrecorded.session })}</span>}>
                      <Gap text={t('work.trace.unrecorded', { id: link.unrecorded.session })} />
                      {link.unrecorded.branches.map((branch) => <Landed key={branch.branch + branch.at} branch={branch} />)}
                    </Step>
                  )}
                </li>
              ))}
            </ol>
          )}
          {answer && (answer.unread.length > 0 || !chain) && (
            <p className="m-0 text-meta text-ink-faint"><Inline text={t('work.trace.terminal', { kind, id })} /></p>
          )}
        </div>
      )}
    </section>
  );
}

/** Each kind of door, for an id: a press, or none where there is no door or it is the page's own. */
type Doors = Record<'session' | 'quest' | 'ask', (id: string) => (() => void) | undefined>;

/** The folded line's story: the ask it came from, the quests it reached, how many sessions worked them. */
function Story({ chain }: { chain: NonNullable<TraceAnswer['chain']> }) {
  const { t } = useTranslation();
  const story = traceStory(chain);
  const parts = [
    ...story.asks.map((ask) => t('work.trace.story.ask', { id: ask })),
    ...story.senders.map((name) => t('work.trace.story.sender', { name })),
    ...story.quests.map((quest) => t('work.trace.story.quest', { id: quest })),
    ...(story.sessions > 0 ? [t('work.trace.story.sessions', { count: story.sessions })] : []),
  ];
  if (parts.length === 0) return null;
  return <> · <Inline text={parts.join(' → ')} /></>;
}

/** One step of the chain: its glyph and head, the store it was read from, then what it says. */
function Step({ icon, head, source, children }: { icon: IconName; head: ReactNode; source?: string; children?: ReactNode }) {
  const { t } = useTranslation();
  return (
    <div className="grid min-w-0 gap-1.5 border-l-2 border-line pl-3">
      <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-0.5 text-body text-ink">
        <Icon name={icon} size={14} className="text-ink-faint" />
        {head}
        {source && <span className="text-meta text-ink-faint">{t('work.trace.from', { source: t(`work.trace.source.${source}`) })}</span>}
      </div>
      {children}
    </div>
  );
}

/** A link nothing keeps, said where it would have been. */
function Gap({ text }: { text: string }) {
  const { t } = useTranslation();
  return (
    <p className="m-0 text-small text-ink-soft [overflow-wrap:anywhere]">
      <span className="mr-1.5 inline-block rounded-control border border-dashed border-line-strong px-1 align-baseline text-meta leading-[1.4] text-ink-faint">
        {t('work.trace.missing')}
      </span>
      <Inline text={text} />
    </p>
  );
}

/** A line of facts, quiet, each part joined by a dot. */
function Facts({ parts }: { parts: (string | null | undefined | false)[] }) {
  const shown = parts.filter((part): part is string => Boolean(part));
  if (shown.length === 0) return null;
  return <p className="m-0 text-small text-ink-soft [overflow-wrap:anywhere]"><Inline text={shown.join(' · ')} /></p>;
}

/** An id that opens its page, where there is a door to it; otherwise its name, as text. */
function Door({ label, onOpen }: { label: string; onOpen?: () => void }) {
  if (!onOpen) return <span className="font-semibold">{label}</span>;
  return (
    <button
      type="button"
      onClick={onOpen}
      className="cursor-pointer border-0 bg-transparent p-0 font-semibold text-accent underline-offset-2 hover:underline"
    >
      {label}
    </button>
  );
}

/** Someone's words, as written: the person's, a requirement's, a done's. */
function Said({ meta, children }: { meta?: string; children: string }) {
  return (
    <div className="grid min-w-0 gap-0.5">
      {meta && <span className="text-meta text-ink-faint">{meta}</span>}
      <blockquote className="m-0 whitespace-pre-wrap border-l-2 border-line-strong pl-2 text-body text-ink [overflow-wrap:anywhere]">
        {children}
      </blockquote>
    </div>
  );
}

/** The driver's own English, which no code words, marked as the note's are (D142 points 4, 5). */
function Recorded({ meta, text }: { meta?: string; text: string }) {
  const { t } = useTranslation();
  return (
    <p className="m-0 text-small text-ink-soft [overflow-wrap:anywhere]">
      {meta && <span className="mr-1.5 text-meta text-ink-faint">{meta}</span>}
      <span className="mr-1.5 inline-block rounded-control border border-line px-1 align-baseline text-meta leading-[1.4] text-ink-faint">
        {t('work.handed.recorded')}
      </span>
      <Inline text={text} />
    </p>
  );
}

/** A small heading inside a step. */
function Part({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="grid min-w-0 gap-1">
      <span className="text-meta font-semibold text-ink-faint">{title}</span>
      {children}
    </div>
  );
}

/** The ask, from its record: the person's words, oldest first, and its go-aheads. */
function AskStep({ ask, doors }: { ask: TraceAskLink; doors: Doors }) {
  const { t, i18n } = useTranslation();
  const at = (iso: string) => moment(iso, i18n.language);
  const head = <Door label={t('work.trace.ask.head', { id: ask.id })} onOpen={doors.ask(ask.id)} />;
  if (ask.missing) {
    return (
      <Step icon="inbox" head={head} source={ask.source}>
        <Gap text={t(`work.trace.ask.gap.${ask.missing}`, { id: ask.id, namedBy: namedBy(t, ask.namedBy) })} />
      </Step>
    );
  }

  return (
    <Step icon="inbox" head={<>{head}{ask.state && <Pill>{t(`asks.state.${ask.state}`, { defaultValue: ask.state })}</Pill>}</>} source={ask.source}>
      <Facts parts={[
        t('work.trace.ask.facts', { workspace: ask.workspace, tier: t(`asks.tierShort.${ask.tier}`, { defaultValue: ask.tier ?? '' }) }),
      ]} />
      {ask.quests.length > 0 && (
        <p className="m-0 text-small text-ink-soft">
          {t('work.trace.ask.became', { quests: list(ask.quests.map((quest) => `#${quest}`), i18n.language) })}
        </p>
      )}
      {ask.intake && (
        <p className="m-0 text-small text-ink-soft">
          {ask.intake.state
            ? <>{t('work.trace.ask.intake', { id: ask.intake.session })} · {stateWords(t, ask.intake.state)}</>
            : t('work.trace.ask.intakeGone', { id: ask.intake.session })}
        </p>
      )}
      {ask.words ? (
        <Part title={t('work.trace.ask.words')}>
          {ask.words.map((word, index) => <Said key={index} meta={`${at(word.at)} · ${given(t, word)}`}>{word.text}</Said>)}
          {ask.wordsKeptFrom && <p className="m-0 text-small text-ink-soft">{t('work.trace.ask.wordsKeptFrom', { at: at(ask.wordsKeptFrom) })}</p>}
        </Part>
      ) : (
        <Part title={t('work.trace.ask.words')}>
          <Gap text={t('work.trace.ask.wordsNone')} />
          {ask.sentence && <Said>{ask.sentence}</Said>}
        </Part>
      )}
      <Part title={t('work.trace.ask.goAheads')}>
        {!ask.goAheads ? <Gap text={t('work.trace.ask.goAheadsBefore')} />
          : ask.goAheads.length === 0 ? <p className="m-0 text-small text-ink-soft">{t('work.trace.ask.goAheadsNone')}</p>
          : (
            <ul className="m-0 grid list-none gap-1 p-0">
              {ask.goAheads.map((goAhead) => (
                <li key={goAhead.number} className="min-w-0 text-small text-ink-soft [overflow-wrap:anywhere]">
                  <span className="text-ink">
                    {`#${goAhead.number} · `}
                    {t('asks.goAhead.act', { kind: t(`asks.goAhead.kind.${goAhead.kind}`, { defaultValue: goAhead.kind }), on: goAhead.on })}
                    {` “${goAhead.act}”`}
                  </span>
                  {' · '}
                  {goAhead.answer
                    ? t(goAhead.answer.approved ? 'work.trace.goAhead.approved' : 'work.trace.goAhead.refused', { at: at(goAhead.answer.at) })
                    : t('work.trace.goAhead.waiting')}
                  {goAhead.answer?.words && ` “${goAhead.answer.words}”`}
                  {goAhead.firstAskedBy && goAhead.firstAskedAt
                    && ` · ${t('work.trace.goAhead.first', { session: goAhead.firstAskedBy, at: at(goAhead.firstAskedAt) })}`}
                  {goAhead.near != null && ` · ${t('work.trace.goAhead.near', { near: goAhead.near })}`}
                </li>
              ))}
            </ul>
          )}
      </Part>
      {ask.note && (
        <Part title={t('work.trace.ask.close')}><Said>{ask.note}</Said></Part>
      )}
    </Step>
  );
}

/** The quest, as its moves replay: where it came from, what the person required and how its done answered. */
function QuestStep({ quest, doors }: { quest: TraceQuestLink; doors: Doors }) {
  const { t, i18n } = useTranslation();
  const at = (iso?: string | null) => (iso ? moment(iso, i18n.language) : t('work.trace.session.openedUnknown'));
  const head = <Door label={t('work.trace.quest.head', { id: quest.id })} onOpen={doors.quest(quest.id)} />;
  if (quest.missing) {
    return (
      <Step icon="quests" head={head} source={quest.source}>
        <Gap text={t(`work.trace.quest.gap.${quest.missing}`, { id: quest.id, namedBy: namedBy(t, quest.namedBy) })} />
        <Records quest={quest} doors={doors} />
      </Step>
    );
  }

  const status = quest.status as keyof typeof QUEST_TONE | undefined;
  return (
    <Step
      icon="quests"
      head={(
        <>
          {head}
          {quest.address && <span className="font-mono text-small text-ink-soft">→ {quest.address}</span>}
          {status && <Pill tone={QUEST_TONE[status] ?? 'neutral'}>{t(`status.${status}`, { defaultValue: status })}</Pill>}
        </>
      )}
      source={quest.source}
    >
      {quest.title && <p className="m-0 text-body text-ink [overflow-wrap:anywhere]">{quest.title}</p>}
      <Facts parts={[t('work.trace.quest.facts', { filed: at(quest.filed), moved: at(quest.moved) })]} />
      <p className="m-0 text-small text-ink-soft">
        {quest.ask
          ? <Inline text={t('work.trace.quest.byAsk', { id: quest.ask })} />
          : <Inline text={t('work.trace.quest.noAsk', { from: quest.from ?? '' })} />}
      </p>
      <Facts parts={[
        quest.parent && t('work.trace.quest.parent', { id: quest.parent }),
        quest.publishedBy && t('work.trace.quest.publishedBy', { id: quest.publishedBy }),
        quest.awaits && t('work.trace.quest.awaits', { id: quest.awaits }),
        quest.held && t('work.trace.quest.held'),
        quest.accepted && t('work.trace.quest.accepted', { at: at(quest.accepted) }),
      ]} />
      {quest.requirements.length > 0 && (
        <Part title={t('work.trace.quest.requirements')}>
          <ol className="m-0 grid list-none gap-2 p-0">
            {quest.requirements.map((requirement) => (
              <li key={requirement.number} className="grid min-w-0 gap-0.5">
                <Said meta={`${requirement.number} · ${t('work.trace.quest.check', { check: requirement.check })}`}>{requirement.quote}</Said>
                <p className="m-0 text-small text-ink-soft [overflow-wrap:anywhere]">
                  <span className={cn(requirement.answer === 'met' ? 'text-ink-done' : requirement.answer === 'departed' ? 'text-ink-open' : 'text-ink-faint')}>
                    {t(`work.trace.answer.${requirement.answer}`)}
                  </span>
                  {requirement.met && `${t('work.trace.colon')}${requirement.met}`}
                  {requirement.departed && `${t('work.trace.colon')}${requirement.departed}`}
                  {requirement.on && ` · ${t('work.trace.answerOn')} “${requirement.on}”`}
                </p>
              </li>
            ))}
          </ol>
        </Part>
      )}
      {quest.then.length > 0 && (
        <Facts parts={[t('work.trace.quest.then', { steps: list(quest.then.map((step) => `${step.to} “${step.title}”`), i18n.language) })]} />
      )}
      {quest.note && <Part title={t('work.trace.quest.close')}><Said>{quest.note}</Said></Part>}
      <Records quest={quest} doors={doors} />
    </Step>
  );
}

/** The session records that name a quest, oldest first, each a door to its session. */
function Records({ quest, doors }: { quest: TraceQuestLink; doors: Doors }) {
  const { t } = useTranslation();
  if (!quest.records) return <Gap text={t('work.trace.quest.recordsUnread')} />;
  if (quest.records.length === 0) return <p className="m-0 text-small text-ink-soft">{t('work.trace.quest.recordsNone')}</p>;
  return (
    <p className="m-0 flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5 text-small text-ink-soft">
      <span>{t('work.trace.quest.records')}</span>
      {quest.records.map((record) => (
        <span key={record.id} className="inline-flex items-baseline gap-1">
          <Door label={record.id} onOpen={doors.session(record.id)} />
          <span className="text-meta text-ink-faint">{t(`sessionState.${record.state}`, { defaultValue: record.state })}</span>
        </span>
      ))}
    </p>
  );
}

/** One session: what ran it, what it was handed, its rules, where its work landed, and what stood when it started. */
function SessionStep({ session, doors, nameOf }: { session: TraceSessionLink; doors: Doors; nameOf?: AccountNamer }) {
  const { t, i18n } = useTranslation();
  const at = (iso: string) => moment(iso, i18n.language);
  const state = session.state.state as keyof typeof SESSION_TONE;
  return (
    <Step
      icon="frameWork"
      head={(
        <>
          <Door label={t('work.trace.session.head', { id: session.id })} onOpen={doors.session(session.id)} />
          {session.state.state && <Pill tone={SESSION_TONE[state] ?? 'neutral'}>{t(`sessionState.${state}`, { defaultValue: state })}</Pill>}
        </>
      )}
      source={session.source}
    >
      <Facts parts={[
        stateNote(t, session.state),
        session.opened ? t('work.trace.session.opened', { at: at(session.opened) }) : t('work.trace.session.openedUnknown'),
      ]} />
      <Facts parts={[
        session.agent.adapter ? t('work.trace.session.agent', { agent: session.agent.adapter }) : t('work.trace.session.agentUnknown'),
        session.agent.harness ? t('work.trace.session.version', { version: session.agent.harness }) : t('work.trace.session.versionUnknown'),
        // The account by the person's name for it (ACCTNAME1), looked up as the trace is drawn; the record keeps the id.
        !session.teammate && (session.agent.account
          ? t('work.trace.session.account', {
            account: nameOf && session.agent.adapter ? nameOf(session.agent.adapter, session.agent.account) : session.agent.account,
          })
          : t('work.trace.session.ownSignIn')),
      ]} />
      {session.teammate ? (
        <p className="m-0 text-small text-ink-soft">{t('work.trace.session.teammate')}</p>
      ) : (
        <Facts parts={[
          session.tree ? t('work.trace.session.tree', { tree: `\`${session.tree}\`` }) : t('work.trace.session.treeUnknown'),
          session.baseCommit ? t('work.trace.session.base', { commit: `\`${short(session.baseCommit)}\`` }) : t('work.trace.session.baseUnknown'),
        ]} />
      )}
      {!session.quest && !session.ask && <p className="m-0 text-small text-ink-soft">{t('work.trace.session.chat')}</p>}
      {session.before && (
        <p className="m-0 text-small text-ink-soft">
          {session.before.first ? t('work.trace.session.first') : t(session.before.ended ? 'work.trace.session.after' : 'work.trace.session.afterRunning', {
            session: session.before.session,
            state: t(`sessionState.${session.before.state}`, { defaultValue: session.before.state ?? '' }),
            at: session.before.at ? at(session.before.at) : t('work.trace.session.openedUnknown'),
            tree: t(`work.trace.tree.${session.before.tree ?? 'unknown'}`),
          })}
        </p>
      )}
      {session.took && <p className="m-0 text-small text-ink-soft">{t('work.trace.session.took')}</p>}
      {session.answer && <Part title={t('work.trace.session.yourAnswer')}><Said>{session.answer}</Said></Part>}
      {session.events && <Events session={session} />}
      {session.rules && <Rules rules={session.rules} />}
      <Evidence session={session} />
      {session.landing && (
        <div className="grid min-w-0 gap-1">
          {session.landing.branches.map((branch) => <Landed key={branch.branch + branch.at} branch={branch} />)}
          {session.landing.due && <Due due={session.landing.due} />}
          {session.landing.missing && <Gap text={t(`work.trace.landing.gap.${session.landing.missing}`)} />}
        </div>
      )}
      {session.stood && <Stood session={session} />}
    </Step>
  );
}

/** This machine's record of it: the driver's start notes, each instruction by its size with what it was handed, acceptances. */
function Events({ session }: { session: TraceSessionLink }) {
  const { t, i18n } = useTranslation();
  const events = session.events!;
  if (events.missing) return <Gap text={t(`work.trace.events.gap.${events.missing}`)} />;
  return (
    <div className="grid min-w-0 gap-1.5">
      {events.starts.length > 0 && (
        <Part title={t('work.trace.session.starts')}>
          {events.starts.map((note, index) => <Recorded key={index} text={note} />)}
        </Part>
      )}
      {events.instructions.length === 0 ? (
        <p className="m-0 text-small text-ink-soft">
          {t(session.kind === 'chat' ? 'work.trace.session.chatInstruction' : 'work.trace.session.noInstruction')}
        </p>
      ) : events.instructions.map((instruction) => (
        <div key={instruction.seq} className="min-w-0">
          <p className="m-0 text-small text-ink-soft">
            {instruction.kept
              ? t('work.trace.session.instructionCut', { chars: figure(instruction.chars), kept: figure(instruction.kept) })
              : t('work.trace.session.instruction', { chars: figure(instruction.chars) })}
          </p>
          <HandedAccount account={instruction.account ?? null} ruled={false} />
        </div>
      ))}
      {events.accepted.map((note, index) => (
        <Recorded key={index} meta={t('work.trace.session.accepted', { at: moment(note.at, i18n.language) })} text={note.said} />
      ))}
    </div>
  );
}

/** The rules it was handed, by count, while their file stands; or why none are read. */
function Rules({ rules }: { rules: NonNullable<TraceSessionLink['rules']> }) {
  const { t } = useTranslation();
  if (rules.missing) return <Gap text={t(`work.trace.rules.gap.${rules.missing}`)} />;
  return (
    <Facts parts={[
      t('work.trace.session.rules', { allowed: figure(rules.allowed), asked: figure(rules.asked), denied: figure(rules.denied) }),
      rules.hard > 0 && t('work.trace.session.hard', { count: rules.hard }),
      rules.guarded && t('work.trace.session.guarded'),
    ]} />
  );
}

/** The commits the driver read off its tree at its end, or the record's own heading where it names none. */
function Evidence({ session }: { session: TraceSessionLink }) {
  const { t } = useTranslation();
  const evidence = session.evidence;
  if (!evidence) return session.teammate ? null : <p className="m-0 text-small text-ink-soft">{t('work.trace.session.noEvidence')}</p>;
  if (evidence.commits.length === 0) return <Recorded meta={t('work.trace.session.evidence')} text={evidence.said} />;
  return (
    <Part title={t('work.trace.session.commits')}>
      <ul className="m-0 grid list-none gap-0.5 p-0">
        {evidence.commits.map((commit) => (
          <li key={commit.sha} className="min-w-0 font-mono text-small text-ink [overflow-wrap:anywhere]">{commit.line}</li>
        ))}
      </ul>
    </Part>
  );
}

/** A branch a landing made: where from and when, a plugin's push and its pull request, and whether it is gone. */
function Landed({ branch }: { branch: TraceBranch }) {
  const { t, i18n } = useTranslation();
  const at = (iso: string) => moment(iso, i18n.language);
  const parts = [
    branch.line
      ? t('work.trace.session.landed', { branch: branch.branch, line: branch.line, at: at(branch.at) })
      : t('work.trace.session.landedNoLine', { branch: branch.branch, at: at(branch.at) }),
    branch.plugin && t(branch.pushed ? 'work.trace.session.pushed' : 'work.trace.session.handed', { plugin: branch.plugin }),
    branch.gone && t('work.trace.session.gone', { at: at(branch.gone) }),
    branch.removedAs && (branch.removedAs === 'inside' && branch.removedOn
      ? t('work.trace.session.removedInside', { on: branch.removedOn })
      : t('work.trace.session.removed')),
  ].filter((part): part is string => Boolean(part));
  // Who accepted it and the rule it was made under (LAND2b): a landing from before they were kept says so, and no rule is made up.
  const accepted = [
    branch.acceptedBy === 'auto' || branch.acceptedBy === 'person'
      ? t(`work.trace.acceptedBy.${branch.acceptedBy}`)
      : t('work.trace.acceptedUnknown'),
    ...(branch.rule ? [
      t(`work.trace.rule.${branch.rule.source}`, { defaultValue: branch.rule.source }),
      branch.rule.plugin ? t('work.trace.rulePlugin', { plugin: branch.rule.plugin }) : t('work.trace.ruleNoPlugin'),
      t(branch.rule.autoAccept ? 'work.trace.ruleAuto' : 'work.trace.rulePress'),
    ] : []),
  ];
  return (
    <div className="grid min-w-0 gap-0.5">
      <p className="m-0 text-small text-ink-soft [overflow-wrap:anywhere]">
        <Inline text={parts.join(' · ')} />
        {branch.pullRequest && branch.pushed && (
          <>
            {' · '}
            <ExternalLink href={branch.pullRequest} className="text-accent underline underline-offset-2">{t('work.trace.session.pullRequest')}</ExternalLink>
          </>
        )}
      </p>
      <p className="m-0 text-meta text-ink-faint [overflow-wrap:anywhere]"><Inline text={accepted.join(' · ')} /></p>
    </div>
  );
}

/** Its entry on the due list (LAND2b): when it became due, whether a try closed it, and each try by its code. */
function Due({ due }: { due: TraceDue }) {
  const { t, i18n } = useTranslation();
  const at = (iso: string) => moment(iso, i18n.language);
  return (
    <Part title={t('work.trace.due.title')}>
      <p className="m-0 flex min-w-0 flex-wrap items-baseline gap-x-2 text-small text-ink-soft">
        <span>
          {due.closed
            ? t('work.trace.due.closed', { since: at(due.since), closed: at(due.closed) })
            : t('work.trace.due.waiting', { since: at(due.since) })}
        </span>
        <span className="text-meta text-ink-faint">{t('work.trace.from', { source: t(`work.trace.source.${due.source}`) })}</span>
      </p>
      {due.tries.length === 0 ? <p className="m-0 text-small text-ink-soft">{t('work.trace.due.untried')}</p> : (
        <ol className="m-0 grid list-none gap-0.5 p-0">
          {due.tries.map((tried, index) => (
            <li key={index} className="min-w-0 text-small text-ink-soft [overflow-wrap:anywhere]">
              <Inline text={[
                `${at(tried.at)} · ${t(`work.trace.try.${tried.code}`, { defaultValue: tried.code })}`,
                tried.branch && t('work.trace.due.branch', { branch: tried.branch }),
                tried.commits != null && t('work.trace.due.commits', { count: tried.commits }),
                tried.uncommitted != null && t('work.trace.due.uncommitted', { count: tried.uncommitted }),
                tried.tip && t('work.trace.due.tip', { tip: short(tried.tip) }),
              ].filter((part): part is string => Boolean(part)).join(' · ')} />
            </li>
          ))}
        </ol>
      )}
    </Part>
  );
}

/** What stood when it started, by the moments kept: the standing answer, each go-ahead, and the person's words before it. */
function Stood({ session }: { session: TraceSessionLink }) {
  const { t, i18n } = useTranslation();
  const stood = session.stood!;
  const at = (iso?: string | null) => (iso ? moment(iso, i18n.language) : '');
  return (
    <Part title={t('work.trace.session.stood')}>
      {stood.missing ? <Gap text={t(`work.trace.stood.gap.${stood.missing}`)} /> : (
        <>
          {stood.standing && (
            <div className="grid min-w-0 gap-0.5">
              <p className="m-0 text-small text-ink-soft">
                {t(`work.trace.standing.${stood.standing.state}`, { repository: stood.standing.repository, at: at(stood.standing.at) })}
              </p>
              {stood.standing.says && <Said>{stood.standing.says}</Said>}
            </div>
          )}
          {stood.goAheads.map((goAhead) => (
            <p key={goAhead.number} className="m-0 text-small text-ink-soft">
              {t(`work.trace.goAhead.stood.${goAhead.state}`, { number: goAhead.number, at: at(goAhead.at) })}
              {goAhead.mine && goAhead.state === 'asked-after' && ` · ${t('work.trace.goAhead.mine')}`}
            </p>
          ))}
          {stood.words && (
            <p className="m-0 text-small text-ink-soft">
              {t('work.trace.session.wordsBefore', { before: figure(stood.words.before), of: figure(stood.words.of), ask: stood.words.ask })}
            </p>
          )}
        </>
      )}
    </Part>
  );
}

type T = ReturnType<typeof useTranslation>['t'];

/** What names a link nothing holds. */
const namedBy = (t: T, by?: { kind: string; id?: string | null } | null) =>
  t(`work.trace.namedBy.${by?.kind ?? 'trace'}`, { id: by?.id ?? '' });

/** How one of the person's words was given, and to which session on which quest. */
function given(t: T, word: { kind: string; session?: string | null; quest?: string | null }): string {
  const session = word.session ?? '';
  const how = word.kind === 'asked' ? t('work.trace.word.asked')
    : word.kind === 'answered' ? t('work.trace.word.answered', { session })
    : word.kind === 'added' ? t('work.trace.word.added', { session })
    : word.kind === 'reopened' ? t('work.trace.word.reopened', { session })
    : t('work.trace.word.said', { kind: word.kind, session });
  return word.quest ? `${how} · ${t('work.trace.word.onQuest', { quest: word.quest })}` : how;
}

/** What a record's flags add to its state, or null where they add nothing. */
const stateNote = (t: T, state: TraceState) =>
  state.limit ? t('work.trace.session.limit')
    : state.interrupted ? t('work.trace.session.interrupted')
    : state.answered ? t('work.trace.session.answered')
    : state.state === '' ? t('work.trace.session.stateUnknown')
    : null;

/** A record's state in words, with what its flags add. */
const stateWords = (t: T, state: TraceState) =>
  [t(`sessionState.${state.state}`, { defaultValue: state.state }), stateNote(t, state)].filter(Boolean).join(' · ');
