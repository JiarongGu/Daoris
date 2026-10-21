import { useShenora } from '@shenora/react';
import { useTranslation } from 'react-i18next';
import { App } from './App';
import { LinkOpener } from '../design/links';
import { ContextMenus } from '../menus/ContextMenu';
import { clipped } from '../menus/press';
import { ShellSignals } from './ShellSignals';
import { failure, Toasts, useToasts } from '../design/ui';
import { DetachedSession } from '../work/console/DetachedSession';
import { MonitorWindow } from '../work/console/MonitorWindow';
import { useLinkOpener, useOpenBrowser, useSecondaryWindowTheme } from '../bridge/shell';
import { MONITOR_WINDOW, type SecondaryWindow, sessionWindowName } from '../work/window';

/**
 * The root of a window that is not the application (D55 §b, SURF8).
 *
 * @remarks
 * **A secondary window carries none of the application's chrome**: it keeps its *native* frame
 * (D55 §b, and `SecondaryForm` has the framework constraint behind it), so there is no app strip,
 * no activity bar, no mode switch and no palette here.
 *
 * **The handshake is not optional.** `ShellSignals` is what tells the host this page is ready to
 * receive notifications; without it the bridge buffers every event forever and a window whose entire
 * job is live output shows none. It is mounted here for that reason first, and for the driver's
 * tick-driven refetch second.
 */
export function SecondaryWindowRoot({ window: which }: { window: SecondaryWindow }) {
  // Absent in a browser, like every other shell-only surface (D47 §4). Both of these windows are a
  // rail and a live stream, and the stream has no HTTP route at all — so over a keyed remote this
  // would be a window with nothing honest in it. A URL somebody pasted therefore lands on the
  // platform rather than on an empty imitation of the desktop, which is the same fallback the Work
  // frame already makes for a remembered mode it cannot honour.
  const { isAvailable } = useShenora();
  const { toasts, notify, dismiss } = useToasts();
  // Its own frame's caption follows the theme this page is in (WINDOW2), not the OS's.
  useSecondaryWindowTheme(which.kind === 'monitor' ? MONITOR_WINDOW : sessionWindowName(which.id));
  // A detached conversation's links go where the application's do (BRW7).
  const linkOpener = useLinkOpener(notify);
  const { t } = useTranslation();
  const openBrowser = useOpenBrowser();

  if (!isAvailable) return <App />;

  return (
    <LinkOpener.Provider value={linkOpener}>
      {/* First (PUSH1a): its effects run before the window's own, so the driver's state is read as the snapshot it asks and
          never pulled beside it. It renders nothing. */}
      <ShellSignals
        notify={notify}
        // NOTIFY2b (the notification design §3.3): this window holds no *Notifications*, so a terminal's press heard while it is the
        // one in front is said here by where it waits, with no door; the main window's taskbar button is lit meanwhile.
        onConfirming={() => {
          if (document.visibilityState === 'visible' && document.hasFocus()) notify(t('notifications.toast.inMain'), 'error');
        }}
      />
      {which.kind === 'monitor'
        ? <MonitorWindow notify={notify} />
        : <DetachedSession id={which.id} notify={notify} />}

      <Toasts items={toasts} onClose={dismiss} />
      {/* The right-click menu here too (CTX1, D138 §6): no Search and no Quick Ask in this window, so a selection is
          offered its copy alone; a link opens in Daoris's browser as the application's does. */}
      <ContextMenus
        doors={{
          copy: (text) => {
            void navigator.clipboard?.writeText(text).then(() => notify(t('contextMenu.copied', { what: clipped(text) })), () => {});
          },
          openInBrowser: (address) => openBrowser.mutate(address, { onError: failure(notify) }),
        }}
      />
    </LinkOpener.Provider>
  );
}
