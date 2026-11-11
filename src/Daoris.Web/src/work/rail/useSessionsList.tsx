import { useState } from 'react';
import type { TFunction } from 'i18next';
import type { OpenGroup } from '../../app/opener';
import type { Notify } from '../../design/ui';
import type { useAttendedBox } from '../conversation/useAttendedBox';
import { keptSessionFilters, sessionFilters } from '../groups';
import { ListMore } from '../frame/ListPane';
import type { ListSpec, ViewLayout } from '../frame/ViewFrame';
import type { useFrameLayout } from '../frame/useFrameLayout';
import type { useAttendedSession } from '../head/useAttendedSession';
import type { useHeadAsks } from '../head/useHeadAsks';
import { SessionRail } from './SessionRail';

/**
 * Sessions' list: the rail, handed to the list pane as every view with a list hands its own. NEW is one control;
 * the list's own choices are by state, the default, or by repository, and whether archived is shown. Off Sessions the
 * view's own list is the spec.
 */
export function useSessionsList({
  t, notify, selected, elsewhere, viewLayout, group, onGroupBrought, session, frame, boxState, asks, chosen, attend, newConversationPressed,
}: {
  t: TFunction;
  notify: Notify;
  selected: string | null;
  elsewhere: boolean;
  viewLayout?: ViewLayout;
  group: OpenGroup | null;
  onGroupBrought?: () => void;
  session: ReturnType<typeof useAttendedSession>;
  frame: ReturnType<typeof useFrameLayout>;
  boxState: ReturnType<typeof useAttendedBox>;
  asks: ReturnType<typeof useHeadAsks>;
  chosen: () => void;
  attend: (id: string) => void;
  newConversationPressed: () => void;
}): ListSpec | undefined {
  const { sessions, sessionsResyncing, entries } = session;
  const { listed } = frame;
  const { taking, lastTurns } = boxState;
  const { archivingEnded, setArchivingEnded, doors } = asks;

  const sessionsFilters = sessionFilters(listed.pane('sessions').filters);
  // A go to a group of the list where the person keeps it by repository shows it by state:
  // for the go only and never remembered, it lasts until the person chooses an arrangement in the ⋯, which says what is
  // shown, or leaves Sessions. Set as the go arrives, not after, so the rail draws the group the render it looks for it.
  const [goByState, setGoByState] = useState(false);
  if (!elsewhere && (group === 'needs-you' || group === 'failed') && sessionsFilters.group === 'repository' && !goByState) {
    setGoByState(true);
  }
  if (elsewhere && goByState) setGoByState(false);
  const arrangement = goByState ? 'state' : sessionsFilters.group;
  const sessionsList = (): ListSpec => ({
    view: 'sessions',
    name: t('work.rail.label'),
    labels: { open: t('work.rail.open'), close: t('work.rail.close'), resize: t('work.rail.resize') },
    make: { label: t('work.start.title'), onMake: newConversationPressed },
    // The list's ⋯: how it is grouped, a choice of two, and whether archived sessions
    // are shown, both remembered; then, below a rule, *Archive what ended…*, which lists first.
    more: (
      <ListMore
        label={t('work.list.more')}
        choice={{
          label: t('work.list.groupBy'),
          value: arrangement,
          options: [
            { value: 'state', label: t('work.list.byState') },
            { value: 'repository', label: t('work.list.byRepository') },
            // Each ask its entries: offered where the reader answers entries, since it groups by them.
            ...(entries ? [{ value: 'ask', label: t('work.list.byAsk') }] : []),
          ],
          onChoose: (value) => {
            setGoByState(false);
            listed.setFilters('sessions', keptSessionFilters({
              ...sessionsFilters, group: value === 'repository' ? 'repository' : value === 'ask' ? 'ask' : 'state',
            }));
          },
        }}
        items={[
          { id: 'archived', label: t('work.list.showArchived'), checked: sessionsFilters.archived },
          { id: 'archiveEnded', label: t('work.list.archiveEnded'), rule: true },
        ]}
        onChoose={(id) => {
          if (id === 'archiveEnded') setArchivingEnded(true);
          else if (id === 'archived') {
            listed.setFilters('sessions', keptSessionFilters({ ...sessionsFilters, archived: !sessionsFilters.archived }));
          }
        }}
      />
    ),
    loading: sessions.isPending,
    resyncing: sessionsResyncing,
    chosen: selected,
    // What waits on the person, then what runs, in the open list's order.
    strip: (
      <SessionRail
        selected={selected}
        onSelect={(id) => { chosen(); attend(id); }}
        notify={notify}
        compact
        taking={taking}
        arrangement={arrangement}
        archived={sessionsFilters.archived}
        // The strip draws no group and is handed none: a go lays the list over beside it, and the list there brings the
        // group. Handed one, it answered first and let the go go before the list laid over had looked.
      />
    ),
    body: (
      <SessionRail
        selected={selected}
        onSelect={(id) => { chosen(); attend(id); }}
        notify={notify}
        taking={taking}
        lastTurns={lastTurns}
        arrangement={arrangement}
        archived={sessionsFilters.archived}
        archiveEnded={archivingEnded}
        onArchiveEnded={() => setArchivingEnded(false)}
        group={group}
        onGroupBrought={onGroupBrought}
        // A row's acts that are this frame's: answering, asking to stop or to delete, reviewing, a terminal there.
        doors={doors}
      />
    ),
  });
  return elsewhere ? viewLayout?.list : sessionsList();
}
