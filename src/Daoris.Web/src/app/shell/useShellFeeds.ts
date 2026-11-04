import { useCallback, useMemo, useRef, useState } from 'react';
import { useAsks, useSessions } from '../../data/queries';
import { type NotificationsAt } from '../../notifications/NotificationsView';
import { useAttention, useDismissUpdate, useHarnesses, useLinkOpener, useOpenBrowser, useOpenWindow, useRemotes, useSayUpdate, useShowReviewAgain, useUpdateState, useAnswerConfirmation, useConfirmationShown, usePersonKey, useServiceConfirmations, useTerminalConfirmations, useUnkeyedHost, useDismissNotice, useMarkChannel, useMarkNoticesRead, useMessages, useNotices } from '../../bridge/shell';
import { type Confirmation } from '../../person/Confirmations';
import { useReviewActs } from '../../work/gates/reviewActs';
import { type Notify } from '../../design/atoms/Toasts';

export function useShellFeeds({ notify }: {
  notify: Notify;
}) {
  // The open sessions, for who drives the browser and the session attended, and the remotes, the status bar's. Both share caches
  // the frames already fill, so neither is a second fetch — and both are absent in a browser, which is what the bar then says.
  const running = useSessions(null, false);
  const remotes = useRemotes();
  // Who needs the person, as the shell pushes it (PUSH1d4): the bell counts the band's rows and each place of the activity bar the rows
  // its door opens (BADGEUX1), the status bar the sessions the driver runs (SESSBADGE1), all judged in .NET once for every window, so
  // the numbers and the band are one judgement. A browser has none.
  const attention = useAttention();
  // Done and failed (NOTIFY2f, D196 point 6): the notices of this window's scope, as the shell pushes them, and the person's read and
  // dismissal of them, written by the shell and pushed to every window.
  const notices = useNotices();
  const markNoticesRead = useMarkNoticesRead();
  const dismissNotice = useDismissNotice();
  // Messages (NOTIFY2g, the notification design §2.2's third group): each ask's entry of this window's scope and the conversations'
  // answers, as the shell pushes them; an ask's *Mark read* is its channel's mark moved through its newest message (`CHANNEL_MARK`),
  // and an ask is named by its own sentence where this machine holds it, asked only while one is listed.
  const messages = useMessages();
  const markChannel = useMarkChannel();
  const askRecords = useAsks(false, (messages.asks ?? []).some((entry) => !entry.teammate && entry.unread > 0));
  // The agents this machine has, for the Agents menu (UX6e): the roster the frame holds, asked once and kept.
  const roster = useHarnesses();
  // The install's update (UPDATE1, D139): what is staged, the drain, and the person's word on it, as the banner's.
  const update = useUpdateState();
  const sayUpdate = useSayUpdate();
  const dismissUpdate = useDismissUpdate();
  // The person's door in this window (PERSONDOOR1f, RPC1c; D156 points 2 and 4): the person key, asked of the shell as the page
  // loads and held in memory for every write; what a terminal sent to be confirmed here, and, once the key is held, what waits at
  // the service; the person's answer; and the adoption notice where the host gates with a key this window does not hold.
  const personKey = usePersonKey();
  const unkeyedHost = useUnkeyedHost(personKey);
  const [unkeyedSaid, setUnkeyedSaid] = useState(false);
  const terminalConfirmations = useTerminalConfirmations();
  // NOTIFY2j: as the shell tells every window, so each counts one list, and its first read is no arrival.
  const { waiting: serviceConfirmations, read: serviceRead } = useServiceConfirmations(personKey.held);
  const answerConfirmation = useAnswerConfirmation();
  // CONFIRMSEEN1: a terminal's press's two minutes count from when this window, in front of the person, shows its card.
  const confirmationShown = useConfirmationShown();
  // NOTIFY2b (the notification design §3.4): a service's press is shown when this window judges its card's region in view, which its
  // door never hears; the window keeps that, so the bell's warning hue goes once each is shown here.
  const [serviceShown, setServiceShown] = useState<ReadonlySet<string>>(() => new Set());
  // Every press waiting, of either source, as the confirmation channel tells them (never a row's copy, §2.2).
  const presses: Confirmation[] = useMemo(() => [
    ...terminalConfirmations,
    ...serviceConfirmations.map((each) => ({ ...each, shown: serviceShown.has(each.id) })),
  ], [terminalConfirmations, serviceConfirmations, serviceShown]);
  const pressShown = (waiting: Confirmation) => {
    if (waiting.source === 'pipe') confirmationShown.mutate(waiting);
    else setServiceShown((was) => new Set([...was, waiting.id]));
  };
  // *Notifications* (NOTIFY2b, §2.1): whether this window draws it; each ask for it shown where it stands, and the bell's presses,
  // each counted, as Ask Daoris's are; and what a door asked to show there.
  const [notificationsOpen, setNotificationsOpen] = useState(false);
  const [notificationsShow, setNotificationsShow] = useState(0);
  const [notificationsToggle, setNotificationsToggle] = useState(0);
  const [notificationsAt, setNotificationsAt] = useState<NotificationsAt | null>(null);
  const notificationsAsks = useRef(0);
  const showNotifications = useCallback((at?: Omit<NotificationsAt, 'n'>) => {
    notificationsAsks.current += 1;
    const n = notificationsAsks.current;
    if (at) setNotificationsAt({ ...at, n });
    setNotificationsShow(n);
  }, []);
  // Opening a window is the shell's act, not the page's (SURF8). In a browser it simply rejects,
  // which is why the commands that use it are gated on a shell being here.
  const openWindow = useOpenWindow();
  const openBrowser = useOpenBrowser();
  // *Show it again* on the strip's review chip (REVIEWENV1d): a set-up's build served to its tab again, and the tab in front.
  const showReviewAgain = useShowReviewAgain();
  // The chip's *Reviewed* (REVIEWENV1g): the review's one owner, as the step's page presses it.
  const reviewActs = useReviewActs({ notify });
  // Where the page's links open (BRW7): Daoris's browser where the person chose it and a shell is here
  // to open one, and otherwise null — a link then opens as a link always has.
  const linkOpener = useLinkOpener(notify);
  return { attention, presses, askRecords, serviceRead, notificationsOpen, showNotifications, running, remotes, roster, openWindow, openBrowser, messages, markNoticesRead, linkOpener, notices, setNotificationsToggle, showReviewAgain, reviewActs, update, sayUpdate, dismissUpdate, unkeyedHost, unkeyedSaid, setUnkeyedSaid, answerConfirmation, pressShown, notificationsAt, setNotificationsOpen, dismissNotice, markChannel, notificationsShow, notificationsToggle };
}
