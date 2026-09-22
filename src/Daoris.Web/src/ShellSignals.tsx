import { useEffect, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import { keys } from './queries';
import { newsFrom } from './signals';
import type { Notify } from './ui';

type Tick = { events?: string[] };

/**
 * The shell's push channel into the page (D46 §6): the driver's tick reports arrive as toasts, and
 * everything a tick can change refetches — in the desktop the session surface is live, while a plain
 * browser keeps its polling and never mounts a transport. Renders nothing; costs nothing where no
 * host answers.
 */
export function ShellSignals({ notify, onAttend }: {
  notify: Notify;
  /**
   * A session the shell is asking the page to attend (SURF5b) — the person clicked an OS
   * notification. Absent where nothing can act on it, which is every window but the application's.
   */
  onAttend?: (session: string) => void;
}) {
  const { isAvailable, bridge } = useShenora();
  const client = useQueryClient();
  /** What the last tick said, so an unchanged report is not said again. A ref: it must not re-render. */
  const toldLast = useRef<string[]>([]);

  useEffect(() => {
    if (!isAvailable) return;
    // The kit buffers host events until this handshake — without it, no notification ever arrives.
    bridge.notifyReady().catch(() => {
      // A failed handshake is the host log's problem; the page stays a working page.
    });
  }, [isAvailable, bridge]);

  useShenoraEvent<Tick>('DAORIS', 'DRIVER_TICK', (tick) => {
    /* 🔴 The driver's own sentences, verbatim — but only the ones that are NEWS.
     *
     * A tick report is a log: it says what this tick did, every tick, which is right for a console
     * and wrong for an interruption. Seen on the deployed application, four identical toasts about
     * one untrusted repository, stacking up the window — and that condition needs a person to run a
     * command, so it would have said so forever. The verbatim rule is untouched; what changed is
     * whether being told again counts as being told something. */
    const lines = tick?.events ?? [];
    for (const line of newsFrom(toldLast.current, lines)) notify(line);
    toldLast.current = lines;

    // 🔴 Refetching is NOT deduplicated. A tick that repeats its report can still have changed the
    // world — the two questions are different, and collapsing them would make a stale window the
    // price of a quiet one.
    void client.invalidateQueries({ queryKey: keys.allSessions });
    void client.invalidateQueries({ queryKey: keys.allQuests });
    void client.invalidateQueries({ queryKey: keys.driver });
  });

  useShenoraEvent<{ message?: string }>('DAORIS', 'DRIVER_ERROR', (error) => {
    notify(error?.message ?? 'driver error', 'error');
  });

  /**
   * What is worth interrupting the person for (SURF5b): a session that parked, or one that ended
   * without them asking. The same event the shell turns into an OS balloon while nobody is looking
   * at the window — so this is the half for when somebody IS, and the two never both fire because
   * the shell tests the foreground before raising one.
   *
   * The sentence is the driver's own and is rendered verbatim, like every other sentence it
   * writes (D24): it is composed once in `AttentionWatch` so a toast here and a line on a headless
   * machine cannot drift.
   */
  useShenoraEvent<{ headline?: string; detail?: string; kind?: string }>(
    'DAORIS', 'SESSION_ATTENTION', (item) => {
      if (!item?.headline) return;
      notify(
        item.detail ? `${item.headline}: ${item.detail}` : item.headline,
        // A park is the one that is WAITING on somebody; an ending is news.
        item.kind === 'Parked' ? 'error' : 'ok');
      void client.invalidateQueries({ queryKey: keys.allSessions });
    });

  /** The person clicked an OS notification, so the window comes forward on that session. */
  useShenoraEvent<{ session?: string }>('DAORIS', 'ATTEND_SESSION', (asked) => {
    if (asked?.session) onAttend?.(asked.session);
  });

  return null;
}
