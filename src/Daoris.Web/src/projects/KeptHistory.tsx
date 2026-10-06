import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline, Prose } from '../ui';
import { ClearAsk, KeptDoorButton, type KeptDoors } from '../work/ClearAsk';
import { clearOffered, type HistoryPlan, type HistoryUnitName, readingSaid } from '../work/history';
import { PageSection } from '../work/ViewMain';

/**
 * **A workspace's *Kept on this machine*** (HIST1e, D153; the history-clearing design §2.4, §6.1), the last section of its
 * Details: always the reading of what the home keeps of its finished work (its closed quests and asks, their sessions and
 * what they take on the disk), what a clear would take now, a count per reason that keeps the rest with its door where this
 * page holds one, the conversations only *Delete…* takes, and the home's own left-over files and log. Then *Clear history…*
 * while anything may go, which lists under it first, the counts by kind and every unit kept with its reason and door, and
 * on its second press sends exactly the units it listed.
 *
 * @remarks
 * **A molecule**: the plan arrives as a prop and every press goes out. The clear asks here, where it was pressed, rather than
 * under the page's header, since the press is in Details and not in the header (the platform language §4: a step in a task
 * happens where it was started). **Nothing clears by itself** (§7): the reading only makes the history's size a fact on the
 * screen. A host that answers no plan (one older than HIST1c) leaves the section out rather than a blank one.
 */
export function KeptHistory({ workspace, plan, reading = false, refusal = null, busy = false, doors, onClear }: {
  workspace: string;
  /** The workspace's plan with its reading; null while it is read, or where the host answers none. */
  plan: HistoryPlan | null;
  /** The plan is on its way. */
  reading?: boolean;
  /** The read was refused, in the sentence that says why. */
  refusal?: string | null;
  /** A clear on its way: the presses wait for it. */
  busy?: boolean;
  /** Where a kept reason's door leads: its Branches, *Sync now*, a quest's, an ask's or a session's page. */
  doors?: KeptDoors;
  /** The second press: exactly the units the list held; `done` once the driver has answered. */
  onClear: (units: readonly HistoryUnitName[], done: () => void) => void;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(false);
  if (!plan && !reading && !refusal) return null;
  const said = plan?.reading ? readingSaid(t, plan.reading) : null;
  const offered = clearOffered(plan);

  return (
    <PageSection title={t('projects.workspace.kept')}>
      {!plan ? (
        <Prose className="text-small"><Inline text={refusal ?? t('history.reading.reading')} /></Prose>
      ) : (
        <div className="grid gap-2">
          {said && (
            <>
              <Prose className="text-ink">{said.holds}</Prose>
              <Prose className="text-small">{said.takes}</Prose>
              {said.kept.length > 0 && (
                <ul aria-label={t('history.stays')} className="m-0 grid list-none gap-1 p-0">
                  {said.kept.map((kept) => (
                    <li key={kept.code} className="flex flex-wrap items-baseline gap-x-2 text-small text-ink-soft">
                      <span>{kept.text}</span>
                      <KeptDoorButton door={kept.door} doors={doors} />
                    </li>
                  ))}
                </ul>
              )}
              {said.conversations && <Prose className="text-small">{said.conversations}</Prose>}
              {said.leftOver && <Prose className="text-small text-ink-faint">{said.leftOver}</Prose>}
              <Prose className="text-small text-ink-faint">{said.log}</Prose>
            </>
          )}
          {asking ? (
            <ClearAsk
              className="mt-1"
              target={{ scope: 'workspace', id: workspace }}
              plan={plan}
              meanIt={(list) => t('projects.workspace.clearMeanIt', {
                // The records it sends; with none, what records already gone left and the intake's room, each an item it takes.
                count: list.units.length > 0 ? list.units.length : list.leftOver.count + (list.room > 0 ? 1 : 0),
              })}
              busy={busy}
              doors={doors}
              onClear={(units) => onClear(units, () => setAsking(false))}
              onCancel={() => setAsking(false)}
            />
          ) : offered && (
            <div className="mt-1">
              <Button disabled={busy} onClick={() => setAsking(true)}>{t('projects.workspace.clear')}</Button>
            </div>
          )}
        </div>
      )}
    </PageSection>
  );
}
