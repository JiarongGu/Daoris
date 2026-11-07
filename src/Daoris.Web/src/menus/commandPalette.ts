import type { IconName } from '../design/ui';
import { type CommandEntry, MENUS, type MenuId, type Translate } from './commands';

/** A named thing a person can do, as the palette lists it. */
export type Command = {
  /** Stable, structural, and never shown: the id is what a test and the log name. */
  id: string;
  /** What the person reads. Already translated by the caller. */
  title: string;
  /** The menu it is in, so a list of sixty is still scannable. */
  group: string;
  icon: IconName;
  /** Extra words that should MATCH but are not shown — "settings" finding Machine, and 中文. */
  keywords?: string;
  run: () => void;
};

/**
 * What the palette lists now: every row in its rule that applies and is not the choice in force, under its
 * menu's name, in the menus' order.
 *
 * @remarks
 * **A row is absent where its surface is absent, and where it does not apply** — never present-and-disabled, as the menus
 * keep it: a palette is a promise that what it lists can be done, and its order is by match, so a place learned there
 * means nothing. The disclosure boundary is therefore kept by omission, and Playwright asserts the absence.
 */
export function paletteCommands(entries: readonly CommandEntry[], t: Translate = (key) => key): Command[] {
  const menuName = (menu: MenuId) => t(MENUS.find((each) => each.id === menu)!.label);
  return entries.filter((entry) => entry.palette).map((entry) => ({
    id: entry.id,
    title: entry.title,
    group: menuName(entry.menu),
    icon: entry.icon ?? 'execute',
    ...(entry.keywords ? { keywords: entry.keywords } : {}),
    run: entry.run,
  }));
}

/** Every key the window answers, by menu, in the reader's language: what *Keyboard shortcuts* prints. */
export function shortcutGroups(entries: readonly CommandEntry[], t: Translate): { menu: string; rows: { label: string; keys: string[] }[] }[] {
  return MENUS
    .map((menu) => ({
      menu: t(menu.label),
      rows: entries
        .filter((entry) => entry.menu === menu.id && entry.keys.length > 0)
        .map((entry) => ({ label: entry.label, keys: entry.keys.map((key) => key.combo) })),
    }))
    .filter((group) => group.rows.length > 0);
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
