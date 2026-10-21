import { type ReactNode, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon, type Notify } from '../design/ui';
import { storedHere, storeHere } from '../lib/stored';
import {
  type Confirmation, type ConfirmationAnswer, confirmationAnchor, Confirmations, PressArea, type PressNames,
} from '../person/Confirmations';
import { NothingNeedsYou } from '../work/attention/AttentionList';
import type { Notice } from '../bridge/notices';
import { DoneAndFailed } from './DoneAndFailed';

/** A group of *Notifications* a door can name (§2.2): the presses waiting for confirmation, *What needs you*, and *Done and failed*. */
export type NotificationsGroup = 'presses' | 'needs' | 'done';

const GROUPS: readonly NotificationsGroup[] = ['presses', 'needs', 'done'];

/** What a door asked *Notifications* to show: a group, or a press's card; counted, so the same ask twice is two. */
export type NotificationsAt = { group?: NotificationsGroup; press?: string; n: number };

/** Which groups this window folded (§2.4: view state, the window's own, in its session storage as its scope is). */
export const FOLDED = 'daoris.notifications.folded';

function foldedHere(): Set<NotificationsGroup> {
  try {
    const kept: unknown = JSON.parse(storedHere(FOLDED) ?? '[]');
    return new Set(Array.isArray(kept) ? kept.filter((each): each is NotificationsGroup => GROUPS.includes(each as NotificationsGroup)) : []);
  } catch {
    return new Set();
  }
}

/**
 * *Notifications* (NOTIFY2b, D196 point 3, the notification design §2): everything that comes to the person, in one view of the
 * frame's regions, the right side bar by default or the panel where the person moved it.
 *
 * @remarks
 * **Its groups, in one column** (§2.2): *Waiting for your confirmation*, each press of either source as its whole card, read from the
 * confirmation channel alone (never a row's copy); then *What needs you*, today's rows in their groups and order with their acts and
 * doors, its `confirmation` row drawn as its card above. Each is headed by its name and its count and folds by its head, the fold
 * this window's own; a group holding nothing is not drawn, and with nothing in any, *Nothing needs you* and the sessions working.
 * Then *Done and failed* (NOTIFY2f): what ended, failed or went unanswered, newest first, each read once it is shown here in a
 * window in front (`DoneAndFailed`). *Messages* joins with the rows that produce it (2g).
 *
 * **Its scroll area is the presses' area** (§3.4): a card is *shown* when its decision region is in view here, which a folded group,
 * a tab not shown or a closed region never are, since none of them draws the card.
 *
 * A molecule over a slot: the application hands it the presses and their answers, and *What needs you* as the band it draws, so it
 * presses no hook itself.
 */
export function NotificationsView({
  presses, needs, working, attention, onAnswer, notify, onShown, names, at = null, onVisible, notices = [], onRead, onOpenNotice, onDismiss,
}: {
  presses: Confirmation[];
  /** How many rows *What needs you* holds but the presses' own; null while not judged, which draws the band's own skeleton. */
  needs: number | null;
  /** The sessions working now, which the line says when nothing needs the person. */
  working: number;
  /** *What needs you*'s rows, as the band draws them in the view, with no card of its own. */
  attention: ReactNode;
  onAnswer: (waiting: Confirmation, confirm: boolean) => Promise<ConfirmationAnswer>;
  notify?: Notify;
  onShown?: (waiting: Confirmation) => void;
  names?: PressNames;
  /** What a door asked to show: its group unfolded and brought into view, or a press's card. */
  at?: NotificationsAt | null;
  /** Told whether the view is drawn: a press's toast stays until the view opens (§3.3). */
  onVisible?: (shown: boolean) => void;
  /** *Done and failed*, the window's notices newest first (NOTIFY2f). */
  notices?: Notice[];
  /** The notices shown here in a window in front, *Opened*, or named by *Mark all read*, read. */
  onRead?: (ids: string[]) => void;
  /** A notice's door: its session's page. */
  onOpenNotice?: (notice: Notice) => void;
  onDismiss?: (notice: Notice) => void;
}) {
  const { t } = useTranslation();
  const area = useRef<HTMLDivElement>(null);
  const [folded, setFolded] = useState(foldedHere);
  const fold = (group: NotificationsGroup) => setFolded((was) => {
    const next = new Set(was);
    if (next.has(group)) next.delete(group);
    else next.add(group);
    storeHere(FOLDED, next.size > 0 ? JSON.stringify([...next]) : null);
    return next;
  });

  const told = useRef(onVisible);
  useEffect(() => { told.current = onVisible; });
  useEffect(() => {
    told.current?.(true);
    return () => told.current?.(false);
  }, []);

  // A door's ask: its group unfolded, then brought into view once drawn. The scroll is the person's own otherwise (§3.4).
  const [asked, setAsked] = useState<NotificationsAt | null>(null);
  if (at && at.n !== asked?.n) {
    setAsked(at);
    const group = at.press ? 'presses' : at.group;
    if (group && folded.has(group)) {
      const next = new Set(folded);
      next.delete(group);
      storeHere(FOLDED, next.size > 0 ? JSON.stringify([...next]) : null);
      setFolded(next);
    }
  }
  useEffect(() => {
    if (!asked) return;
    const target = asked.press
      ? document.getElementById(confirmationAnchor(asked.press))
      : asked.group ? document.getElementById(groupAnchor(asked.group)) : null;
    target?.scrollIntoView?.({ block: 'nearest' });
  }, [asked]);

  // Nothing waiting on the person: said above what ended, which is no wait.
  const nothing = presses.length === 0 && needs === 0;
  const unread = notices.filter((notice) => !notice.read).length;
  return (
    <div ref={area} data-notifications="" className="flex min-h-0 flex-1 flex-col gap-3 overflow-y-auto p-3">
      <PressArea.Provider value={area}>
        {presses.length > 0 && (
          <Group id="presses" title={t('person.label')} count={presses.length} folded={folded.has('presses')} onFold={fold} warn>
            <Confirmations waiting={presses} onAnswer={onAnswer} notify={notify} onShown={onShown} names={names} />
          </Group>
        )}
        {needs !== 0 && (
          <Group id="needs" title={t('work.attention.title')} count={needs} folded={folded.has('needs')} onFold={fold}>
            {attention}
          </Group>
        )}
        {nothing && <NothingNeedsYou working={working} />}
        {notices.length > 0 && (
          <Group id="done" title={t('notifications.done.title')} count={unread > 0 ? unread : null}folded={folded.has('done')} onFold={fold}>
            <DoneAndFailed notices={notices} onRead={onRead ?? (() => {})} onOpen={onOpenNotice} onDismiss={onDismiss ?? (() => {})} />
          </Group>
        )}
      </PressArea.Provider>
    </div>
  );
}

/** The element a group is drawn in, which a door brings into view. */
const groupAnchor = (group: NotificationsGroup) => `notifications-${group}`;

/** A group of the view: its head a fold, its name and its count, then what it holds while unfolded (§2.2). */
function Group({ id, title, count, folded, onFold, warn = false, children }: {
  id: NotificationsGroup;
  title: string;
  /** How many it holds; null while not known. */
  count: number | null;
  folded: boolean;
  onFold: (group: NotificationsGroup) => void;
  /** The presses' group wears the warning hue its cards wear (D185 R2: a wait with a clock). */
  warn?: boolean;
  children: ReactNode;
}) {
  return (
    <section id={groupAnchor(id)} aria-label={title} className="grid gap-2">
      <h2 className="m-0">
        <button
          type="button"
          aria-expanded={!folded}
          onClick={() => onFold(id)}
          className="flex w-full items-center gap-1.5 border-0 bg-transparent p-0 text-left text-small font-semibold text-ink"
        >
          <Icon name={folded ? 'chevronRight' : 'chevronDown'} size={13} className="text-ink-faint" />
          <span className="min-w-0 flex-1">{title}</span>
          {count !== null && (
            <span className={warn ? 'font-mono text-meta tabular-nums text-warn' : 'font-mono text-meta tabular-nums text-ink-open'}>{count}</span>
          )}
        </button>
      </h2>
      {!folded && children}
    </section>
  );
}
