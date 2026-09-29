import type { IconName } from './ui';

// The command registry (SURF9). The palette is the widget; THIS is the expensive half — every action
// addressable by name — and it is a pure function so that "what can I do right now" is a value a test
// can assert rather than a screen somebody has to arrange.

/** The application's views (D66): one list, the activity bar's. */
export type View = 'overview' | 'sessions' | 'quests' | 'projects' | 'map' | 'convergence' | 'search' | 'settings';

/**
 * Every view, in the activity bar's order, with the glyph both the bar and the palette show. The
 * bar and the palette each wrote this list (REV3 CLEAN1); the bar is it without Settings, which has
 * its own gear.
 */
export const VIEWS: readonly { view: View; icon: IconName; keywords?: string; shellOnly?: boolean }[] = [
  { view: 'overview', icon: 'overview' },
  // A stream never leaves the machine that produced it (D47 §4), so a browser is shown no Sessions.
  { view: 'sessions', icon: 'frameWork', keywords: 'work watch console conversation', shellOnly: true },
  { view: 'quests', icon: 'quests' },
  { view: 'projects', icon: 'projects' },
  // The workspace map (MAP2, D67 §3): how the repositories are wired, read at a glance.
  { view: 'map', icon: 'map', keywords: 'map topology graph wiring repositories' },
  { view: 'convergence', icon: 'convergence' },
  { view: 'search', icon: 'search' },
  // Everywhere since D66: a browser has appearance to set, if nothing of a machine.
  { view: 'settings', icon: 'settings', keywords: 'settings theme dark light appearance language machine remote harness account' },
];

/** A named thing a person can do, from anywhere. */
export type Command = {
  /** Stable, structural, and never shown: the id is what a test and a keybinding name. */
  id: string;
  /** What the person reads. Already translated by the caller — this module holds no i18n. */
  title: string;
  /** The heading it sits under, so a list of thirty is still scannable. */
  group: string;
  icon: IconName;
  /** Extra words that should MATCH but are not shown — "settings" finding Machine, and 中文. */
  keywords?: string;
  run: () => void;
};

/**
 * Everything the application can be asked to do right now.
 *
 * @remarks
 * **A command is absent where its surface is absent** — never present-and-disabled. That is the same
 * rule the Machine tab, the mode switch and the caption room already follow, and it matters more
 * here: a palette is a promise that what it lists can be done, so listing a shell-only action in a
 * browser would be the palette lying. The disclosure boundary (D47 §4) is therefore enforced by
 * OMISSION, and Playwright asserts the absence.
 *
 * **Pure, and given its world.** No hook, no bridge, no i18n: the caller hands in what is true and
 * what to run. That is what makes every combination — a browser, a shell on Overview, a shell on
 * Sessions with something parked — reachable by passing an argument.
 */
export function commands(world: {
  /** Titles, already translated, keyed by command id. The caller owns the catalogue. */
  label: (id: string) => string;
  /** Group headings, already translated. */
  group: (id: 'go' | 'do' | 'work') => string;
  /** Whether a shell is here. False is a browser: no Sessions, no machine settings, no session controls. */
  attached: boolean;
  /** The view in front of the person — the palette does not offer to go where they already are. */
  current: View;
  go: (view: View) => void;
  refresh: () => void;
  toggleLanguage: () => void;
  /** Ask Sessions to open its start-a-session form. Shell-only, like the view. */
  startSession: () => void;
  /** Ask Sessions to show the review of whatever is attended. Shell-only. */
  review: () => void;
  /** Open the monitor window on a second screen (SURF8). Shell-only: a page cannot open a window. */
  monitor: () => void;
  /** Open Daoris's own browser (D78), where the person signs in and watches. Shell-only, like the monitor. */
  browser: () => void;
  /**
   * Open the ask composer (INT4c) — anywhere, a browser included: an ask is a local host's HTTP door,
   * so it needs no shell, only this machine.
   */
  ask: () => void;
  /** Open Ask Daoris (HELP1): its panel beside whatever is in front of the person. Shell-only. */
  help: () => void;
  /** Open Quick Ask (DOCK1d): Ask Daoris's conversation in a box at the palette's place. Shell-only. */
  quickAsk: () => void;
  /**
   * Detach the attended session into a window of its own (SURF8) — absent when nothing is attended,
   * for the reason every other absence here is: a palette is a promise that what it lists can be
   * done, and "detach" with nothing to detach is a row that does nothing.
   */
  detach?: () => void;
}): Command[] {
  // Sessions only where a shell is — a stream never leaves the machine — and never the view already
  // in front of the person: an entry that does nothing is noise in a list whose whole value is that
  // everything in it is worth pressing.
  const list: Command[] = VIEWS
    .filter(({ view, shellOnly }) => view !== world.current && (!shellOnly || world.attached))
    .map(({ view, icon, keywords }) => ({
      id: `go.${view}`,
      icon,
      keywords,
      title: world.label(`go.${view}`),
      group: world.group('go'),
      run: () => world.go(view),
    }));

  if (world.attached) {
    list.push(
      {
        id: 'work.start',
        icon: 'plus',
        group: world.group('work'),
        title: world.label('work.start'),
        keywords: 'new session chat conversation',
        run: world.startSession,
      },
      {
        id: 'work.review',
        icon: 'diff',
        group: world.group('work'),
        title: world.label('work.review'),
        keywords: 'diff changes landed',
        run: world.review,
      },
      {
        id: 'work.monitor',
        icon: 'monitor',
        group: world.group('work'),
        title: world.label('work.monitor'),
        keywords: 'second screen window watch live monitor',
        run: world.monitor,
      },
      {
        id: 'work.browser',
        icon: 'browser',
        group: world.group('work'),
        title: world.label('work.browser'),
        keywords: 'browser web page sign in login ticket chrome jira',
        run: world.browser,
      },
      {
        id: 'work.help',
        icon: 'help',
        group: world.group('work'),
        title: world.label('work.help'),
        keywords: 'help ask daoris setup configure how f1 问道衍 帮助',
        run: world.help,
      },
      {
        id: 'work.quickAsk',
        icon: 'help',
        group: world.group('work'),
        title: world.label('work.quickAsk'),
        keywords: 'quick ask chat question daoris 快速 提问 问道衍',
        run: world.quickAsk,
      },
    );

    // Only where there is something to detach.
    if (world.detach) {
      list.push({
        id: 'work.detach',
        icon: 'external',
        group: world.group('work'),
        title: world.label('work.detach'),
        keywords: 'window pop out detach second screen',
        run: world.detach,
      });
    }
  }

  list.push(
    {
      id: 'do.ask',
      icon: 'plus',
      group: world.group('do'),
      title: world.label('do.ask'),
      keywords: 'ask request ticket intake circle workspace new quest 请求',
      run: world.ask,
    },
    {
      id: 'do.refresh',
      icon: 'refresh',
      group: world.group('do'),
      title: world.label('do.refresh'),
      keywords: 'reindex rescan',
      run: world.refresh,
    },
    {
      id: 'do.language',
      icon: 'languages',
      group: world.group('do'),
      title: world.label('do.language'),
      keywords: '中文 chinese english language',
      run: world.toggleLanguage,
    },
  );

  return list;
}

/**
 * Which commands match what was typed.
 *
 * @remarks
 * **Subsequence, not substring.** "oq" finds *Open quests* and "cnv" finds *Convergence* — the
 * matching every palette in the reference class does, and the reason people stop reading the list at
 * all. Ranked so a match at a word boundary beats one buried mid-word, because "se" should offer
 * *Search* before *Convergence*.
 *
 * Empty input is not a filter: it is "show me everything", which is what a palette opened by someone
 * who does not yet know what they want is for.
 */
export function matching(all: Command[], typed: string): Command[] {
  const needle = typed.trim().toLowerCase();
  if (!needle) return all;

  return all
    .map((command) => ({ command, score: score(`${command.title} ${command.keywords ?? ''}`, needle) }))
    .filter((hit) => hit.score !== null)
    .sort((a, b) => a.score! - b.score!)
    .map((hit) => hit.command);
}

/**
 * How well a haystack matches, lower being better — or null for no match at all.
 *
 * The score is the index of the last matched character, penalised for every match that did not begin
 * a word. So an exact prefix wins, initials win next, and a scatter across the middle comes last.
 */
function score(haystack: string, needle: string): number | null {
  const text = haystack.toLowerCase();
  let at = 0;
  let penalty = 0;

  for (const character of needle) {
    const found = text.indexOf(character, at);
    if (found === -1) return null;
    // A character that starts a word is what a person meant by typing initials.
    const boundary = found === 0 || /[\s./-]/.test(text[found - 1]);
    if (!boundary) penalty += 1;
    at = found + 1;
  }

  return at + penalty * 2;
}
