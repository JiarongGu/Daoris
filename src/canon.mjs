import { existsSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { listMarkdown, readText } from './fsx.mjs';
import { DaorisError } from './errors.mjs';

const TIERS = ['rules', 'knowledge'];

/**
 * The canon ships INSIDE the package, so the pinned ref in a repo's manifest is
 * itself the version pin and no command ever fetches anything. DAORIS_CANON
 * overrides the root for developing Daoris itself.
 */
export function resolveCanonRoot(packageRoot) {
  return process.env.DAORIS_CANON || join(packageRoot, 'canon');
}

/**
 * The layout is the contract, and the directory is the tier:
 *
 *   core/<f>.md                  -> rules/<f>.md      (always installed)
 *   packs/<name>/rules/<f>.md    -> rules/<f>.md
 *   packs/<name>/knowledge/<f>.md-> knowledge/<f>.md
 *
 * There is deliberately no `tier` field anywhere — the harness decides the tier
 * by path (it auto-loads rules/ and not knowledge/), so a second source of
 * truth would only be something to disagree with.
 */
export function readCanon(canonRoot) {
  if (!existsSync(canonRoot)) throw new DaorisError(`no canon at '${canonRoot}'`);
  const version = JSON.parse(readText(join(canonRoot, 'canon.json'))).version;
  const packs = new Map();

  packs.set('core', {
    name: 'core',
    description: 'Universal workflow rules every repo gets.',
    files: listMarkdown(join(canonRoot, 'core')).map((file) => ({
      pack: 'core',
      source: `core/${file}`,
      target: `rules/${file}`,
    })),
  });

  const packsDir = join(canonRoot, 'packs');
  if (existsSync(packsDir)) {
    for (const entry of readdirSync(packsDir, { withFileTypes: true })) {
      if (!entry.isDirectory()) continue;
      const dir = join(packsDir, entry.name);
      const manifest = JSON.parse(readText(join(dir, 'pack.json')));
      const files = [];
      for (const tier of TIERS) {
        for (const file of listMarkdown(join(dir, tier))) {
          files.push({
            pack: entry.name,
            source: `packs/${entry.name}/${tier}/${file}`,
            target: `${tier}/${file}`,
          });
        }
      }
      packs.set(entry.name, { name: entry.name, description: manifest.description, files });
    }
  }
  return { version, root: canonRoot, packs };
}

/** Core is never opt-in. Sorted by target so plans and locks are stable. */
export function selectFiles(canon, packNames) {
  const selected = ['core', ...packNames.filter((name) => name !== 'core')];
  const files = [];
  for (const name of selected) {
    const pack = canon.packs.get(name);
    if (!pack) {
      const available = [...canon.packs.keys()].filter((key) => key !== 'core').sort().join(', ');
      throw new DaorisError(`unknown pack '${name}' — available: ${available || '(none)'}`);
    }
    files.push(...pack.files);
  }
  return files.sort((a, b) => a.target.localeCompare(b.target));
}
