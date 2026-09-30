import { useShenora } from '@shenora/react';
import { App } from './App';
import { LinkOpener } from './links';
import { ShellSignals } from './ShellSignals';
import { Toasts, useToasts } from './ui';
import { DetachedSession } from './work/DetachedSession';
import { MonitorWindow } from './work/MonitorWindow';
import { useLinkOpener, useSecondaryWindowTheme } from './shell';
import { MONITOR_WINDOW, type SecondaryWindow, sessionWindowName } from './work/window';

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

  if (!isAvailable) return <App />;

  return (
    <LinkOpener.Provider value={linkOpener}>
      {which.kind === 'monitor'
        ? <MonitorWindow notify={notify} />
        : <DetachedSession id={which.id} notify={notify} />}

      <Toasts items={toasts} onClose={dismiss} />
      <ShellSignals notify={notify} />
    </LinkOpener.Provider>
  );
}
