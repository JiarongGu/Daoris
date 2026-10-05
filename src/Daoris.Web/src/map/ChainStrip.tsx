import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../api';
import { elapsed, sessionTool } from '../format';
import type { AccountNamer } from '../tools';
import { Pill, QUEST_TONE, SectionTitle, SESSION_TONE } from '../ui';
import { cn } from '../lib/cn';
import { questName } from '../work/identity';
import type { ChainStep } from './chain';

/** The mark per step — an arrangement of existing tokens (D41), and never the only signal. */
const MARK: Record<ChainStep['kind'], string> = {
  ask: 'border-line-strong bg-raised',
  unseen: 'border-line bg-page',
  quest: 'border-accent bg-accent-soft',
  // Hollow and solid: a dashed ring at seven pixels was a smudge in dark (seen on the window).
  pending: 'border-ink-faint bg-page',
};

/**
 * How one piece of work was carried (MAP1, D67 §3): the ask, the quests before and after this one,
 * the steps still to come, and on what each quest ran — every session's agent, version and account,
 * read from its record (D49 §4).
 *
 * @remarks
 * A molecule: the chain arrives built (`buildChain`), so every shape is reachable in a story. The
 * same strip renders on a quest's page and beside an attended session. Each surface decides what
 * pressing a quest or a session does, so the strip adds no act of its own.
 *
 * **Every state is said in words** (D41): a quest's status and a session's state are pills, the
 * quest being read is named as such, and a step not yet published says so. A mark's hue only
 * repeats what the words already say.
 */
export function ChainStrip({ chain, level = 2, attended, onQuest, onSession, nameOf }: {
  chain: ChainStep[];
  /** What a person calls an account (ACCTNAME1, D152 §4.2), from the roster; absent, each record's id is said. */
  nameOf?: AccountNamer;
  /** The session being read, where the strip sits beside one: marked, and no door to itself (SESS1 S7). */
  attended?: string;
  /** The heading level where it sits: its own section on a quest's page, under a session's head in Work. */
  level?: 2 | 3;
  onQuest?: (quest: Quest) => void;
  onSession?: (session: Session) => void;
}) {
  const { t } = useTranslation();

  return (
    <section aria-label={t('chain.title')}>
      <SectionTitle level={level}>{t('chain.title')}</SectionTitle>
      <ol className="m-0 list-none p-0">
        {chain.map((step, index) => (
          <li key={key(step, index)} className="group relative pb-3 pl-5 last:pb-0">
            {/* The rail behind the marks, stopping at the last one rather than trailing into nothing. */}
            <span aria-hidden className="absolute bottom-0 left-[3px] top-3 w-px bg-line group-last:hidden" />
            <span
              aria-hidden
              className={cn('absolute left-0 top-1.5 size-[7px] rounded-full border', MARK[step.kind],
                step.kind === 'quest' && step.current && 'size-[9px] -left-px top-[5px] bg-accent')}
            />
            <Step step={step} attended={attended} onQuest={onQuest} onSession={onSession} nameOf={nameOf} />
          </li>
        ))}
      </ol>
      {chain.some((step) => step.kind === 'pending') && (
        <p className="m-0 mt-2 text-meta text-ink-faint">{t('chain.pendingHint')}</p>
      )}
    </section>
  );
}

function key(step: ChainStep, index: number): string {
  if (step.kind === 'quest') return `q-${step.quest.id}`;
  if (step.kind === 'pending') return `p-${index}`;
  return `${step.kind}-${step.id}`;
}

function Step({ step, attended, onQuest, onSession, nameOf }: {
  step: ChainStep;
  attended?: string;
  nameOf?: AccountNamer;
  onQuest?: (quest: Quest) => void;
  onSession?: (session: Session) => void;
}) {
  const { t } = useTranslation();

  if (step.kind === 'ask') {
    return (
      <p className="m-0 flex flex-wrap items-baseline gap-x-2 text-small">
        <span className="font-mono text-meta text-ink">{t('chain.ask', { id: step.id })}</span>
        <span className="text-ink-faint">{t('chain.askHint')}</span>
      </p>
    );
  }

  if (step.kind === 'unseen') {
    return (
      <p className="m-0 flex flex-wrap items-baseline gap-x-2 text-small">
        <span className="font-mono text-meta text-ink-soft">#{step.id}</span>
        <span className="text-ink-faint">{t('chain.unseen')}</span>
      </p>
    );
  }

  if (step.kind === 'pending') {
    return (
      <div className="min-w-0 text-small">
        <p className="m-0 flex flex-wrap items-baseline gap-x-2">
          <span className="text-accent">→ {step.step.to}</span>
          <span className="min-w-0 text-ink-soft">{step.step.title}</span>
        </p>
        <p className="m-0 text-meta text-ink-faint">{t('chain.pending')}</p>
      </div>
    );
  }

  const { quest, sessions, current } = step;
  // The agent every session of this quest ran on, where it is one, with its version: said once, never on every row.
  const agents = new Set(sessions.map((session) => sessionTool({ adapter: session.adapter, harnessVersion: session.harnessVersion })));
  const agentOnce = agents.size === 1 ? [...agents][0]! : null;
  const title = onQuest && !current
    ? (
      <button
        type="button"
        onClick={() => onQuest(quest)}
        // Underlined at rest, faintly: an ink title among ink titles gave no sign it was a door.
        title={quest.title}
        className="min-w-0 cursor-pointer truncate border-0 bg-transparent p-0 text-left text-small text-ink underline decoration-line-strong underline-offset-2 hover:text-accent hover:decoration-accent"
      >
        {questName(quest)}
      </button>
    )
    // Each step by its name (SESSUX1j), as every list names a quest, whole on its tip.
    : <span title={quest.title} className={cn('min-w-0 truncate text-small text-ink', current && 'font-semibold')}>{questName(quest)}</span>;

  return (
    <div className="grid min-w-0 gap-1">
      <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
        {title}
        <Pill tone={QUEST_TONE[quest.status]}>{t(`status.${quest.status}`)}</Pill>
        {current && <span className="text-meta text-ink-faint">{t('chain.current')}</span>}
      </p>
      <p className="m-0 font-mono text-meta text-ink-faint">
        #{quest.id} · {t('chain.route', { from: quest.from, to: quest.to })}
      </p>
      {sessions.length === 0
        ? (
          <p className="m-0 text-meta text-ink-faint">
            {/* A closed quest nobody drove was closed by a person; "yet" would promise a session. */}
            {t(quest.status === 'Done' || quest.status === 'Declined' ? 'chain.closedUnrun' : 'chain.noSession')}
          </p>
        )
        : (
          <>
            {/* The agent said once where every session of the quest ran on the same one (UX7c, D152 §7): the install's
                four rows each repeated it, and the fact that mattered, three failed before this one, was lost among them. */}
            {agentOnce && sessions.length > 1 && <p className="m-0 font-mono text-meta text-ink-faint">{agentOnce}</p>}
            <ul className="m-0 grid list-none gap-1 p-0">
              {sessions.map((session, index) => {
                // A quest carried on several times drew identical rows: which attempt, its account and how long tell them
                // apart, and the one being read says so and is no door to itself (SESS1 S7).
                const here = session.id === attended;
                const name = agentOnce && sessions.length > 1
                  ? t('chain.attempt', { n: index + 1 })
                  : sessionTool(session, false, nameOf);
                // By the person's name for it (ACCTNAME1): a name, not an id, so it is set in the row's words, not as code.
                const account = agentOnce && sessions.length > 1 && session.profile
                  ? nameOf ? nameOf(session.adapter, session.profile) : session.profile
                  : null;
                return (
                  <li key={session.id} className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-0.5">
                    <Pill tone={SESSION_TONE[session.state]}>{t(`sessionState.${session.state}`)}</Pill>
                    {onSession && !here
                      ? (
                        <button
                          type="button"
                          onClick={() => onSession(session)}
                          title={sessionTool(session, false, nameOf)}
                          // Underlined at rest, faintly, as a quest title that is a door is.
                          className="min-w-0 cursor-pointer truncate border-0 bg-transparent p-0 text-left font-mono text-meta text-ink-soft underline decoration-line-strong underline-offset-2 hover:text-accent hover:decoration-accent"
                        >
                          {name}
                        </button>
                      )
                      : <span title={sessionTool(session, false, nameOf)} className="min-w-0 truncate font-mono text-meta text-ink-soft">{name}</span>}
                    {account && <span className="text-meta text-ink-faint">{account}</span>}
                    <span className="text-meta text-ink-faint">{t('chain.ran', { span: elapsed(session.created, session.updated) })}</span>
                    {here && <span className="text-meta font-medium text-ink">{t('chain.thisSession')}</span>}
                  </li>
                );
              })}
            </ul>
          </>
        )}
    </div>
  );
}
