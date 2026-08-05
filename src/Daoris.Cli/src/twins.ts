import type { CommandArgs, Lock, Manifest, Twin } from './types.ts';
import type { ExitCode } from './errors.ts';
import { join } from 'node:path';
import { listMarkdown, readText } from './fsx.ts';
import { parseFrontmatter, stripHeader } from './document.ts';
import { lockIndex, readLock, readManifest } from './config.ts';

const INDEX_FILE = 'RULES_INDEX.md';
const TIERS = ['rules', 'knowledge', 'skills'];

/** Words this common carry no signal about what a document is about. */
const STOPWORDS = new Set([
  'that', 'this', 'with', 'from', 'they', 'them', 'then', 'than', 'have', 'has', 'been', 'were',
  'when', 'what', 'which', 'while', 'where', 'because', 'into', 'onto', 'over', 'under', 'about',
  'there', 'their', 'would', 'could', 'should', 'must', 'never', 'always', 'every', 'each', 'only',
  'also', 'more', 'most', 'some', 'such', 'very', 'just', 'even', 'does', 'done', 'will', 'like',
  'apply', 'applies', 'rule', 'rules', 'name', 'enforces', 'when',
]);

/** Deduped significant words — the crude signal that two documents cover the same ground. */
export function significantTokens(text: string): Set<string> {
  const body = stripHeader(text);
  const { body: withoutFrontmatter } = parseFrontmatter(body);
  const words = (withoutFrontmatter || body).toLowerCase().match(/[a-z][a-z-]{3,}/g) ?? [];
  return new Set(words.filter((word) => !STOPWORDS.has(word)));
}

/**
 * Containment rather than Jaccard: a short local rule that restates a long
 * canonical one is still a twin, and Jaccard would score it low purely for
 * being shorter.
 */
export function containment(a: Set<string>, b: Set<string>): number {
  if (!a.size || !b.size) return 0;
  let shared = 0;
  for (const token of a) if (b.has(token)) shared += 1;
  return shared / Math.min(a.size, b.size);
}

/**
 * Set from measurement against real sibling documents, not taste. Across eleven
 * known pairs: near-verbatim copies score 72-74%, twins that were REWRITTEN
 * rather than copied land at 34-43%, and unrelated documents sit at 7-16%. The
 * original 0.5 sat above the middle band, so it caught only the easy copies and
 * missed every twin whose wording had drifted — which is precisely the set worth
 * finding, since nobody recognises those by eye either.
 *
 * 0.3 catches the rewritten band with no false positive in that sample. It is
 * chosen asymmetrically on purpose: `doctor` is advisory and always exits 0, so
 * a false positive costs one dismissed line while a miss costs duplication that
 * lasts indefinitely.
 */
const THRESHOLD = 0.3;

/**
 * Report local documents that appear to restate a canonical one under a
 * different name.
 *
 * This is the one failure the rest of the tool structurally cannot see: the
 * twin is local, and local is invisible by design (D5). It surfaced on the very
 * first real adoption and was only caught by reading the generated index.
 *
 * Deliberately advisory. Word overlap is a crude signal, and a false positive
 * that failed a build would be worse than the duplication it warns about.
 */
export function findTwins(
  { root, manifest, lock }: { root: string; manifest: Manifest; lock: Lock | null },
): Twin[] {
  const locked = lockIndex(lock);

  interface Candidate { tier: string; target: string; tokens: Set<string> }
  const canonical: Candidate[] = [];
  const local: Candidate[] = [];
  for (const tier of TIERS) {
    for (const file of listMarkdown(join(root, manifest.target, tier))) {
      if (file === INDEX_FILE) continue;
      const target = `${tier}/${file}`;
      const tokens = significantTokens(readText(join(root, manifest.target, tier, file)));
      (locked.has(target) ? canonical : local).push({ tier, target, tokens });
    }
  }

  // Compared WITHIN a tier. A knowledge document and a skill are different kinds of thing, so one
  // restating the other is not duplication worth reporting — and a generic skill ("find the exemplar
  // to mirror") names module, service, handler and test, which is the vocabulary of every
  // architecture document. On the second real adoption one such skill matched three unrelated
  // knowledge documents and buried the genuine twin sitting beside them.
  const twins: Twin[] = [];
  for (const candidate of local) {
    let best: Twin | null = null;
    for (const known of canonical) {
      if (known.tier !== candidate.tier) continue;
      const score = containment(candidate.tokens, known.tokens);
      if (score >= THRESHOLD && (!best || score > best.score)) {
        best = { local: candidate.target, canonical: known.target, score };
      }
    }
    if (best) twins.push(best);
  }
  return twins.sort((a, b) => b.score - a.score);
}

export function commandDoctor({ root, write }: Pick<CommandArgs, 'root' | 'write'>): ExitCode {
  const manifest = readManifest(root);
  const twins = findTwins({ root, manifest, lock: readLock(root) });

  if (!twins.length) {
    write('daoris: no suspected duplicates between this repo\'s own documents and the canon');
    return 0;
  }

  write('daoris: suspected duplicates — advisory only, nothing is changed');
  write('');
  for (const twin of twins) {
    write(`  ${twin.local}`);
    write(`    looks like ${twin.canonical} (${Math.round(twin.score * 100)}% shared vocabulary)`);
  }
  write('');
  write('  If they are the same rule under two names, delete the local one and check whether');
  write("  this repo's entry document referenced it by name. If they genuinely differ, ignore this.");
  write('');
  write('  This finds restatement, not convergence: a document reaching the same principle in a');
  write('  different vocabulary scores like an unrelated one. Still worth a read-through by hand.');
  return 0;
}
