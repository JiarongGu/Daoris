import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline, Prose } from '../ui';
import {
  clearEnd, clearLead, type ClearList, clearList, goingSaid, type HistoryPlan, type HistoryReason, type HistoryTarget,
  type HistoryUnit, type HistoryUnitName, type KeptDoor, keptDoor, reasonSaid,
} from './history';
import { type Answered, InlineConfirm } from './InlineConfirm';

/**
 * The doors a kept unit's line may offer (design §5 step 1), each only where its page holds it: the quest's page for a
 * conflict or an acceptance, the ask's page, the session's page for *Stop…*, the workspace's Branches for *Clean up…*, and
 * *Sync now* for moves the remote has not had.
 */
export type KeptDoors = {
  quest?: (id: string) => void;
  ask?: (id: string) => void;
  session?: (id: string) => void;
  branches?: () => void;
  sync?: () => void;
};

/** A kept unit's door as a button, where its page was handed one; nothing otherwise. */
export function KeptDoorButton({ door, doors }: { door: KeptDoor | null; doors?: KeptDoors }) {
  const { t } = useTranslation();
  if (!door || !doors) return null;
  const [label, press] = door.to === 'quest' ? [t('history.door.quest', { id: door.id }), doors.quest && (() => doors.quest!(door.id))]
    : door.to === 'ask' ? [t('quests.detail.openAsk', { id: door.id }), doors.ask && (() => doors.ask!(door.id))]
      : door.to === 'session' ? [t('work.open'), doors.session && (() => doors.session!(door.id))]
        : door.to === 'branches' ? [t('history.door.branches'), doors.branches]
          : [t('projects.workspace.page.sync'), doors.sync];
  return press ? <Button variant="ghost" className="justify-self-start px-1.5 text-small" onClick={press}>{label}</Button> : null;
}

/** A named list of the clear's: its heading, and the list it names. */
function Listed({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="min-w-0">
      <h3 className="m-0 mb-1.5 text-small font-semibold text-ink-faint">{title}</h3>
      <ul aria-label={title} className="m-0 grid list-none gap-2 p-0">{children}</ul>
    </div>
  );
}

/** One unit kept: its name, its reason's sentence in the reader's language, and the door that frees it. */
function Staying({ unit, doors }: { unit: HistoryUnit; doors?: KeptDoors }) {
  const { t } = useTranslation();
  const name = t(`history.unit.${unit.kind}`, { id: unit.id });
  return (
    <li aria-label={name} className="grid min-w-0 gap-0.5">
      <span className="min-w-0 wrap-anywhere text-body text-ink">{name}</span>
      {unit.keep && <span className="wrap-anywhere text-small text-ink-soft"><Inline text={reasonSaid(t, unit.keep)} /></span>}
      {unit.keep && <KeptDoorButton door={keptDoor(unit.keep)} doors={doors} />}
    </li>
  );
}

/** A piece kept while its unit goes (a teammate's failed session): its sentence names it. */
function KeptPiece({ reason }: { reason: HistoryReason }) {
  const { t } = useTranslation();
  return <li className="wrap-anywhere text-small text-ink-soft"><Inline text={reasonSaid(t, reason)} /></li>;
}

/**
 * **A clear's first press** (HIST1e, D153; the history-clearing design §5, §6.1): it says what the clear would take from this
 * machine (a quest's work, its failed sessions, an ask's work, or for a workspace the counts by kind with the home's
 * left-over files and the intake's room), that nothing brings it back or, where a quest is forgotten here, that the remote
 * keeps the team's copy; and every unit kept, with its reason's sentence and the door that frees it. Then the clear's
 * danger press and *Never mind*.
 *
 * @remarks
 * **A molecule**: the plan arrives as a prop and is held from when the list opened, since the reader answers again on every
 * look and the second press sends exactly the units the first listed (§5 step 2); the driver judges each again as it goes.
 * A list that finds nothing to take says so and offers only *Close*, never a press that clears nothing (D48 §6). A reason is
 * the refusal's own catalogue sentence for its code and variant, read in the reader's language, never the service's words.
 *
 * **It is the one inline confirmation** (UXFIX2): what goes and what stays take the focus on opening and describe the move,
 * so a keyboard reaches it having heard them; the press keeps the list open until the clear answers; a refusal is said in it.
 */
export function ClearAsk({ target, plan, meanIt, busy = false, doors, onClear, onClose, className }: {
  target: HistoryTarget;
  /** The plan as the first press read it; held from the first render. */
  plan: HistoryPlan;
  /** The move's name: *Clear quest*, *Clear ask*, or *Clear N*, counted from the list as it was held. */
  meanIt: string | ((list: ClearList) => string);
  /** A clear on its way: the presses wait for it. */
  busy?: boolean;
  /** Where a kept unit's door leads; absent, none is drawn. */
  doors?: KeptDoors;
  /** The second press, with exactly the units the list held, told how the clear ended. */
  onClear: (units: readonly HistoryUnitName[], answered: Answered) => void;
  /** Put down, or the clear landed. */
  onClose: () => void;
  className?: string;
}) {
  const { t } = useTranslation();
  const [held] = useState(plan);
  const list = clearList(held);
  const going = target.scope === 'workspace' ? goingSaid(t, list) : [];
  const stays = list.staying.length > 0 || list.kept.length > 0;
  const said = list.takes ? t('history.join', { first: clearLead(t, target, list), second: clearEnd(t, list) }) : t('history.nothing');

  return (
    <InlineConfirm
      block
      className={className}
      label={t('history.title')}
      says={(
        <>
          <Prose className="text-small"><Inline text={said} /></Prose>
          {(going.length > 0 || stays) && (
            <div className="grid gap-4 @min-[44rem]/main:grid-cols-2">
              {going.length > 0 && (
                <Listed title={t('history.goes')}>
                  {going.map((line) => <li key={line} className="wrap-anywhere text-body text-ink">{line}</li>)}
                </Listed>
              )}
              {stays && (
                <Listed title={t('history.stays')}>
                  {list.staying.map((unit) => <Staying key={`${unit.kind}:${unit.id}`} unit={unit} doors={doors} />)}
                  {list.kept.map((reason, i) => <KeptPiece key={`${reason.code}:${reason.session ?? i}`} reason={reason} />)}
                </Listed>
              )}
            </div>
          )}
        </>
      )}
      // Nothing to take: only *Close*, never a press that clears nothing.
      meanIt={list.takes ? (typeof meanIt === 'function' ? meanIt(list) : meanIt) : undefined}
      busy={busy}
      onConfirm={(answered) => onClear(list.units, answered)}
      onClose={onClose}
    />
  );
}
