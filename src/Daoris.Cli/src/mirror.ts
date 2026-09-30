// The skills mirror (LAYOUT3, D117 §3.2 and §5.4's mirror table).
//
// Where the reference links `.claude/skills` to `.agents/skills`, `sync` writes a copy of every skill
// under the target's skills folder into the mirror root, canonical and the repository's own alike,
// because the harness that reads only there needs both. A link is not an option: on a checkout with
// `core.symlinks=false` it is a text file holding a path, and that harness lists no skills (LAYOUT2).
//
// 🔴 **Measured against the lock (D13), exactly as a canonical file is.** A mirror that differs from
// what the lock recorded was edited HERE; one that matches the lock while its source moved on is only
// behind, and `sync` renews it. A mirror of the repository's own skill is still Daoris's file — D5 is
// about what Daoris writes, and Daoris wrote the mirror.

import type { Harness, LockEntry, MirrorEntry, MirrorPlan } from './types.ts';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { digestBytes, listFiles, normalize, sha256 } from './fsx.ts';
import { stripHeader, withHeader } from './document.ts';
import { isFile } from './layout.ts';

/** One file a mirror copies: where from, where to, and what the copy holds. */
export interface MirrorSource {
  /** The copy, relative to the repository root. */
  path: string;
  /** The source, relative to the repository root. */
  of: string;
  /** The source's tier path — `skills/<name>/<file>` — which is how the lock names a canonical one. */
  target: string;
  /** True when the skill it belongs to is canonical, so an edit to it can be promoted. */
  canonical: boolean;
  content: string | Buffer;
}

/**
 * The one line a mirror's entry file carries, under its frontmatter (D14): the source, and what to do.
 *
 * @remarks
 * It opens like every provenance line, so `stripHeader` takes it off on the way to the canon, and it
 * REPLACES the source's provenance line rather than sitting beside it: a reader needs one instruction,
 * and it is to edit the other file.
 */
export function mirrorHeader(of: string, root: string, canonical: boolean): string {
  return `<!-- daoris: mirror of ${of} for agents that read only ${root} — edit that file, not this`
    + `${canonical ? ', and promote an edit with `daoris upstream`' : ''}; daoris sync rewrites this one -->`;
}

/** The hash the lock records for a mirror's content — text as text, bytes as `digestBytes` reads them. */
export const mirrorDigest = (content: string | Buffer): string =>
  typeof content === 'string' ? sha256(content) : digestBytes(content);

/**
 * Every file the mirror copies, and what each copy holds.
 *
 * @param canonical Canonical files by tier path, with the content they are about to have — `sync`
 * passes what it is writing, so a canon update renews the copy in the same run, and `check` passes what
 * is on disk.
 * @param owned Whether a tier path is Daoris's own (in the lock, or selected): never read as the
 * repository's, even when a file of that name sits on disk mid-move.
 */
export function mirrorSources(
  { root, harness, target, canonical, owned }:
  {
    root: string; harness: Harness; target: string;
    canonical: ReadonlyMap<string, string | Buffer>; owned: (tierPath: string) => boolean;
  },
): MirrorSource[] {
  const mirror = harness.mirror;
  const tier = mirror ? harness.tiers[mirror.tier] : undefined;
  if (!mirror || !tier?.dir || !tier.entryFile) return [];
  const dir = tier.dir;
  const entryFile = tier.entryFile;

  // Relative to the tier's folder: `<skill>/<file>`.
  const files = new Map<string, { content: string | Buffer; canonical: boolean }>();
  for (const [path, content] of canonical) {
    if (path.startsWith(`${dir}/`)) files.set(path.slice(dir.length + 1), { content, canonical: true });
  }
  for (const rel of listFiles(join(root, target, dir))) {
    if (files.has(rel) || owned(`${dir}/${rel}`)) continue;
    files.set(rel, { content: readFileSync(join(root, target, dir, rel)), canonical: false });
  }

  const sources: MirrorSource[] = [];
  for (const [rel, file] of files) {
    const [skill, ...rest] = rel.split('/');
    // A skill is a folder with its entry file; a file beside the skills, or in a folder with none, is not one.
    const entry = files.get(`${skill}/${entryFile}`);
    if (!skill || !rest.length || !entry) continue;
    // Per-agent metadata another agent writes inside the skill belongs to that agent (D117 §3.2).
    if (rest.length > 1 && mirror.skip.includes(rest[0]!)) continue;

    const of = `${target}/${dir}/${rel}`;
    const isEntry = rest.length === 1 && rest[0] === entryFile;
    const text = (content: string | Buffer) => normalize(typeof content === 'string' ? content : content.toString('utf8'));
    sources.push({
      path: `${mirror.root}/${rel}`,
      of,
      target: `${dir}/${rel}`,
      canonical: entry.canonical,
      // Every other file is a copy as it stands: a script's first line may be a shebang, and a template may
      // be in a format no comment fits.
      content: isEntry ? withHeader(mirrorHeader(of, mirror.root, entry.canonical), stripHeader(text(file.content))) : file.content,
    });
  }
  return sources.sort((a, b) => a.path.localeCompare(b.path));
}

/** What sits at a mirror path now, as the lock would hash it, or null when nothing (or a folder) does. */
function onDisk(root: string, path: string): string | null {
  const abs = join(root, path);
  return isFile(abs) ? digestBytes(readFileSync(abs)) : null;
}

/**
 * The mirror table of D117 §5.4, decided for every mirror path — the ones the sources want and the ones
 * the lock recorded.
 *
 * @param vacating Paths an old canonical file holds that this sync deletes (a move from a root the
 * mirror now writes to): read as absent, since the file there is Daoris's and on its way out. The move's
 * own cells speak for an edited one.
 * @param editedSources Source paths whose canonical file drifted, so an edited mirror of one is refused
 * naming both.
 */
export function planMirrors(
  { root, sources, locked, vacating, editedSources }:
  {
    root: string; sources: readonly MirrorSource[]; locked: readonly MirrorEntry[];
    vacating: ReadonlySet<string>; editedSources: ReadonlySet<string>;
  },
): MirrorPlan {
  const recorded = new Map(locked.map((entry) => [entry.path, entry]));
  const plan: MirrorPlan = { writes: [], retire: [], drop: [], edited: [], collisions: [], editedGone: [] };

  for (const source of sources) {
    const want = mirrorDigest(source.content);
    const held = vacating.has(source.path) ? null : onDisk(root, source.path);
    const entry = recorded.get(source.path);
    let state: 'create' | 'update' | 'unchanged';

    if (held === null) {
      // Absent: created, or recreated when the lock had it (`check` reports that one missing first).
      state = 'create';
    } else if (entry) {
      if (held === entry.sha256) state = held === want ? 'unchanged' : 'update';
      // Differs from the lock and is what would be written: the edit already reached the source.
      else if (held === want) state = 'unchanged';
      else {
        plan.edited.push({
          path: source.path, of: source.of, canonical: source.canonical, sourceEdited: editedSources.has(source.of),
        });
        state = 'update';
      }
    } else if (held === want) {
      state = 'unchanged';
    } else {
      // The repository's own file at a mirror path: a skill it keeps for that one agent, or one not moved.
      plan.collisions.push(source.path);
      state = 'update';
    }

    plan.writes.push({ path: source.path, of: source.of, content: source.content, sha256: want, state });
  }

  const wanted = new Set(sources.map((source) => source.path));
  for (const entry of locked) {
    if (wanted.has(entry.path)) continue;
    const held = onDisk(root, entry.path);
    if (held === null) plan.drop.push(entry.path);
    else if (held === entry.sha256) plan.retire.push(entry.path);
    // Nothing can receive the edit but a copy aside: the skill it mirrored is gone.
    else plan.editedGone.push(entry.path);
  }
  return plan;
}

/** The mirror entries a canonical lock entry's disk content implies, for `check` (offline, canon-free). */
export function canonicalOnDisk(
  root: string, target: string, entries: readonly LockEntry[],
): Map<string, Buffer> {
  const files = new Map<string, Buffer>();
  for (const entry of entries) {
    if (entry.in) continue;
    const abs = join(root, target, entry.target);
    if (isFile(abs)) files.set(entry.target, readFileSync(abs));
  }
  return files;
}
