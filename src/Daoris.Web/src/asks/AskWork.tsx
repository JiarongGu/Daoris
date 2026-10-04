import { useTranslation } from 'react-i18next';
import { type Consideration, sittingBecause, sittingSentence } from '../signals';
import { Inline, Pill, QUEST_TONE, SESSION_TONE, type ShownState } from '../ui';
import type { WorkPlanSession } from '../work/pausing';
import { whyItSits, type WorkItem } from './workTree';

/** A door into another page, in the accent, as the ask's other doors are. */
const DOOR = 'cursor-pointer border-0 bg-transparent p-0 text-left text-accent underline-offset-2 hover:underline';

/** The doors and facts every row of the work reads, handed down unchanged. */
type Shared = {
  ask: string;
  considered: readonly Consideration[];
  questTitles: Record<string, string>;
  onOpenQuest: (id: string) => void;
  onAttend?: (session: string) => void;
};

/**
 * **An ask's work, as its page lists it** (PAUSE1h, D132 §7.1): each quest of `WORK_PLAN`'s answer with its state, its route
 * and why it sits; the sessions that worked on it, as doors into Sessions; and under it the questions those sessions asked of
 * other repositories, each listed the same way. It leads by saying that this is what a pause or an abandon reaches, so the
 * person sees it before pressing either.
 *
 * @remarks
 * **A molecule**: the work arrives as a tree (`workTree`) and the driver's last look as the tick's `considered`, so every state
 * is reached by its props. Why a quest sits is one rule (`whyItSits`): taken where a pause does not reach it, a pause, or the
 * driver's sentence in the reader's language (`sittingSentence`), its backticks set as code.
 *
 * **A door opens something, or it is not a door** (platform language §4): a quest's title opens its page only where the page
 * holds it, and a session is a door only where Sessions is; elsewhere each is named. A session's word is its record's state;
 * a teammate's says the machine it runs on, which nothing this machine sends reaches (D47 §4).
 */
export function AskWork({ items, ...shared }: Shared & { items: readonly WorkItem[] }) {
  const { t } = useTranslation();
  return (
    <>
      <p className="m-0 mb-2.5 text-small text-ink-soft">{t('asks.work.lead')}</p>
      <ul className="m-0 grid list-none gap-3 p-0">
        {items.map((item) => <WorkRow key={item.quest.quest} item={item} {...shared} />)}
      </ul>
    </>
  );
}

/** One quest of the work: its line, why it sits, its sessions, and the questions they asked, each a row of its own. */
function WorkRow({ item, ...shared }: Shared & { item: WorkItem }) {
  const { t } = useTranslation();
  const { quest, sessions, questions } = item;
  const { ask, considered, questTitles, onOpenQuest, onAttend } = shared;
  const why = whyItSits(item, sittingBecause(considered, quest.quest), ask);
  const sentence = why === null ? null : 'line' in why ? t(why.line.key, why.line.values) : sittingSentence(why.sitting);
  const asked = t('asks.work.asked', { count: questions.length });

  return (
    <li className="min-w-0">
      <p className="m-0 flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-1">
        <Pill tone={QUEST_TONE[quest.status]}>{t(`status.${quest.status}`)}</Pill>
        <span className="shrink-0 font-mono text-meta text-ink-faint">#{quest.quest.slice(0, 6)}</span>
        {/* A door only onto a quest the page holds: a question to a repository in another workspace is named, not opened. */}
        {questTitles[quest.quest] ? (
          // Named as every list names it (SESSUX1j), whole on its tip.
          <button
            type="button"
            title={quest.title}
            onClick={() => onOpenQuest(quest.quest)}
            className={`${DOOR} min-w-0 wrap-anywhere text-body`}
          >
            {questTitles[quest.quest]}
          </button>
        ) : (
          <span className="min-w-0 wrap-anywhere text-body text-ink">{quest.title}</span>
        )}
        <span className="text-meta text-ink-faint">→ {quest.to}</span>
      </p>

      {sentence && <p className="m-0 mt-0.5 wrap-anywhere text-small text-ink-soft"><Inline text={sentence} /></p>}

      {sessions.length > 0 && (
        <p className="m-0 mt-1 flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1">
          <span className="text-meta text-ink-faint">{t('asks.work.sessions')}</span>
          {sessions.map((session) => <SessionDoor key={session.session} session={session} onAttend={onAttend} />)}
        </p>
      )}

      {questions.length > 0 && (
        <div className="mt-1.5 min-w-0">
          <span className="text-meta text-ink-faint">{asked}</span>
          <ul aria-label={asked} className="m-0 mt-1 grid list-none gap-2.5 border-l border-line py-0.5 pl-3">
            {questions.map((question) => <WorkRow key={question.quest.quest} item={question} {...shared} />)}
          </ul>
        </div>
      )}
    </li>
  );
}

/** One session of a quest: its record's state, its id as the door into Sessions, and where a teammate's runs. */
function SessionDoor({ session, onAttend }: { session: WorkPlanSession; onAttend?: (session: string) => void }) {
  const { t } = useTranslation();
  // A state this page has no word for is said as the record wrote it, quiet.
  const tone = SESSION_TONE[session.state as ShownState] ?? 'neutral';
  return (
    <span className="inline-flex min-w-0 flex-wrap items-center gap-x-1.5 gap-y-0.5">
      <Pill tone={tone}>{t(`sessionState.${session.state}`, { defaultValue: session.state })}</Pill>
      {onAttend ? (
        <button
          type="button"
          aria-label={t('asks.work.openSession', { id: session.session })}
          onClick={() => onAttend(session.session)}
          className={`${DOOR} min-w-0 wrap-anywhere font-mono text-meta`}
        >
          {session.session}
        </button>
      ) : (
        <span className="min-w-0 wrap-anywhere font-mono text-meta text-ink-soft">{session.session}</span>
      )}
      {session.teammate && (
        <span className="text-meta text-ink-faint">
          {session.machine ? t('asks.work.teammate', { machine: session.machine }) : t('asks.work.teammateUnnamed')}
        </span>
      )}
    </span>
  );
}
