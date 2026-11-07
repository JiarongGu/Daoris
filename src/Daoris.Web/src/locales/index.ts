/**
 * The catalogues, one file per area: `locales/<language>/<area>.json`, merged here at load, so
 * i18next sees one flat catalogue per language, exactly the resources it would see if each language were a
 * single file.
 *
 * @remarks
 * **A key's home is its prefix.** A key lives in the file named by its longest dotted prefix that has a
 * file: `settings.rules.add` in `settings.rules.json`, `settings.theme.label` in `settings.json`,
 * `nav.quests` in `nav.json`. A new first segment is a new file. An area that grows past about a hundred
 * keys is split the same way: each of its second segments with ten or more keys becomes a file of its
 * own, and the rest stay behind (`settings` and `work` were, at the split).
 *
 * One file held every string, so eleven of eighteen branches in three days edited the same two files
 * (`docs/2026-09-30-parallel-development-design.md` §1). Files found by existing, not listed, keep it
 * that way: adding an area adds a file and edits nothing else. `scripts/i18n-check.mjs` fails the build
 * when an area's two files disagree, when a key is in two files, or when a key is not in its home.
 */
export type Catalogue = Record<string, string>;

const files = import.meta.glob<Catalogue>('./*/*.json', { eager: true, import: 'default' });

/**
 * Each language's areas by name, as the files hold them — what the parity tests read. A language is a folder: the catalogues
 * present are the languages the page offers, so adding a language is adding a folder. An area's name is its file's name:
 * `./en/settings.rules.json` is the area `settings.rules` of `en`.
 */
export const areas: Record<string, Record<string, Catalogue>> = {};
for (const [path, keys] of Object.entries(files)) {
  const [, language, area] = /^\.\/([^/]+)\/(.+)\.json$/.exec(path) ?? [];
  if (language && area) (areas[language] ??= {})[area] = keys;
}

const merged = (language: Record<string, Catalogue>): Catalogue => Object.assign({}, ...Object.values(language));

/** Each language's areas merged into the one flat catalogue i18next is given. */
export const catalogues: Record<string, Catalogue> = Object.fromEntries(Object.entries(areas).map(([language, each]) => [language, merged(each)]));

/** The languages the page offers: the catalogues present, English (the source) first. */
export const languages: string[] = Object.keys(catalogues).sort((a, b) => (a === 'en' ? -1 : b === 'en' ? 1 : a.localeCompare(b)));

/** English, the source catalogue. */
export const en: Catalogue = catalogues.en!;
/** 中文, the catalogue the page ships beside it. */
export const zh: Catalogue = catalogues.zh!;

/** The area a key belongs in: the longest of `names` that is the key or a dotted prefix of it, else null. */
export function homeOf(key: string, names: readonly string[]): string | null {
  const holding = names.filter((name) => key === name || key.startsWith(`${name}.`));
  return holding.sort((a, b) => b.length - a.length)[0] ?? null;
}
