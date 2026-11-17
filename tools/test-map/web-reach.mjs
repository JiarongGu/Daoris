/**
 * What the census of test units (`tools/test-map.mjs`) reads of the page's sources beyond their imports (WEBREACH1), so a change
 * in one domain reaches that domain's tests and a word's change the tests of the pages that say it, not the whole run.
 *
 * - **A re-export by the names taken.** A module's `export … from` is followed by its importers, each for the names it takes,
 *   not by the module itself: an importer of the bridge's barrel (`bridge/shell.ts`, re-exports only) or of the design kit
 *   (`design/ui.tsx`, its own components and its atoms re-exported) reaches the module and, of what it re-exports, the modules
 *   that hand the names it takes. An importer that takes a module whole (a namespace, a side effect, a mock, a dynamic import)
 *   or a name none is found to hand takes every re-export. Loading a barrel loads every member, so a member that fails to load
 *   fails every importer: the units that take its names see it, and so does the barrel's own test, which takes it whole
 *   (`bridge/bridge.test.ts`).
 * - **The catalogues by key.** A catalogue (`src/locales/<language>/<area>.json`) is data the page reads by key: a source reaches
 *   the catalogues, in every language, of each area whose key or dotted prefix it names (`'agents.list.open'`, a template's
 *   `` `work.${…}` `` naming every `work.*` area). The index that globs them hands them whole to an importer taking a name other
 *   than its bare ones (`en`, `zh`, `catalogues`, `areas`: the parity and catalogue suites, a reader that walks one), unless the
 *   importer declares `// test-map: reads the catalogues by key — <why>`: the i18n runtime, whose readers each read a word by its
 *   key, and a reader whose every key or prefix is a string of its own.
 * - **A dot names no folder.** A page source's bare `'.'` or `'..'` is a key's separator or a step from the process's folder (the
 *   page's tests read the disk from `process.cwd()`), never the folder the source sits in.
 */
import { exportsOf } from './ts-facts.mjs';

/** The page's catalogues and the index that loads them, as the census reads them. */
export const WORDS = Object.freeze({
  catalogue: /^src\/Daoris\.Web\/src\/locales\/[^/]+\/(.+)\.json$/,
  glob: 'src/Daoris.Web/src/locales/*/*.json',
  index: 'src/Daoris.Web/src/locales/index.ts',
  /** The index's names that hand no catalogue's words: the languages present are its folders, and a key's home its name. */
  bare: Object.freeze(['languages', 'homeOf']),
});

/** A source that takes the catalogues whole but reads each word by a key or prefix its strings name declares it: the i18n runtime does. */
const BY_KEY = /^\s*\/\/\s*test-map:\s*reads the catalogues by key\b/m;

const PAGE = /^src\/Daoris\.Web\/src\//;
const KEY = /^[A-Za-z][\w-]*(?:\.[\w-]+)*\.?$/;

/** The page's reach over a tree: `resolve(from, spec)` is the census's import resolution. */
export function webReach(files, read, resolve) {
  const byArea = new Map();
  for (const file of files) {
    const area = WORDS.catalogue.exec(file)?.[1];
    if (area) byArea.set(area, [...(byArea.get(area) ?? []), file]);
  }
  // Each dotted prefix of an area's name, with the areas at or below it.
  const under = new Map();
  for (const area of byArea.keys()) {
    const parts = area.split('.');
    for (let at = 1; at <= parts.length; at += 1) {
      const prefix = parts.slice(0, at).join('.');
      under.set(prefix, [...(under.get(prefix) ?? []), area]);
    }
  }
  const exported = new Map();
  const exportsFor = (file) => {
    if (!exported.has(file)) exported.set(file, exportsOf(read(file)));
    return exported.get(file);
  };

  /** The catalogue files a page source's strings name by key: a key's home (its longest dotted prefix with an area) and every area below a prefix. */
  const catalogues = (file, strings) => {
    const out = new Set();
    if (!PAGE.test(file)) return out;
    for (const value of strings) {
      if (!value.includes('.') || !KEY.test(value)) continue;
      const trimmed = value.replace(/\.$/, '');
      const parts = trimmed.split('.');
      for (let at = parts.length; at >= 1; at -= 1) {
        const home = byArea.get(parts.slice(0, at).join('.'));
        if (home) {
          home.forEach((each) => out.add(each));
          break;
        }
      }
      for (const area of under.get(trimmed) ?? []) byArea.get(area).forEach((each) => out.add(each));
    }
    return out;
  };

  /** The modules `module` re-exports from, resolved. */
  const sources = (module) => exportsFor(module).from.map((each) => resolve(module, each.spec)).filter(Boolean);

  /**
   * The modules that hand `name` out of `module`: itself where it declares the name, else itself and the modules of the re-export
   * that hands it, followed down; null where none is found.
   */
  const holders = (module, name, seen) => {
    if (seen.has(`${module}\0${name}`)) return null;
    seen.add(`${module}\0${name}`);
    const facts = exportsFor(module);
    if (facts.own.includes(name)) return [module];
    for (const each of facts.from) {
      const renamed = each.names === null ? name : each.names.find((entry) => entry.name === name)?.source;
      const target = renamed === undefined ? null : resolve(module, each.spec);
      const found = target && holders(target, renamed, seen);
      if (found) return [module, ...found];
    }
    return null;
  };

  return {
    catalogues,
    /** Whether a bare dot of a page source is read as no folder. */
    dotIsNoFolder: (file, value) => PAGE.test(file) && (value === '.' || value === '..'),
    /** Whether a glob of `file` is the index's catalogue glob, which hands its importers the catalogues by key (`through`). */
    keyedGlob: (file, glob) => file === WORDS.index && glob === WORDS.glob,
    /** What an import of `target` taking `names` (null: whole) from `importer` reaches: the modules followed, the files read, the globs. */
    through(importer, target, names) {
      if (target === WORDS.index) {
        const whole = (names === null || names.some((name) => !WORDS.bare.includes(name))) && !BY_KEY.test(read(importer));
        // Each catalogue named as well as globbed, so a reader of them all is one read from each, as near as a reader by key.
        return { imports: [target], files: whole ? [...byArea.values()].flat() : [], globs: whole ? [WORDS.glob] : [] };
      }
      const imports = new Set([target]);
      const whole = (module) => {
        imports.add(module);
        for (const each of sources(module)) if (!imports.has(each)) whole(each);
      };
      let all = names === null;
      for (const name of names ?? []) {
        const found = holders(target, name, new Set());
        if (!found) all = true;
        else found.forEach((each) => imports.add(each));
      }
      if (all) whole(target);
      return { imports: [...imports], files: [], globs: [] };
    },
  };
}
