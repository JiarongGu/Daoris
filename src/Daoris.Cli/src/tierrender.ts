// What goes inside the region: the always-loaded tier, rendered once (CANON8b, D59).
//
// `.claude/rules/` was read by exactly one of three harnesses, so the tier moves into the file all
// three read. Eight files become one span, and three content decisions follow from that — each a
// consequence of concatenating rather than a preference:
//
//   1. FRONTMATTER IS STRIPPED. D14 already says frontmatter is only frontmatter at byte 0. Eight
//      rules concatenated would put seven `---` blocks mid-document, where a reader sees horizontal
//      rules and stray key-value prose. What it carried becomes the table.
//   2. PROVENANCE STAYS, per rule. It is what keeps drift and `upstream` per RULE inside one region,
//      which design §4 rests on — without it a person improving one sentence has drifted all eight.
//   3. THE TABLE KEEPS THE RULES, AND POINTS AT THE REST (WSSETUP14a, D128 §2). The rules rows are
//      cheap and carry the `applies_when`/`enforces` the stripped frontmatter held. Knowledge and
//      skills grow with the repository, so their tables live in `<target>/INDEX.md`, read on demand,
//      and the region says where: a row per document put the pilot's first rule past the byte at which
//      one agent stops reading.

import type { CanonFile, Harness, LockEntry } from './types.ts';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { firstHeading, frontmatterEnd, makeHeader, parseFrontmatter, SKILL_FIELDS, stripFrontmatter } from './document.ts';
import { readText } from './fsx.ts';
import { DEFAULT_HARNESS, HARNESSES, regionIn } from './harness.ts';
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
  /** The core rows this repository switched off, and the pack that offered each (D71). */
  off?: readonly { target: string; by: string }[];
  /** Where the skills live and the root that mirrors them, for the one sentence that says so (D117 §5.3). */
  mirror?: { source: string; root: string };
  /** Each declared room and its first heading (D117 §2.2). */
  rooms?: readonly { path: string; heading: string | null }[];
  /** Each declared document with a path, in the roles' order, and its role's job (D122 §2.7). */
  documents?: readonly { role: string; path: string; job: string }[];
}

/** What the index renders: the on-demand tiers as they are on disk (D128 §2.3). */
export interface IndexInput {
  knowledge: TierDocument[];
  skills: TierDocument[];
  /** Where the on-demand tiers live, so each row names its document from the repository's root. */
  target?: string;
}

const RULE_HEAD = '| Rule | Applies when | Enforces |\n|---|---|---|';
const KNOWLEDGE_HEAD = '| Document | Applies when | Enforces |\n|---|---|---|';
const BARE_HEADING = '## Knowledge without frontmatter';
const BARE_HEAD = '| Document | Its first heading |\n|---|---|';
const SKILL_HEAD = '| Skill | Use when |\n|---|---|';
const ROOM_HEAD = '| Room | About |\n|---|---|';
const WHERE_HEAD = '| Role | Where | Its job |\n|---|---|---|';

/** The heading of the roster's last table, which `check` compares on its own (D122 §2.8). */
export const WHERE_HEADING = '## Where things are';

/** The index's file name, at the target's root beside the tiers rather than inside one (D128 §2.3). */
export const INDEX_FILE = 'INDEX.md';

/**
 * Where the index of the on-demand tiers is, repository-relative: at the target's root, where no tier
 * lists it as a document, the service does not read it, and a repository's own `knowledge/INDEX.md`
 * cannot collide with it.
 */
export const indexPath = (target: string): string => (target === '' ? INDEX_FILE : `${target}/${INDEX_FILE}`);

/** A document's name — the filename, which is what every rule's frontmatter `name` must match. */
function nameOf(file: CanonFile): string {
  return file.target.replace(/^.*\//, '').replace(/\.md$/, '');
}

function metaOf(document: TierDocument, required: readonly string[] = ['applies_when', 'enforces']) {
  if (document.meta) return required.every((field) => document.meta![field]) ? document.meta : null;
  return parseFrontmatter(document.text, required).meta;
}

/**
 * A document with no frontmatter block at all, which the index lists by its first heading and `check`
 * counts (WSSETUP14c, D128 §3.1–§3.2). One answer for both, so the count is the table's rows.
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
 * The roster half: the rules table, the pointer to the index, the rooms and where the records are.
 *
 * @remarks
 * Split out because `check` rebuilds everything below the rules table from the disk and the manifest to
 * tell whether the region has gone stale, and it does that offline, with no canon. The rules rows are
 * not rebuildable offline, because the frontmatter they come from is stripped on the way in; a canon
 * change is `status`'s report and `sync`'s job, which is where a canon change belongs.
 *
 * 🔴 Nothing here grows with the repository's documents (WSSETUP14a, D128 §2.1). The knowledge and
 * skill tables did, and on the first real set-up they put the first rule past byte 32,768 of the file,
 * where one agent stops reading. They are `renderIndex`'s now, and the region carries one sentence
 * naming the file, with no count in it: a count would change the region with every document.
 */
export function renderRoster(input: TierInput): string {
  const target = input.target ?? HARNESSES[DEFAULT_HARNESS]!.defaultTarget;
  // 🔴 The region's own headings sit at the SAME level the rule bodies use, and the bodies are not
  // touched. Nesting them under a `## Doctrine` wrapper reads better in a table of contents and
  // requires demoting every `#` inside eight rules — a transformation that has to be exactly
  // reversible over arbitrary markdown, including inside fenced blocks, or a rule is silently
  // corrupted on its way back out through `upstream`. A body rewritten on the way in cannot be
  // compared on the way out, so the document is flat: a concatenation of documents, which is what it
  // is. Seen by rendering the real canon and reading it.
  const lines: string[] = [
    '# Doctrine',
    '',
    'Generated by daoris from the shared canon. Edit the canon, not this — a local edit here is'
    + ' reported as drift, and `daoris upstream <file>` promotes one.',
    '',
    '## Always loaded — every rule below, in full',
    '',
    RULE_HEAD,
  ];

  for (const document of input.rules) {
    const meta = metaOf(document);
    // Named, not linked. The rule is a few lines down in the same document, and a link would have to
    // guess the anchor its heading produces — which is the canon's sentence, not its filename.
    lines.push(meta
      ? `| \`${nameOf(document.file)}\` | ${meta.applies_when} | ${meta.enforces} |`
      : `| \`${nameOf(document.file)}\` | ⚠ needs frontmatter | ⚠ needs frontmatter |`);
  }

  // 🔴 What is NOT here, said here (D71). A core row a pack switched off is absent from every table
  // below, and a session that meets a doctrine with a hole in it cannot tell the hole from a canon
  // that never had the rule. Under the rules table, because that half is `sync`'s alone: `check`
  // rebuilds only the on-demand half offline, and this line needs the lock's knowledge of who did it.
  if (input.off?.length) {
    const rows = input.off.map((row) =>
      `\`${row.target.replace(/^.*\//, '').replace(/\.md$/, '')}\` (by \`${row.by}\`)`);
    lines.push('', `Switched off here, by a pack and confirmed in \`daoris.json\`: ${rows.join(', ')}.`);
  }

  // The pointer (D128 §2.2), linked because it names a real file the reader has to go and open.
  const index = indexPath(target);
  lines.push('', '## Read on demand', '',
    `The knowledge and the skills, each with when it applies, are listed in [${index}](${index}), generated `
    + 'from the files: read it before a non-trivial task, and search it when it is long.');
  // Said once, beside the pointer: the copy exists for the agent that reads only there, and an edit
  // made to it is refused (D117 §3.3), so the reader learns which file to open before editing.
  if (input.mirror) {
    lines.push('', `Skills live in \`${input.mirror.source}/\`; \`${input.mirror.root}/\` mirrors them for the agent `
      + 'that reads only there — edit the source.');
  }

  // 🔴 Telling, not loading (D117 §2.2). D59 rejected telling for the RULES, which every task needs; a
  // room is on-demand material like knowledge, and no harness is measured loading a nested file for a
  // session started at the root (LAYOUT2), so the roster is what makes each one reachable. Only when a
  // room is declared, so a region without rooms is byte for byte what it was.
  if (input.rooms?.length) {
    lines.push('', '## Rooms', '', "Read a folder's room before changing anything in it.", '', ROOM_HEAD);
    for (const room of input.rooms) {
      lines.push(`| [${room.path}](${room.path}/AGENTS.md) | ${room.heading ?? '⚠ no heading'} |`);
    }
  }

  // 🔴 Where the records are (D122 §2.7), as data rather than prose: the one part of the standard every
  // task needs, and the search each session would otherwise make. From the manifest alone, so `check`
  // rebuilds it offline and fails when it differs. Only when a path is declared, so a region whose
  // repository declares nothing is byte for byte what it was. A path is a code span with its pipes
  // escaped, which is what keeps a table a table.
  if (input.documents?.length) {
    lines.push('', WHERE_HEADING, '', 'Declared in `daoris.json`; look here before searching.', '', WHERE_HEAD);
    for (const document of input.documents) {
      lines.push(`| ${document.role} | \`${document.path.replace(/\|/g, '\\|')}\` | ${document.job} |`);
    }
  }

  return lines.join('\n');
}

/**
 * `<target>/INDEX.md`: the knowledge and skills, each with when it applies (WSSETUP14a, D128 §2.3).
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

  // 🔴 A document with no frontmatter at all is listed by what it says it is about (WSSETUP14c, D128
  // §3.1): the first set-up met 166 of them, and a row reading *needs frontmatter* twice told a reader
  // nothing. Its heading is the document's own words, never a guessed *applies when* (§3.4). A block
  // missing a field keeps the warning above, since there the field is what is wrong. Only when there is
  // one, so an index whose documents are all described is what it was.
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
