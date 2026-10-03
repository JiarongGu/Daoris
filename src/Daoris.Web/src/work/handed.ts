import type { TFunction } from 'i18next';
import { figure, moment } from '../format';
import type { HandedCut, HandedSection, InstructionAccount } from './conversation';

/**
 * What a session was handed, as the page reads it (CONTEXT1, D143 point 1): the account the driver kept beside its
 * instruction, worded from its codes in the reader's language. Pure, so every state a story or a test needs is an argument.
 *
 * @remarks
 * **The page's map of the codes is the twin's third side**, as LANG1b's note codes are: `HANDED_CUTS` names the values each
 * cut's entry says, held to the driver's `HandedCuts.Values` by `handed.test.ts`, which also holds every
 * `work.handed.(section|source|none|cut).*` entry to the codes the driver declares. **What the page cannot word**, a code a
 * newer driver writes or a value its entry needs that the record does not give, shows the driver's own English, marked
 * *shown as recorded*.
 */

/** The values each cut's entry says: `n` and `limit` counts, `from` and `to` moments — the driver's `HandedCuts.Values`. */
export const HANDED_CUTS: Readonly<Record<string, readonly CutValue[]>> = {
  'requirements-bound': ['n', 'limit'],
  'words-older': ['n', 'limit', 'from', 'to'],
  'words-long': ['n', 'limit'],
  'words-unread': [],
  'words-not-kept': ['from'],
  'go-aheads-bound': ['n', 'limit'],
  'acts-long': ['n', 'limit'],
  'standing-long': ['n', 'limit'],
  'plan-bound': ['n', 'limit'],
  'indexes-bound': ['n', 'limit'],
  'files-elsewhere': ['n'],
  'rules-unread': [],
};

type CutValue = 'n' | 'limit' | 'from' | 'to';

/** A line the page shows: worded from its codes, or the driver's English where it could not be, marked. */
export type HandedLine = { text: string; recorded: boolean };

/** A section handed, as one row: its name, its size, where it came from, its facts, and what its bound left out. */
export type HandedRow = {
  key: string;
  name: HandedLine;
  /** Its characters, or for the rules handed beside it how many; absent where the record gives neither. */
  size?: string;
  /** Where it came from, worded; absent where the row is the driver's English whole. */
  source?: string;
  /** What its source names, shown as code: `#abc123`, `reports`, `docs/code-map.json`. */
  from?: string;
  /** Its other facts, worded: how many of how many, when its source was set. */
  facts: string[];
  cuts: HandedLine[];
  /** Handed beside the instruction rather than in it: the permission rules. */
  beside: boolean;
};

export type HandedView = {
  rows: HandedRow[];
  /** Each section not handed, with why. */
  absent: HandedLine[];
  /** The instruction's characters, as the record counts them. */
  chars: number;
  /** How many sections of the instruction were handed. */
  sections: number;
  /** How many sections had a part left out. */
  cut: number;
};

/** The sources whose `from` is an id a reader knows by its mark: a quest's, an ask's. */
const MARKED = new Set(['quest', 'ask']);

/**
 * The account as rows to draw, in the reader's language. `exists` says whether the catalogue words a key, so a code this page
 * does not know shows as its record keeps it.
 */
export function handedView(
  t: TFunction, exists: (key: string) => boolean, account: InstructionAccount, language: string,
): HandedView {
  const handed = account.sections.filter((section) => !section.none);
  const rows = handed.map((section, index) => row(t, exists, section, index, language));
  const absent = account.sections.filter((section) => section.none).map((section) => notHanded(t, exists, section));
  return {
    rows,
    absent,
    chars: account.chars,
    sections: handed.filter((section) => typeof section.chars === 'number').length,
    cut: handed.filter((section) => (section.cuts?.length ?? 0) > 0).length,
  };
}

const recorded = (text: string): HandedLine => ({ text, recorded: true });

function row(t: TFunction, exists: (key: string) => boolean, section: HandedSection, index: number, language: string): HandedRow {
  const key = `${index}:${section.name}`;
  const nameKey = `work.handed.section.${section.name}`;
  const sourceKey = `work.handed.source.${section.source}`;
  const beside = typeof section.chars !== 'number';
  // A section or a source this page cannot word is the driver's sentence whole: half worded would read as a fact it is not.
  if (!exists(nameKey) || !exists(sourceKey)) {
    return { key, name: recorded(section.said), facts: [], cuts: [], beside };
  }

  const facts: string[] = [];
  if (typeof section.shown === 'number' && typeof section.of === 'number') {
    facts.push(t('work.handed.items', { shown: figure(section.shown), of: figure(section.of) }));
  }
  if (section.at) facts.push(t('work.handed.set', { at: moment(section.at, language) }));

  return {
    key,
    name: { text: t(nameKey), recorded: false },
    size: typeof section.chars === 'number' ? figure(section.chars)
      : typeof section.shown === 'number' ? t('work.handed.rules', { count: section.shown })
        : undefined,
    source: t(sourceKey),
    ...(section.from ? { from: MARKED.has(section.source) ? `#${section.from}` : section.from } : {}),
    facts,
    cuts: (section.cuts ?? []).map((cut) => cutLine(t, exists, cut, language)),
    beside,
  };
}

/** What a bound left out, worded with its values, or as recorded where the entry or a value it needs is missing. */
function cutLine(t: TFunction, exists: (key: string) => boolean, cut: HandedCut, language: string): HandedLine {
  const key = `work.handed.cut.${cut.code}`;
  const needs = HANDED_CUTS[cut.code];
  if (!needs || !exists(key)) return recorded(cut.said);

  const values: Record<CutValue, string | undefined> = {
    n: typeof cut.count === 'number' ? figure(cut.count) : undefined,
    limit: typeof cut.limit === 'number' ? figure(cut.limit) : undefined,
    from: cut.from ? moment(cut.from, language) : undefined,
    to: cut.to ? moment(cut.to, language) : undefined,
  };
  if (needs.some((name) => values[name] === undefined)) return recorded(cut.said);
  return { text: t(key, values), recorded: false };
}

/** A section not handed: its name and why, or as recorded where either is a code this page does not know. */
function notHanded(t: TFunction, exists: (key: string) => boolean, section: HandedSection): HandedLine {
  const nameKey = `work.handed.section.${section.name}`;
  const whyKey = `work.handed.none.${section.none}`;
  if (!exists(nameKey) || !exists(whyKey)) return recorded(section.said);
  return { text: t('work.handed.notHandedItem', { name: t(nameKey), why: t(whyKey) }), recorded: false };
}
