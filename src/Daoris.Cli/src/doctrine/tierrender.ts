// What goes inside the region: the always-loaded tier, rendered once.
//
// `.claude/rules/` is read by only one of three harnesses, so the tier lives in the file all three
// read. Each rule file becomes one span, and three content decisions follow from concatenating:
//
//   1. FRONTMATTER IS STRIPPED. Frontmatter is only frontmatter at byte 0; concatenated, it would put
//      `---` blocks mid-document, where a reader sees horizontal rules and stray key-value prose.
//   2. PROVENANCE STAYS, per rule file. It is what keeps drift and `upstream` per file inside one
//      region; without it a person improving one sentence has drifted every rule at once.
//   3. THE REGION LISTS PLACES, THEN THE RULES IN FULL. Every session pays for the region, so it holds
//      what every session needs: where the records are, one line pointing at `<target>/INDEX.md` (the
//      knowledge and skills, read on demand), and the rules themselves.

import type { CanonFile, Harness, LockEntry } from './records.ts';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { firstHeading, frontmatterEnd, makeHeader, parseFrontmatter, SKILL_FIELDS, stripFrontmatter } from './document.ts';
import { readText } from '../platform/fsx.ts';
import { DEFAULT_HARNESS, HARNESSES, regionIn } from '../layout/harness.ts';
import { findRegion } from './region.ts';

/** One document on its way into the region: where it came from, and what it says. */
export interface TierDocument {
  file: CanonFile;
  text: string;
  /**
   * The frontmatter, when `text` no longer carries it — a span holds its body alone, and the roster's
   * rows are built from `applies_when` and `enforces`.
   */
  meta?: Record<string, string>;
  /** True for a document this repository wrote itself — listed in the roster, never synced. */
  local?: boolean;
}

/** What the region renders: the rules, the pointer to the index, and what the manifest declares. */
export interface TierInput {
  rules: TierDocument[];
  version: string;
  /** Where the on-demand tiers live, for the pointer to their index. */
  target?: string;
  /** The core rows this repository switched off, and the pack that offered each. */
  off?: readonly { target: string; by: string }[];
  /** Where the skills live and the root that mirrors them, for the one sentence that says so. */
  mirror?: { source: string; root: string };
  /** Each declared room and its first heading. */
  rooms?: readonly { path: string; heading: string | null }[];
  /** Each declared document with a path, in the roles' order. */
  documents?: readonly { role: string; path: string }[];
}

/** What the index renders: the on-demand tiers as they are on disk. */
export interface IndexInput {
  knowledge: TierDocument[];
  skills: TierDocument[];
  /** Where the on-demand tiers live, so each row names its document from the repository's root. */
  target?: string;
}

const KNOWLEDGE_HEAD = '| Document | Applies when | Enforces |\n|---|---|---|';
const BARE_HEADING = '## Knowledge without frontmatter';
const BARE_HEAD = '| Document | Its first heading |\n|---|---|';
const SKILL_HEAD = '| Skill | Use when |\n|---|---|';
const ROOM_HEAD = '| Room | About |\n|---|---|';

/** The heading the region's places start under, from which `check` compares the roster offline. */
export const WHERE_HEADING = '## Where things are';

/** The sentence above the declared places, rendered only when the manifest declares any. */
export const DECLARED_LINE = 'Declared in `daoris.json`; look here before searching.';

/** The places list's last item, the pointer to the index of the knowledge and skills, always rendered. */
export const INDEX_ITEM = '- knowledge and skills, each with when it applies:';

/** The roles read by lookup and never to orient: one line, named the library. */
const LIBRARY: readonly string[] = ['decisions', 'archive', 'fixes'];

/** The roles whose line says more than its path. */
const NOTES: Readonly<Record<string, string>> = { gates: 'the checks, and what may run unasked' };

/** The index's file name, at the target's root beside the tiers rather than inside one. */
export const INDEX_FILE = 'INDEX.md';

/**
 * Where the index of the on-demand tiers is, repository-relative: at the target's root, where no tier
 * lists it as a document, the service does not read it, and a repository's own `knowledge/INDEX.md`
 * cannot collide with it.
 */
export const indexPath = (target: string): string => (target === '' ? INDEX_FILE : `${target}/${INDEX_FILE}`);

function metaOf(document: TierDocument, required: readonly string[] = ['applies_when', 'enforces']) {
  if (document.meta) return required.every((field) => document.meta![field]) ? document.meta : null;
  return parseFrontmatter(document.text, required).meta;
}

/**
 * A document with no frontmatter block at all, which the index lists by its first heading and `check`
 * counts. One answer for both, so the count is the table's rows.
 */
export const withoutFrontmatter = (document: TierDocument): boolean =>
  !document.meta && frontmatterEnd(document.text) === -1;

/** The roster says which rows are this repository's own, because those are never synced. */
function mark(document: TierDocument): string {
  return document.local ? ' _(local)_' : '';
}

/**
 * The first sentence, capped — a skill's `description` is the harness's TRIGGER text and is long by
 * design, so copying it whole pays for it twice. The reasoning and the measurement are in
 * `indexgen.ts`, where this roster lived before the tier moved.
 */
function summarize(description: string, limit = 110): string {
  const text = description.trim();
  if (text.length <= limit) return text;
  const cut = text.lastIndexOf(' ', limit);
  return `${text.slice(0, cut > 40 ? cut : limit).trimEnd()}…`;
}

/**
 * The region's body: the roster, then every always-loaded rule in full.
 *
 * Deterministic in its input's order — the canon's own — because a region that renders differently
 * from the same canon makes every `sync` a diff and every drift report a lie.
 */
export function renderTier(input: TierInput): string {
  return [renderRoster(input), ...renderRules(input)].join('\n');
}

/**
 * The roster half: the header, then where the records are, the pointer to the index, the mirror and
 * the rooms.
 *
 * @remarks
 * Split out because `check` rebuilds everything from {@link WHERE_HEADING} to the first rule from the
 * disk and the manifest to tell whether the region has gone stale, offline and with no canon. The
 * switched-off line sits above that heading, because only `sync` holds the lock's knowledge of who
 * switched a row off.
 *
 * 🔴 Nothing here grows with the repository's documents. The knowledge and skill tables did, and on
 * the first real set-up they put the first rule past byte 32,768 of the file, where one agent stops
 * reading. They are `renderIndex`'s, and the region carries one line naming the file, with no count in
 * it: a count would change the region with every document.
 */
export function renderRoster(input: TierInput): string {
  const target = input.target ?? HARNESSES[DEFAULT_HARNESS]!.defaultTarget;
  // 🔴 The region's own headings sit at the level the rule bodies use, and the bodies are not
  // touched: demoting every heading inside a rule has to be exactly reversible over arbitrary markdown,
  // or a rule is silently corrupted on its way back out through `upstream`.
  const lines: string[] = [
    '# Doctrine',
    '',
    'Generated from the shared canon; edit the canon, not this. `daoris reconcile` says how to keep a local edit.',
  ];

  // 🔴 What is NOT here, said here. A core rule a pack switched off is absent, and a session that meets a
  // doctrine with a hole in it cannot tell the hole from a canon that never had the rule.
  if (input.off?.length) {
    const rows = input.off.map((row) =>
      `\`${row.target.replace(/^.*\//, '').replace(/\.md$/, '')}\` (by \`${row.by}\`)`);
    lines.push('', `Switched off here, by a pack and confirmed in \`daoris.json\`: ${rows.join(', ')}.`);
  }

  // 🔴 Where the records are, as data rather than prose: the search each session would otherwise make.
  // From the manifest alone, so `check` rebuilds it offline and fails when it differs. The roles read by
  // lookup share one line, so the list itself says they are not read to orient.
  lines.push('', WHERE_HEADING, '');
  const documents = input.documents ?? [];
  if (documents.length) {
    lines.push(DECLARED_LINE, '');
    const library = documents.filter((document) => LIBRARY.includes(document.role));
    for (const document of documents) {
      if (!LIBRARY.includes(document.role)) {
        const note = NOTES[document.role];
        lines.push(`- ${document.role} \`${document.path}\`${note ? `: ${note}` : ''}`);
      } else if (document === library[library.length - 1]) {
        lines.push(`- ${library.map((each) => `${each.role} \`${each.path}\``).join(', ')}: the library`);
      }
    }
  }
  lines.push(`${INDEX_ITEM} \`${indexPath(target)}\`, searched for your kind of change`);

  // Said once, beside the pointer: the copy exists for the agent that reads only there, and an edit
  // made to it is refused, so the reader learns which file to open before editing.
  if (input.mirror) {
    lines.push('', `Skills live in \`${input.mirror.source}/\`; \`${input.mirror.root}/\` mirrors them for the agent `
      + 'that reads only there — edit the source.');
  }

  // 🔴 Telling, not loading. A room is on-demand material like knowledge, and no harness is measured
  // loading a nested file for a session started at the root, so the roster is what makes each one
  // reachable. Only when a room is declared.
  if (input.rooms?.length) {
    lines.push('', '## Rooms', '', "Read a folder's room before changing anything in it.", '', ROOM_HEAD);
    for (const room of input.rooms) {
      lines.push(`| [${room.path}](${room.path}/AGENTS.md) | ${room.heading ?? '⚠ no heading'} |`);
    }
  }

  return lines.join('\n');
}

/**
 * `<target>/INDEX.md`: the knowledge and skills, each with when it applies.
 *
 * @remarks
 * Every document is named by its path from the repository's root, as a code span rather than a link:
 * an agent opens it as written, and a renderer shows no broken link from a file one folder down. A
 * skill is named by its entry file for the same reason, since an agent whose skill roots hold nothing
 * still activates one by reading that file. Rebuilt by `check` from the disk alone, offline and
 * without the canon, so it must be a function of the files and the lock and nothing else.
 */
export function renderIndex(input: IndexInput): string {
  const target = input.target ?? HARNESSES[DEFAULT_HARNESS]!.defaultTarget;
  const at = (document: TierDocument) => `\`${target === '' ? '' : `${target}/`}${document.file.target}\`${mark(document)}`;
  const lines: string[] = [
    '# Index',
    '',
    'Generated by daoris from the files. Edit the documents, not this: `daoris sync` rewrites it, and'
    + ' `daoris check` fails when it is behind.',
    '',
    '## Knowledge',
    '',
    KNOWLEDGE_HEAD,
  ];
  const bare = input.knowledge.filter(withoutFrontmatter);
  for (const document of input.knowledge.filter((each) => !withoutFrontmatter(each))) {
    const meta = metaOf(document);
    lines.push(meta
      ? `| ${at(document)} | ${meta.applies_when} | ${meta.enforces} |`
      : `| ${at(document)} | ⚠ needs frontmatter | ⚠ needs frontmatter |`);
  }

  // 🔴 A document with no frontmatter at all is listed by what it says it is about: a real
  // set-up meets many of them, and a row reading *needs frontmatter* twice tells a reader
  // nothing. Its heading is the document's own words, never a guessed *applies when*. A block
  // missing a field keeps the warning above, since there the field is what is wrong. Only when there is
  // one, so an index whose documents are all described is unchanged.
  if (bare.length) {
    lines.push('', BARE_HEADING, '', BARE_HEAD);
    for (const document of bare) {
      lines.push(`| ${at(document)} | ${firstHeading(document.text)?.replace(/\|/g, '\\|') ?? '—'} |`);
    }
  }

  lines.push('', '## Skills', '', SKILL_HEAD);
  for (const document of input.skills) {
    const meta = parseFrontmatter(document.text, SKILL_FIELDS).meta;
    lines.push(`| ${at(document)} | ${meta?.description ? summarize(meta.description) : '⚠ needs frontmatter'} |`);
  }

  return `${lines.join('\n')}\n`;
}

/**
 * Every rule in full, each behind its own provenance line.
 *
 * No separator rule between them: a `---` line is indistinguishable from the frontmatter fence this
 * render exists to remove, and in markdown it can turn the line above it into a heading. The
 * provenance comment and a blank line separate them, and each rule opens with its own heading.
 */
export function renderRules(input: TierInput): string[] {
  const lines: string[] = [];
  for (const document of input.rules) {
    lines.push(
      '',
      makeHeader(document.file.pack, document.file.source, input.version),
      '',
      stripFrontmatter(document.text),
    );
  }

  return lines;
}

/** The first line of a rule's text the region reads as its own structure (a provenance line or a marker), or null. */
export function structureLine(text: string): string | null {
  return text.split('\n').map((line) => line.replace(/\r$/, '')).find((line) => /^<!-- \/?daoris:/.test(line)) ?? null;
}

/**
 * One rule's body, read back out of a rendered region.
 *
 * 🔴 This is what keeps drift and `upstream` **per rule** inside one span. Without it the region is a
 * single artefact: a person who improves one sentence has drifted all eight rules at once, and
 * `daoris upstream <file>` has nothing to extract. It finds the rule by its PROVENANCE line — the
 * same line `planSync` compares bodies against — and returns everything up to the next one.
 *
 * @param source `<pack>/<source>`, exactly as the provenance header spells it.
 * @returns the body, or null when this region does not carry that rule.
 */
export function tierRuleBody(region: string, source: string): string | null {
  const lines = region.split('\n');
  const marker = `<!-- daoris: ${source} @`;

  const start = lines.findIndex((line) => line.startsWith(marker));
  if (start === -1) return null;

  // Up to the next provenance line, or the end. A separator immediately before it belongs to the
  // NEXT rule's frame, not to this one's body.
  let end = lines.length;
  for (let at = start + 1; at < lines.length; at += 1) {
    if (lines[at]!.startsWith('<!-- daoris: ')) {
      end = at;
      break;
    }
  }

  return lines.slice(start + 1, end).join('\n').trim();
}

/**
 * A span's rule body as it stands on disk: the region its lock entry names, then the rule inside it.
 *
 * @param read The file's text, or null when there is none. A caller reading many spans passes a
 * cached reader so each file is read once.
 * @returns null when the entry is not a span, or its file, region or rule is not there.
 */
export function spanBody(
  root: string,
  harness: Harness,
  entry: LockEntry,
  read: (file: string) => string | null = (file) => {
    const abs = join(root, file);
    return existsSync(abs) ? readText(abs) : null;
  },
): string | null {
  const region = entry.in ? regionIn(harness, entry.in) : null;
  if (!region) return null;
  const text = read(entry.in!);
  if (text === null) return null;
  const held = findRegion(text, region.name);
  return held.kind === 'present' ? tierRuleBody(held.body, `${entry.pack}/${entry.source}`) : null;
}
