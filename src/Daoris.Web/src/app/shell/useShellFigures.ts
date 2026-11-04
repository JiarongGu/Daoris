import { useMemo } from 'react';
import type { useScope } from '../../data/scope';
import { useQuests, useRegistry, useSessions, useSyncStanding } from '../../data/queries';
import { type NotificationsAt } from '../../notifications/NotificationsView';
import { usePressAnnouncements } from '../../notifications/announce';
import { type MessageNames } from '../../notifications/Messages';
import { askName, sessionTitle } from '../../work/identity';
import { bellCount, needsListed, placeCounts } from '../../work/attention/attention';
import { failure } from '../../design/ui';
import { useNoticeWords, useSyncNow, useTaskbarCount } from '../../bridge/shell';
import { type Confirmation, type PressNames, routeKey } from '../../person/Confirmations';
import { browserDrivers } from '../../work/strip/browserDrivers';
import { type DriverPresence } from '../../work/frame/frame';
import { type UseQueryResult } from '@tanstack/react-query';
import { type RepositoryResponse, type QuestResponse, type AskResponse } from '../../data/generated/service';
import { type DriverState } from '../../bridge/driver';
import { type AttentionRead } from '../../bridge/attention';
import { type TFunction } from 'i18next';
import { type Session } from '../../data/api';
import { type MachineWiring } from '../../bridge/repositories/remotes';
import { type Notify } from '../../design/atoms/Toasts';

export function useShellFigures({ repositories, driver, attached, attention, presses, t, outstanding, scope, workspaces, askRecords, serviceRead, notificationsOpen, showNotifications, running, remotes, notify }: {
  repositories: UseQueryResult<RepositoryResponse[], Error>;
  driver: UseQueryResult<DriverState, Error>;
  attached: boolean;
  attention: AttentionRead;
  presses: Confirmation[];
  t: TFunction<"translation", undefined>;
  outstanding: UseQueryResult<QuestResponse[], Error>;
  scope: ReturnType<typeof useScope>;
  workspaces: UseQueryResult<string[], Error>;
  askRecords: UseQueryResult<AskResponse[], Error>;
  serviceRead: boolean;
  notificationsOpen: boolean;
  showNotifications: (at?: Omit<NotificationsAt, "n"> | undefined) => void;
  running: UseQueryResult<Session[], Error>;
  remotes: UseQueryResult<MachineWiring, Error>;
  notify: Notify;
}) {
  const indexed = (repositories.data ?? []).reduce((sum, r) => sum + r.total, 0);

  // Ready once the driver's service is up, not merely its file (LOOK2a); a shell older than the field says nothing, and reads ready.
  const presence: DriverPresence = driver.data
    ? (driver.data.ready === false ? 'starting' : 'running')
    : driver.isError ? 'stopped' : attached ? 'starting' : 'absent';
  // The bell's count (NOTIFY2b, the notification design §2.1): each press of either source once, and every other row of What needs
  // you as .NET judged them (PUSH1d4), a press's own row being its card. Two answers to "how many need me" would disagree the first
  // time either was edited. Before the first judgement the rows are no count yet, and the presses count alone. Each obligation once
  // (COMMS1j), and an unread message none.
  const needsRows = attention.state === 'known' ? needsListed(attention.rows).length : 0;
  const waiting = bellCount(presses, attention.state === 'known' ? attention.rows : []);
  // While any press waits unshown, the bell wears the warning hue with the presses' own count (§3.3, Fable's F1.1).
  const pressUnshown = presses.some((each) => each.shown !== true);
  // The same count on the window's taskbar button and in its title (NOTIFY1b), so it is seen with the window behind others.
  useTaskbarCount(waiting, t('nav.waiting', { count: waiting }));
  // And the notices' words, so a notification behind the window speaks its language (NOTIFY2d, the notification design §3.3).
  useNoticeWords();
  // What a press's card names its references by (§3.2): a session as Sessions names it, its workspace beside it where its
  // repository's name is in two workspaces, from the records the window holds; a quest by its name.
  const allSessions = useSessions(null, true, attached);
  const machineRegistry = useRegistry('machine');
  // 🔴 One count of the sessions the driver runs (SESSBADGE1, D76's note), the cap's own (CAPCOUNT1), judged in .NET beside the rows:
  // the status bar says it, a standing fact with its noun, never a conversation the person holds. It is no badge (BADGEUX1).
  const working = attention.state === 'known' ? attention.working : 0;
  // The status bar's: not judged yet is no count, never a zero (PUSHUX1); a plain browser has no driver here, so none it runs.
  const workingSaid = attention.state === 'reading' ? null : working;
  // 🔴 One meaning for a number (BADGEUX1, the UI/UX design §3.10): a place counts only what waits on the person there, the bell's rows
  // whose door opens it (`placeOf`), so each place's number is the bell's for the same rows and the places sum to the bell's rows.
  // Running work is a mark, never a number: Sessions' while the driver's work is under way and not parked on the person, Quests'
  // while a quest is taken. The sessions the page holds judge where a unit of work's door goes, as the band's door does.
  const placed = attention.state === 'known' ? placeCounts(attention.rows, allSessions.data) : {};
  const sessionsRunning = attention.state === 'known' ? attention.running : 0;
  const questsTaken = (outstanding.data ?? []).filter((quest) => quest.status === 'Taken').length;
  // Where this circle stands with its remote (SYNC6b), from this machine's own host — so a browser on
  // the machine reads it too, and it says for itself whether the circle is wired. Before it answers,
  // the shell's map says; a browser with neither is not asked.
  // 🔴 WHICH circle: the one chosen, or the only one there is. "Every circle" has no single standing,
  // and assuming `default` both asked a door for a scope nobody chose and named the wrong circle in a
  // family whose one circle is called something else.
  const circle = scope.workspace ?? (workspaces.data?.length === 1 ? workspaces.data[0] : null);
  const standing = useSyncStanding(circle);
  const syncNow = useSyncNow();
  // Titles for the quests in conflict, from the cache the Quests view fills anyway; one it has not
  // loaded is named by its id alone.
  const everything = useQuests(null, true);
  const pressNames: PressNames = useMemo(() => {
    const workspacesOf = new Map<string, Set<string>>();
    for (const row of machineRegistry.data ?? []) {
      if (!row.workspace) continue;
      const held = workspacesOf.get(row.repository) ?? new Set<string>();
      held.add(row.workspace);
      workspacesOf.set(row.repository, held);
    }
    const quests = everything.data ?? [];
    return {
      session: (id) => {
        const record = (allSessions.data ?? []).find((one) => one.id === id);
        if (!record) return null;
        const quest = record.quest ? quests.find((one) => one.id === record.quest) : undefined;
        const collides = (workspacesOf.get(record.repository)?.size ?? 0) > 1;
        return { title: sessionTitle(record, quest), repository: record.repository, workspace: collides ? record.workspace ?? null : null };
      },
      quest: (id) => quests.find((one) => one.id === id)?.title ?? null,
    };
  }, [allSessions.data, machineRegistry.data, everything.data]);
  // A press said by a toast in the window in front (NOTIFY2b, §3.3): its act and the session it names, never a value; its door
  // shows *Notifications* at its card. Said once, staying until the view opens or the press ends.
  const pressToast = (press: Confirmation): string => {
    if (press.act) return t('person.ask.for', { act: press.act });
    const key = routeKey(press.module, press.route);
    const act = t(key);
    if (act === key) return t('person.ask.for', { act: press.route });
    const session = press.named?.find((each) => each.role === 'session');
    const named = session ? pressNames.session?.(session.value) : null;
    return named ? t('notifications.toast.press', { act, session: named.title }) : t('person.ask.to', { act });
  };
  const messageNames: MessageNames = {
    ask: (id) => {
      const record = (askRecords.data ?? []).find((one) => one.id === id);
      return record ? askName(record) : null;
    },
    session: (id) => pressNames.session?.(id)?.title ?? null,
  };
  const announcements = usePressAnnouncements({
    presses,
    serviceRead,
    viewOpen: notificationsOpen,
    words: pressToast,
    openLabel: t('notifications.toast.open'),
    onOpen: (press) => showNotifications({ press: press.id }),
  });
  // Who is driving Daoris's browser (BRW8), named from the caches the frame already fills: the driver's
  // ids, the sessions, and the quests they serve. Said beside the browser's door and in its Settings.
  const driving = browserDrivers(driver.data?.drivingBrowser, running.data ?? [], everything.data ?? []);
  const wired = standing.data
    ? standing.data.wired
    : remotes.data && circle
      ? remotes.data.remotes.some((row) => row.workspace === circle)
      : null;

  const onSyncNow = (workspace: string) => syncNow.mutate(workspace, {
    // The pass's own words, verbatim: the wall it hit, and what the remote understood and did not
    // take. A clean pass that said nothing is the one sentence this page writes.
    onSuccess: (report) => {
      if (report.problem) notify(report.problem, 'error');
      for (const note of report.notes) notify(note);
      if (!report.problem && report.notes.length === 0) notify(t('work.sync.done', { workspace }));
    },
    onError: failure(notify),
  });
  return { machineRegistry, circle, wired, onSyncNow, syncNow, driving, announcements, waiting, pressUnshown, placed, sessionsRunning, questsTaken, needsRows, working, pressNames, messageNames, presence, workingSaid, standing, everything, indexed };
}
