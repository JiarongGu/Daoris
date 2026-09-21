import type { IconName } from './ui';

// The command registry (SURF9). The palette is the widget; THIS is the expensive half — every action
// addressable by name — and it is a pure function so that "what can I do right now" is a value a test
// can assert rather than a screen somebody has to arrange.

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
 * what to run. That is what makes every combination — a browser, a shell in Manage, a shell in Work
 * with something parked — reachable by passing an argument.
 */
export function commands(world: {
  /** Titles, already translated, keyed by command id. The caller owns the catalogue. */
  label: (id: string) => string;
  /** Group headings, already translated. */
  group: (id: 'go' | 'do' | 'work') => string;
  /** Whether a shell is here. False is a browser: no Work, no Machine, no session controls. */
  attached: boolean;
  /** Which frame is current, so the switch offers the other one. */
  mode: 'manage' | 'work';
  /** How many things are waiting on a person — shown on the command that goes to them. */
  waiting: number;
  go: (tab: 'overview' | 'quests' | 'projects' | 'convergence' | 'search' | 'settings') => void;
  setMode: (mode: 'manage' | 'work') => void;
  refresh: () => void;
  toggleLanguage: () => void;
  /** Ask the Work frame to open its start-a-session form. Shell-only, like the frame. */
  startSession: () => void;
  /** Ask the Work frame to show the review of whatever is attended. Shell-only. */
  review: () => void;
  /** Open the monitor window on a second screen (SURF8). Shell-only: a page cannot open a window. */
  monitor: () => void;
  /**
   * Detach the attended session into a window of its own (SURF8) — absent when nothing is attended,
   * for the reason every other absence here is: a palette is a promise that what it lists can be
   * done, and "detach" with nothing to detach is a row that does nothing.
   */
  detach?: () => void;
}): Command[] {
  const domains: { id: string; icon: IconName; run: () => void }[] = [
    { id: 'go.overview', icon: 'overview', run: () => world.go('overview') },
    { id: 'go.quests', icon: 'quests', run: () => world.go('quests') },
    { id: 'go.projects', icon: 'projects', run: () => world.go('projects') },
    { id: 'go.convergence', icon: 'convergence', run: () => world.go('convergence') },
    { id: 'go.search', icon: 'search', run: () => world.go('search') },
  ];

  const list: Command[] = domains.map((command) => ({
    ...command,
    title: world.label(command.id),
    group: world.group('go'),
  }));

  if (world.attached) {
    // The machine's own wiring: absent in a browser because the service has no route onto it and an
    // empty view would imply one exists.
    list.push({
      id: 'go.settings',
      icon: 'settings',
      group: world.group('go'),
      title: world.label('go.settings'),
      keywords: 'settings machine remote harness',
      run: () => world.go('settings'),
    });

    // The other frame. Only ever the one you are not in — an entry that does nothing is noise in a
    // list whose whole value is that everything in it is worth pressing.
    const other = world.mode === 'work' ? 'manage' : 'work';
    list.push({
      id: `go.${other}`,
      icon: other === 'work' ? 'diff' : 'overview',
      group: world.group('go'),
      title: world.label(`go.${other}`),
      run: () => world.setMode(other),
    });

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
