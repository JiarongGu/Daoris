/**
 * The catalogues, one file per area (MOD2): `locales/<language>/<area>.json`, merged here at load, so
 * i18next sees one flat catalogue per language, exactly the resources it saw when each language was a
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

/** An area's name is its file's name: `./en/settings.rules.json` is the area `settings.rules`. */
const byArea = (files: Record<string, Catalogue>): Record<string, Catalogue> =>
  Object.fromEntries(Object.entries(files).map(([path, keys]) => [path.replace(/^.*\/(.+)\.json$/, '$1'), keys]));

/** Each language's areas by name, as the files hold them — what the parity tests read. */
export const areas: { en: Record<string, Catalogue>; zh: Record<string, Catalogue> } = {
  en: byArea(import.meta.glob<Catalogue>('./en/*.json', { eager: true, import: 'default' })),
  zh: byArea(import.meta.glob<Catalogue>('./zh/*.json', { eager: true, import: 'default' })),
};

const merged = (language: Record<string, Catalogue>): Catalogue => Object.assign({}, ...Object.values(language));

/** English, every area merged into the one flat catalogue i18next is given. */
export const en: Catalogue = merged(areas.en);
/** 中文, the same. */
export const zh: Catalogue = merged(areas.zh);

/** The area a key belongs in: the longest of `names` that is the key or a dotted prefix of it, else null. */
export function homeOf(key: string, names: readonly string[]): string | null {
  const holding = names.filter((name) => key === name || key.startsWith(`${name}.`));
  return holding.sort((a, b) => b.length - a.length)[0] ?? null;
}
