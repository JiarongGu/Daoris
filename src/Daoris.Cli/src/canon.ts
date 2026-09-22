import { existsSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { listFiles, listMarkdown, readText } from './fsx.ts';
import { DaorisError } from './errors.ts';
import type { Canon, CanonFile, Pack } from './types.ts';

/**
 * The canon's own vocabulary for what a document IS: always-loaded, read-on-demand, or invoked by
 * name. Where each one lands on disk is the harness's business (src/harness.ts), which is why this
 * list describes doctrine rather than directories — and why there is still no `tier` field (D7).
 */
const TIERS = ['rules', 'knowledge', 'skills'];

/**
 * The canon ships INSIDE the package, so the pinned ref in a repo's manifest is
 * itself the version pin and no command ever fetches anything (D11). That is the
 * published layout, staged at pack time by tools/stage-package.mjs.
 *
 * In THIS repository the canon lives at the root instead, because it is data the
 * whole project shares rather than the CLI's private asset — so a development
 * checkout falls back to the workspace root. The published case is checked first,
 * so an installed package can never accidentally resolve someone else's tree.
 *
 * DAORIS_CANON overrides both; it is how the tests drive a fixture canon.
 */
export function resolveCanonRoot(packageRoot: string): string {
  if (process.env.DAORIS_CANON) return process.env.DAORIS_CANON;

  // INSTALLED: the canon ships beside the package and is the only one that exists. Deciding by
  // `node_modules` rather than by "does a canon happen to be here" matters, because `prepack` stages
  // a copy into the source tree and a leftover would otherwise SHADOW the real one — gitignored,
  // silent, and stale from whenever the last pack ran. Two copies with one quietly winning is the
  // exact failure this tool exists to prevent, so it must not be possible in the tool itself.
  if (packageRoot.replace(/\\/g, '/').includes('/node_modules/')) {
    return join(packageRoot, 'canon');
  }

  // DEVELOPMENT: the workspace root holds the canon, because it is data the whole project shares.
  return join(packageRoot, '..', '..', 'canon');
}

/**
 * Every tier of one pack, as source -> target pairs. Core is laid out exactly
 * like a pack, so this reads both:
 *
 *   core/rules/<f>.md                  -> rules/<f>.md      (always installed)
 *   core/skills/<n>/SKILL.md           -> skills/<n>/SKILL.md
 *   packs/<name>/rules/<f>.md          -> rules/<f>.md
 *   packs/<name>/knowledge/<f>.md      -> knowledge/<f>.md
 *   packs/<name>/skills/<n>/SKILL.md   -> skills/<n>/SKILL.md
 *
 * The listing is recursive, so a skill's directory comes along with it and the
 * skills tier needs no special case.
 */
function tierFiles(canonRoot: string, pack: string, prefix: string): CanonFile[] {
  const files: CanonFile[] = [];
  for (const tier of TIERS) {
    // A skill is a directory, not a document: the platform lets it carry a
    // reference doc, a template, or a script it invokes through its own
    // directory variable. Shipping only the SKILL.md would install a skill
    // whose first step runs a file that never arrived.
    const list = tier === 'skills' ? listFiles : listMarkdown;
    for (const file of list(join(canonRoot, prefix, tier))) {
      files.push({ pack, source: `${prefix}/${tier}/${file}`, target: `${tier}/${file}` });
    }
  }
  return files;
}

/**
 * The pack contract this build speaks (PLUG1).
 *
 * @remarks
 * 🔴 **A pack declares the canon it was written against, and the host reads it BEFORE materializing
 * anything.** Taken from a neighbouring application's plugin manifests, which have carried an
 * integer `apiVersion` all along. Without it a pack from a newer canon failed at whatever it
 * happened to touch first — a frontmatter field that did not exist yet, a tier that is now a region,
 * a skill layout that moved — and every one of those is a confusing error about the wrong thing.
 *
 * **Raise this when a pack written for the new shape cannot work on the old one**, never for an
 * addition: a number that goes up on every change teaches people to ignore it.
 *
 * 1 — the shape as of D59: `rules/` into the always-loaded region, `knowledge/` and `skills/` as
 * directories, frontmatter of `name`, `applies_when`, `enforces`.
 */
export const PACK_API = 1;

export function readCanon(canonRoot: string): Canon {
  if (!existsSync(canonRoot)) throw new DaorisError(`no canon at '${canonRoot}'`);
  const version = JSON.parse(readText(join(canonRoot, 'canon.json'))).version;
  const packs = new Map<string, Pack>();

  packs.set('core', {
    name: 'core',
    description: 'Universal workflow rules and discovery skills every repo gets.',
    // Core is not a pack somebody wrote against a contract — it IS the contract, and ships with the
    // build that speaks it.
    api: PACK_API,
    files: tierFiles(canonRoot, 'core', 'core'),
  });

  const packsDir = join(canonRoot, 'packs');
  if (existsSync(packsDir)) {
    for (const entry of readdirSync(packsDir, { withFileTypes: true })) {
      if (!entry.isDirectory()) continue;
      const manifest = JSON.parse(readText(join(packsDir, entry.name, 'pack.json')));

      // Absent is 1, so every pack written before the field keeps working — the field is how a pack
      // opts into SAYING something, never a wall in front of one that never spoke.
      const declared = manifest.apiVersion ?? PACK_API;
      if (typeof declared !== 'number' || !Number.isInteger(declared) || declared < 1) {
        throw new DaorisError(
          `pack '${entry.name}' declares apiVersion ${JSON.stringify(manifest.apiVersion)}, which is `
          + 'not a whole number. It is the canon contract the pack was written against — a version, '
          + 'not a name.');
      }

      // 🔴 Refused here, before a single file is planned: the whole value is failing at the manifest
      // rather than three steps later at whatever the new shape happened to touch first. Both
      // numbers named, because "incompatible" alone sends a person to guess which side is behind.
      if (declared > PACK_API) {
        throw new DaorisError(
          `pack '${entry.name}' needs canon api ${declared} and this build speaks ${PACK_API}. `
          + 'Upgrade daoris, or use a version of the pack written for this canon.');
      }

      packs.set(entry.name, {
        name: entry.name,
        description: manifest.description,
        api: declared,
        files: tierFiles(canonRoot, entry.name, `packs/${entry.name}`),
      });
    }
  }
  return { version, root: canonRoot, packs };
}

/** Core is never opt-in. Sorted by target so plans and locks are stable. */
export function selectFiles(canon: Canon, packNames: readonly string[]): CanonFile[] {
  const selected = ['core', ...packNames.filter((name) => name !== 'core')];
  const files: CanonFile[] = [];
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
