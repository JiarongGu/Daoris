import { type ListView } from '../work/frame/layout';
import { type View, VIEWS } from '../menus/commands';
import { stored } from '../lib/stored';

/** The views are `commands.ts`'s one list (D66); the activity bar and the palette read the same. */
export type Tab = View;

/**
 * Whether the person was last watching Sessions — a per-browser preference like the language and the
 * scope, never machine wiring and never a tracked file. Only Sessions is worth returning to (D55's
 * reason for remembering a mode, kept when the mode went — D66): a relaunch lands on Overview, the
 * landing view (D40), unless the person was attending work.
 */
export const VIEW = 'daoris.view';

// What each view's list has chosen is its list's memory since FRAME1c (`work/frame/listPanes.ts`, D118 §3f),
// in the keys that predate it: the session attended (D56), since relaunching into Sessions landed on
// *Nothing attended* while a session sat parked, and Settings' domain (D75), so the gear returns to it.
// A session whose record is gone is let go by the Work frame, a remembered record that closed or went
// once its view reads it (UX6b), and a domain this window cannot show opens on Appearance, so a stale
// one costs nothing.

export function rememberedView(): Tab {
  // Landing on Overview is the safe half of the choice.
  return stored(VIEW) === 'sessions' ? 'sessions' : 'overview';
}

// The activity bar: every view but Settings, which has its own gear. Sessions — what the Work frame
// was (D66) — is absent in a browser rather than disabled.
export const NAV = VIEWS.filter(({ view }) => view !== 'settings');

/**
 * A place's marks (BADGEUX1, the UI/UX design §3.10): its number, what waits on the person there; its running mark, work under way
 * there; and both in words for its tip and its description, the waits first, as the taskbar says them. Sessions says its work under
 * way, Quests its quests taken; no other place runs work.
 */
export function placeMarks(place: View, waits: number, running: number, t: (key: string, values?: Record<string, unknown>) => string) {
  const said = [
    waits > 0 ? t('nav.waiting', { count: waits }) : null,
    running > 0 ? t(place === 'quests' ? 'nav.taken' : 'nav.underWay', { count: running }) : null,
  ].filter(Boolean).join(' · ');
  return { waits, running: running > 0, ...(said ? { detail: said } : {}) };
}

/**
 * The views drawn with a list pane (D118 §2), each naming it in `layout.list.<view>` and
 * `layout.menu.list.<view>`. Sessions' first (FRAME1b), then Plugins, built on the frame (PLUGUI1b), then Quests
 * (FRAME1d), Repositories (FRAME1e), Settings (FRAME1g), and Convergence and Search (FRAME1f), one place since UX6i:
 * every view but Overview and Map, which have none (§4). Knowledge names its list by its mode's (`listOf`).
 */
export const LISTED: ReadonlySet<ListView> = new Set<ListView>([
  'sessions', 'plugins', 'quests', 'projects', 'knowledge', 'agents', 'settings',
]);
export const isListed = (view: View): view is View & ListView => (LISTED as ReadonlySet<string>).has(view);

/**
 * A command's id as the machine log takes it (LOG1b): a family's row by its family (`workspace.scope`, `go.agent`), so a
 * workspace's or an agent's name never reaches the log.
 */
export const loggedId = (id: string) => id.split(':')[0]!;
