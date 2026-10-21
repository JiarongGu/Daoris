import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import { sentence } from '../locales/format';
import { type AttentionTold, attentionToast } from '../notifications/noticeWords';
import { keys } from '../data/queries';
import {
  accountsSlice, asksSlice, attentionSlice, callsSlice, driverSlice, groupsSlice, linesSlice, notPushed, questsSlice, sessionsSlice,
  sweepSlice, syncSlice, usageSlice, useDriverReady, useStateHub, useStateSlices, workSlice,
} from '../bridge/shell';
import { type AccountWaitTick, type Consideration, type TrustHold, newsFrom } from '../data/signals';
import type { Notify } from '../design/ui';
import { FIRST_MAIN_WINDOW, ownWindowName } from '../work/window';

type Tick = { events?: string[]; considered?: Consideration[]; untrusted?: TrustHold[]; waits?: AccountWaitTick[] };

/** The slices the shell pushes with their data and this page reads pushed (PUSH1a, D182); every other family keeps its pulls. */
const PUSHED = [
  driverSlice, linesSlice, sessionsSlice, callsSlice, workSlice, groupsSlice, syncSlice, sweepSlice, questsSlice, asksSlice, usageSlice,
  accountsSlice, attentionSlice,
];

/**
 * The shell's push channel into the page (D46 §6): the driver's tick reports arrive as toasts, and
 * everything a tick can change refetches — in the desktop the session surface is live, while a plain
 * browser keeps its polling and never mounts a transport. Renders nothing; costs nothing where no
 * host answers.
 */
export function ShellSignals({ notify, onAttend, attended, onConfirming }: {
  notify: Notify;
  /**
   * A terminal's press asked, by its id (NOTIFY2b, the notification design §3.3): the press's own event, told once at its ask, which
   * the window announces in its own words while it is the one focused. Absent, it is said by no toast here.
   */
  onConfirming?: (id: string) => void;
  /**
   * The session this window shows on its Sessions view now (NOTIFY1c), so an answer from that very conversation is not toasted
   * over the page that already shows it. Absent or null: no session is open here, and every toast is said.
   */
  attended?: string | null;
  /**
   * A session the shell is asking the page to attend (SURF5b) — the person clicked an OS
   * notification. Absent where nothing can act on it, which is every window but the application's.
   */
  onAttend?: (session: string) => void;
}) {
  const { isAvailable, bridge } = useShenora();
  const { t } = useTranslation();
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

    // Why every open quest is sitting, this tick — the half of the report that is not news but a
    // STANDING answer, so it lands where the views read it rather than in a toast that dismisses
    // itself. Replaced whole: a quest the driver no longer considers is no longer sitting.
    client.setQueryData(keys.considered, tick?.considered ?? []);
    // And which of those holds are the agent's trust (D73), as facts the screen can offer the person
    // to grant. Replaced whole for the same reason; a shell older than the grant sends none.
    client.setQueryData(keys.untrusted, tick?.untrusted ?? []);
    // And which starts wait on their accounts (UX6d), an ask's intake among them, which no consideration names. Replaced whole; an
    // older shell sends none. *What needs you* reads none of these three: who needs the person is pushed (PUSH1d4).
    client.setQueryData(keys.waits, tick?.waits ?? []);

    // 🔴 Refetching is NOT deduplicated. A tick that repeats its report can still have changed the
    // world — the two questions are different, and collapsing them would make a stale window the
    // price of a quiet one. What is read under the sessions' key, but the records and the groups where
    // the shell pushes them (PUSH1d2, PUSH1c3b): their pushes write them, and a tick reads none of them again.
    void client.invalidateQueries({ queryKey: keys.allSessions, predicate: notPushed(client) });
    // The quests, but where the shell pushes them (PUSH1d3): a quest's move is pushed as the change stream tells it. What is read
    // beneath them (each quest's and ask's work) is still asked again.
    void client.invalidateQueries({ queryKey: keys.allQuests, predicate: notPushed(client) });
    // 🔴 And the asks. A tick takes them (INT4b), and the attention band reads them beside the
    // sessions (INT4d) — so a session list fresher than its asks is worse than both stale: seen on
    // the window, an ask made by the other door was missing from *What needs you*, and its parked
    // intake read as a bare session, because the band could not see the ask it belonged to. Where the
    // shell pushes them (PUSH1d3), an ask the other door made is pushed as the change stream tells it,
    // and a tick asks again only each channel and the asks elsewhere beneath them.
    void client.invalidateQueries({ queryKey: keys.allAsks, predicate: notPushed(client) });
    // What is read under the driver's key, but its state where the shell pushes it (PUSH1a, D182): its pushes write it, and a
    // tick reads none of it again.
    void client.invalidateQueries({ queryKey: keys.driver, predicate: notPushed(client) });
    // And the rules: a tick settles what agents proposed about them (PERM2), applying a narrowing and
    // holding a widening for the person, which the Agents place's rules show (*What needs you*'s row is pushed, PUSH1d4).
    void client.invalidateQueries({ queryKey: keys.rules });
    // Every tick runs a pass for each wired circle, so where each stands may have moved (SYNC6b); where the shell pushes the
    // standings, the pass's end pushes what it moved (PUSH1a2), and a tick reads none of them again.
    void client.invalidateQueries({ queryKey: keys.allSync, predicate: notPushed(client) });
    // 🔴 And the index. Seen on the deployed application: the status bar read "555 entries · 7
    // repositories" for as long as the window stayed open, on an index that held 1,050 across 17 —
    // the page had asked while a schema rebuild was still feeding, cached the partial answer, and
    // nothing since had told it to look again. A rebuild is observable, so the tick that says "the
    // world may have changed" has to include the world's size.
    void client.invalidateQueries({ queryKey: keys.allRepositories });
    // 🔴 And the registry (FG4). Seen on the deployed application: 29 repositories imported from a
    // terminal, and Overview still said *no workspace yet* until a reload. The shell now forwards a
    // tick when the registry moved, and this is the half that makes the page look again.
    void client.invalidateQueries({ queryKey: keys.allRegistry });
    // And the accounts (TOOL4g): a session's end may have cooled one, its door may have said what one has left, and the
    // sessions running on each have moved. A file read, never a probe; where the shell pushes them (PUSH1e) its owner judges
    // those once for every window, and a tick reads none of them again.
    void client.invalidateQueries({ queryKey: keys.accounts, predicate: notPushed(client) });
  });

  // The driver's service is up (LOOK2a): what it refused while it came up is asked again now, not at the first tick.
  useDriverReady();

  // What a press changed, wherever it was pressed (STATEHUB1b, D169): asked again here at the one place, in every window.
  useStateHub();

  // What the shell pushes with its data (PUSH1a, D182): heard first, then each slice's snapshot asked, in every window.
  useStateSlices(PUSHED);

  // A failure the driver names is worded from the catalogue, in the reader's language (UX5 U30): a
  // refused connection toasted the .NET socket's own words. Anything else keeps the driver's.
  useShenoraEvent<{ message?: string; code?: string }>('DAORIS', 'DRIVER_ERROR', (error) => {
    notify(error?.code ? sentence({ code: error.code }) : error?.message ?? 'driver error', 'error');
  });

  /**
   * What is worth interrupting the person for (SURF5b): a session that parked, or one that ended
   * without them asking; a quest parked on its failed sessions (SESSUX1i), which waits on their
   * *Try again* as a park waits on their answer; and a conversation that answered their words
   * (NOTIFY1a), news as an ending is. The same event the shell turns into an OS balloon
   * while nobody is looking at the window — so this is the half for when somebody IS, and the two
   * never both fire because the shell tests the foreground before raising one.
   *
   * A held tool call (NOTIFY2d), which its bound refuses by itself, is said as a park is.
   *
   * Said in the window's words (NOTIFY2d, the notification design §3.3): each kind's headline as the
   * shell's balloon says it behind the window, and what a session wrote beside it as written; the
   * driver's English headline only for a kind this build does not word. A window reading 中文 toasted
   * the driver's English until then.
   */
  useShenoraEvent<AttentionTold>(
    'DAORIS', 'SESSION_ATTENTION', (item) => {
      // NOTIFY2b (§3.3): a terminal's press is announced by the window in its own words, keyed on the press's id, never in the
      // shell's English; its card is in *Notifications*, and the shell's balloon is for when no window is in front.
      if (item?.kind === 'Confirming') {
        if (typeof item.id === 'string' && item.id) onConfirming?.(item.id);
        return;
      }
      if (!item?.headline) return;
      // NOTIFY1c: a conversation's answer to the one the person is reading, in a window in front, is already on their page,
      // so a toast over it is noise. Only that kind: a park or an ending of the attended session still interrupts.
      const reading = item.kind === 'Answered' && attended != null && item.session === attended
        && typeof document !== 'undefined' && document.visibilityState === 'visible';
      const said = reading ? null : attentionToast(t, item);
      if (said) {
        notify(
          said,
          // A park is the one that is WAITING on somebody, as a held tool call is; an ending, or an answer, is news.
          item.kind === 'Parked' || item.kind === 'QuestParked' || item.kind === 'Holding' ? 'error' : 'ok');
      }
      void client.invalidateQueries({ queryKey: keys.allSessions, predicate: notPushed(client) });
    });

  /**
   * A conversation ended (SES2). The driver says so the moment its record moves, because nothing
   * else would until the next tick — which is up to a poll in which a chat whose harness exited read
   * as working, and a message typed into it went nowhere (REV3). Not a toast: the person asked for
   * the ending, or `SESSION_ATTENTION` already told them.
   */
  useShenoraEvent<{ session?: string; state?: string }>('DAORIS', 'SESSION_ENDED', () => {
    // The record, where the shell pushes the sessions, and what runs, where it pushes the driver's state, are pushed by the end
    // itself (PUSH1d2, PUSH1a).
    void client.invalidateQueries({ queryKey: keys.allSessions, predicate: notPushed(client) });
    void client.invalidateQueries({ queryKey: keys.driver, predicate: notPushed(client) });
  });

  /**
   * The person clicked an OS notification, so the window comes forward on that session. The shell names the main window it brought
   * forward, the one whose workspace holds the session (NOTIFY2c), and only that window's page attends it, so a window on another
   * workspace keeps what it shows; a shell that names none is every main window's, as before.
   */
  useShenoraEvent<{ session?: string; window?: string }>('DAORIS', 'ATTEND_SESSION', (asked) => {
    if (asked?.window && asked.window !== (ownWindowName(globalThis.location?.search ?? '') ?? FIRST_MAIN_WINDOW)) return;
    if (asked?.session) onAttend?.(asked.session);
  });

  return null;
}
