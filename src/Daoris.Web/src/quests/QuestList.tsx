import { useTranslation } from 'react-i18next';
import type { Ask, Quest, Session } from '../data/api';
import { AskRow } from '../asks/AskRow';
import { TeammateAskRow } from '../asks/TeammateAskRow';
import type { ElsewhereAsk } from '../work/channel';
import { CarriedCount } from '../compose/carry';
import { ago, sittingDays } from '../locales/format';
import { askItem } from '../app/opener';
import { type Consideration, sittingSentence } from '../data/signals';
import { Button, Inline, Pill, QUEST_TONE, SESSION_ACTIVE, SESSION_TONE } from '../design/ui';
import { cn } from '../lib/cn';
import { contextOffer } from '../menus/press';
import { questName } from '../work/identity';
import { groupHeading, ListGroup, ListRowDoor } from '../work/frame/ListPane';
import { questGroups, shownStatus } from './records';

/** What a quest's row says beside the quest itself. */
export type QuestRowFacts = {
  quest: Quest;
  /** Its driven session's state, where one works it now: a live session marks its quest. */
  session?: Session['state'] | null;
  /** The question its taker asked and waits on, while it is unanswered (D79). */
  waits?: string | null;
  /** Why this machine's driver leaves it waiting (USE1), in the driver's words. */
  sitting?: Consideration | null;
};

/** What an ask's row says beside the ask: its intake session's state, where the page holds that record. */
export type AskRowFacts = {
  ask: Ask;
  intake?: Session['state'] | null;
  /** What is unread on its channel (SESSONE2c), where this machine's driver answers one. */
  unread?: number;
};

/**
 * **Quests' list** (FRAME1d, D118 §2): the asks first, since they are where quests come from and a proposal waits on
 * a person (INT4a), then the quests by where they are in their life, each group with its count and absent with none.
 * A row is for reading and choosing; its record opens in the main area, where the acting is.
 *
 * @remarks
 * **A molecule**: the rows arrive with what each says, and every press goes out. Each row is a row of its list
 * (`data-list-row`), so ↑, ↓, Home and End move along it, and Enter opens it.
 *
 * - **Status leads** (platform language §4): the pill, then the title, then the route and how long; the secondary
 *   marks sit at the right of the pill's line, as exceptions rather than identity.
 * - **A held repository's quests say why they wait** (USE1), and the hold is lifted from the row, beside its door and
 *   never inside it, since a button holds no button.
 * - **A receiver filter is said**, since the ⋯ that sets it is a press away and a list that silently holds fewer
 *   quests reads as a family that owes less.
 */
export function QuestList({
  asks, teammates = [], quests, closed, filteredTo = null, empty, unanswered = null, chosen, resuming = false, onChoose, onResume,
}: {
  asks: AskRowFacts[];
  /** The asks the mirrored channel messages name that this machine does not hold (SESSONE2h), a group of their own after the asks. */
  teammates?: readonly ElsewhereAsk[];
  quests: QuestRowFacts[];
  /** Closed quests are shown, in their own group. */
  closed: boolean;
  /** The receiver the quests are filtered to, said at the list's head. */
  filteredTo?: string | null;
  /** What no quest says, under the asks; the whole list empty is the list pane's own empty state. */
  empty: string;
  /** The sentence for a list that has never had an answer, said in place rather than left blank (D118 §3h). */
  unanswered?: string | null;
  /** The list's chosen item: a quest's id, or `ask:<id>`. */
  chosen: string | null;
  /** A held repository is being resumed. */
  resuming?: boolean;
  onChoose: (item: string) => void;
  /** Lift a repository's hold, where the driver is (USE1). */
  onResume?: (repository: string) => void;
}) {
  const { t } = useTranslation();

  if (unanswered) {
    return <p className="m-0 px-3 py-3 text-small text-ink-soft"><Inline text={unanswered} /></p>;
  }

  const facts = new Map(quests.map((row) => [row.quest.id, row]));
  const groups = questGroups(quests.map((row) => row.quest), closed);
  const { waiting, becoming } = placeAsks(asks, new Set(groups.flatMap((group) => group.quests.map((quest) => quest.id))));

  return (
    <div>
      {filteredTo && (
        <p className="m-0 truncate px-2.5 pb-0.5 pt-2 text-meta text-ink-faint">{t('quests.list.filtered', { repository: filteredTo })}</p>
      )}
      {/* Each heading's id is what a door into its group brings into view (ENTRY1b). */}
      {waiting.length > 0 && (
        <ListGroup id={groupHeading('quests', 'asks')} title={t('asks.group', { count: waiting.length })}>
          {waiting.map(({ ask, intake, unread }) => (
            <AskRow key={ask.id} ask={ask} intake={intake ?? null} unread={unread} chosen={chosen === askItem(ask.id)} onOpen={() => onChoose(askItem(ask.id))} />
          ))}
        </ListGroup>
      )}
      {teammates.length > 0 && (
        /* The asks other machines hold, which the sync mirrored the channel of (SESSONE2h): after this machine's own asks. */
        <ListGroup id={groupHeading('quests', 'teammates')} title={t('asks.elsewhere.group', { count: teammates.length })}>
          {teammates.map((ask) => (
            <TeammateAskRow key={ask.id} ask={ask} chosen={chosen === askItem(ask.id)} onOpen={() => onChoose(askItem(ask.id))} />
          ))}
        </ListGroup>
      )}
      {groups.map(({ group, quests: rows }) => rows.length > 0 && (
        <ListGroup key={group} id={groupHeading('quests', group)} title={t(`quests.groups.${group}`, { count: rows.length })}>
          {rows.map((quest) => (
            <QuestRow
              key={quest.id}
              facts={facts.get(quest.id)!}
              ask={becoming.get(quest.id) ?? null}
              chosen={chosen === quest.id}
              resuming={resuming}
              onChoose={onChoose}
              onResume={onResume}
            />
          ))}
        </ListGroup>
      ))}
      {quests.length === 0 && waiting.length > 0 && (
        <p className="m-0 border-t border-line px-2.5 py-2.5 text-small text-ink-soft">{empty}</p>
      )}
    </div>
  );
}

/**
 * Which asks keep a row of their own and which are their quest's row (QUESTLISTS1, D185): an ask that became exactly one
 * quest, which this list shows, is that one piece of work, so the quest's row names it and the ask rides its route line.
 * An ask keeps its row while it waits on something of its own: open, proposed, an intake parked asking the person, several
 * quests, or a quest this list does not show (filtered out, or closed and not included), so no work vanishes from the list.
 */
export function placeAsks(asks: readonly AskRowFacts[], shown: ReadonlySet<string>) {
  const waiting: AskRowFacts[] = [];
  const becoming = new Map<string, Ask>();
  for (const row of asks) {
    const { ask } = row;
    const [only] = ask.quests;
    const merged = (ask.state === 'Published' || ask.state === 'Done') && ask.quests.length === 1
      && row.intake !== 'awaiting-person' && shown.has(only) && !becoming.has(only);
    if (merged) becoming.set(only, ask);
    else waiting.push(row);
  }
  return { waiting, becoming };
}

/** One quest as a row of the list: its pill and marks, its title, its route and how long, and why it sits. */
function QuestRow({ facts, ask, chosen, resuming, onChoose, onResume }: {
  facts: QuestRowFacts;
  /** The ask this quest is the only quest of: it names the row and rides the route line. */
  ask: Ask | null;
  chosen: boolean;
  resuming: boolean;
  onChoose: (item: string) => void;
  onResume?: (repository: string) => void;
}) {
  const { t } = useTranslation();
  const { quest, session, waits, sitting } = facts;
  const sat = sittingDays(quest.filed);
  // The ask's short title names the work where it gave one; else the quest's own name, as before.
  const name = ask?.short?.trim() ? ask.short.trim() : questName(quest);
  // A done a departure holds still waits on the person (DRIFT1d2), so it reads as live, never dimmed as ended.
  const ended = (quest.status === 'Done' || quest.status === 'Declined') && quest.held !== true;
  // Only a hold is the person's to lift here; any other reason is said, and offers nothing.
  const resume = sitting?.verdict === 'Held' ? onResume : undefined;
  // A row offers what it does on a right-click (CTX1, D138 §4): opening it, its hold's resume, and its id; what is done to
  // the quest is its page's.
  const menu = {
    label: name,
    acts: [
      { id: 'open', label: t('contextMenu.act.open'), onSelect: () => onChoose(quest.id) },
      // The ask has no row of its own now (QUESTLISTS1), so its page, where it is paused or abandoned, opens from here.
      ...(ask ? [{ id: 'open-ask', label: t('contextMenu.act.openAsk'), onSelect: () => onChoose(askItem(ask.id)) }] : []),
      ...(resume
        ? [{ id: 'resume', label: t('quests.card.resume', { repository: quest.to }), icon: 'resume' as const, disabled: resuming, onSelect: () => resume(quest.to) }]
        : []),
      { id: 'copy', label: t('contextMenu.act.copyQuest'), icon: 'copy' as const, copy: quest.id },
    ],
  };

  return (
    <li data-list-row="" {...contextOffer(menu)}>
      <ListRowDoor chosen={chosen} dimmed={ended} onPress={() => onChoose(quest.id)}>
        <span className="flex items-baseline gap-2">
          {/* A closure graph's quest at its submission reads submitted (FABRIC-CLOSE1d, §E5); every other its status. */}
          <Pill tone={QUEST_TONE[quest.status]} title={t(`statusHint.${shownStatus(quest)}`)}>{t(`status.${shownStatus(quest)}`)}</Pill>
          <span className="ml-auto flex min-w-0 shrink items-baseline justify-end gap-1.5">
            <CarriedCount links={quest.links?.length ?? 0} files={quest.attachments?.length ?? 0} />
            {/* A week of silence is the signal this view exists to surface. */}
            {quest.status === 'Open' && sat >= 7 && <Pill tone="declined">{t('quests.card.sat', { days: sat })}</Pill>}
            {/* Taken and waiting on another repository's answer (D79): not stuck, and nothing for the person to
                do. The waiting tone, as `awaiting-person` wears it. */}
            {waits && <Pill tone="open" title={t('quests.card.waitsHint')}>{t('quests.card.waits', { id: waits })}</Pill>}
            {/* A departure holds it for the person's yes (DRIFT1d2): what follows waits on them, so open's hue, theirs. A set-up
                step its review holds waits for their review instead (REVIEWENV1g), which no yes lifts, and a step its source's
                reopen holds for their go-on or abandon (FABRIC-CLOSE1c3). */}
            {quest.held && (quest.hold === 'unreviewed'
              ? <Pill tone="open" title={t('review.pillHint')}>{t('review.pill')}</Pill>
              : quest.hold === 'source-reopened'
                ? <Pill tone="open" title={t('quests.card.reopenedHint')}>{t('quests.card.reopened')}</Pill>
                : <Pill tone="open" title={t('quests.card.heldHint')}>{t('quests.card.held')}</Pill>)}
            {/* A live driven session marks its quest; finished ones live in its record. */}
            {session && SESSION_ACTIVE.has(session) && (
              <Pill tone={SESSION_TONE[session]} title={t('quests.session.hint')}>{t(`sessionState.${session}`)}</Pill>
            )}
          </span>
        </span>
        {/* Its name (SESSUX1j), the short title where there is one, whole title on its tip. */}
        <span title={quest.title} className={cn('mt-0.5 block truncate text-body', ended ? 'text-ink-soft' : 'text-ink')}>{name}</span>
        <span className="block truncate font-mono text-meta text-ink-faint">
          <span className="text-accent">{quest.from} → {quest.to}</span>
          {/* The lanes of the repository it asks (D115 §2.2), beside the repository: `to` stays one. */}
          {quest.lanes?.length ? (
            <>
              {' · '}
              <span className="text-ink-soft" title={t('quests.card.lanesHint', { repository: quest.to })}>
                {t('quests.card.lanes', { count: quest.lanes.length, lanes: quest.lanes.join(' + ') })}
              </span>
            </>
          ) : null}
          {' '}· {t('quests.card.filed', { ago: ago(quest.filed) })}
          {quest.updated !== quest.filed && <> · {t('quests.card.moved', { ago: ago(quest.updated) })}</>}
          {/* A step of a chain says which quest's close published it (D65 §4). */}
          {quest.parent && <> · {t('quests.card.follows', { id: quest.parent })}</>}
          {/* The ask it came from, the one piece of work's other name (QUESTLISTS1), unless its route already names it: a quest
              an ask published comes `from` that ask, and the row would say it twice. */}
          {ask && !quest.from.includes(ask.id.slice(0, 6)) && (
            <> · <span title={t('quests.card.fromAskHint')}>{t('quests.card.fromAsk', { id: ask.id.slice(0, 6) })}</span></>
          )}
        </span>
        {/* Why this machine's driver leaves it waiting (USE1): its record said so, the row did not, and a quest to
            a held repository read as a request that would not start. */}
        {sitting && (
          <span className="mt-0.5 block truncate text-small text-ink-soft"><Inline text={sittingSentence(sitting)} /></span>
        )}
      </ListRowDoor>
      {/* The hold is the person's own and one press lifts it, here where it is read: beside the row's door. */}
      {resume && (
        <div className="pb-1.5 pl-[0.8125rem] pr-2.5">
          <Button
            variant="ghost"
            className="px-1.5 py-0.5 text-small"
            title={t('quests.card.resumeTip', { repository: quest.to })}
            disabled={resuming}
            onClick={() => resume(quest.to)}
          >
            {t('quests.card.resume', { repository: quest.to })}
          </Button>
        </div>
      )}
    </li>
  );
}
