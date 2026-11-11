#!/usr/bin/env node
/**
 * The always-read prose, against its ceilings.
 *
 * ## Why
 *
 * The always-loaded **canon** has a byte budget, and the discipline is what makes it worth having: when it
 * goes red the answer is to split principle from detail, never to raise the limit.
 *
 * This repository's *own* standing orders need the same budget. `CLAUDE.md` is read at the start of every
 * session, the backlog's handover with it, and both grow by accretion with nothing to say so. The
 * discipline is the one in deepseek-harness's `verify-doc-budgets` (`docs/2026-09-21-dsh-evaluation.md` §3);
 * nothing is ported, so no notice is owed: the mechanism below is fifty lines of counting, and what converged
 * is the *rule*.
 *
 * ## What is budgeted, and what deliberately is not
 *
 * A ceiling belongs on a document that is **read whole**: the standing orders, the backlog, the
 * consuming story, the forward sequence, the contract.
 *
 * ## Where a ceiling is written: once
 *
 * A document `daoris.json` declares with a ceiling (`documents`) is measured at that number,
 * which `check` reads too, so it is written in one place. `tools/doc-budgets.json` keeps only the
 * documents no role names (the consuming story, the forward sequence, the contract) and the standing
 * orders, until they become the brief. A role bound to a ceiling alone, the brief or a room, and a folder's
 * ceiling, are `check`'s to measure, since only it knows where the region ends and which documents a folder holds. A document with a ceiling in both
 * lists has two numbers, and that is a fact, so it fails.
 *
 * It does **not** belong on an append-only record — the decision log, the task archive, the fix log,
 * the changelogs. Those are read by lookup, they grow by design, and a ceiling on one is a rule that
 * eventually says to delete history. Saying which is which is most of this tool's content; the counting
 * is the easy half.
 *
 * ## It reports; it does not fail
 *
 * Over a ceiling prints loudly and exits 0, as the canon's own budget does. A word count is a judgement, not a fact this tool established: 3,601 words
 * against a ceiling of 3,600 is not *wrong*, and a build that stops over a judgement gets its number
 * raised rather than read. A stale entry — a ceiling naming a document that moved — is the one thing
 * here that IS a fact, and it still fails, because a ceiling pointing at nothing has silently stopped
 * applying and that is a defect in the manifest rather than an opinion about length.
 *
 * ## The discipline, when it goes over
 *
 * 1. **Relocate** what belongs in another tier — the on-demand knowledge document, the decisions
 *    record, the archive — leaving a one-line link if the reader needs the pointer.
 * 2. **Condense** what belongs here but can be shorter.
 * 3. **Raise** the ceiling only when the words genuinely need the space, and justify the manifest's
 *    one-line diff in the commit. A too-low ceiling is a budget bug, not a reason to delete content.
 *
 * Ceilings are guardrails, not reduction targets: set one with headroom over what the document
 * measures today, and lower one only while the document still has room.
 *
 *   node tools/doc-budgets.mjs
 */
import { existsSync, readFileSync, statSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';

const MANIFEST = 'tools/doc-budgets.json';
const DECLARED = 'daoris.json';

/**
 * Whitespace-separated tokens. Stated rather than left to be inferred: a count nobody can reproduce is
 * a ceiling nobody can argue with. Tables, code and links all count, because a reader pays for them.
 */
export const words = (text) => text.split(/\s+/).filter(Boolean).length;

const readJson = (file) => (existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : {});

/**
 * Every ceiling under `root`: the declared documents' first, in the manifest's order, then this tool's
 * own list. `twice` names a document both give a number, which is measured at the manifest's.
 */
export function ceilings(root) {
  const budgets = [];
  for (const [role, entry] of Object.entries(readJson(join(root, DECLARED)).documents ?? {})) {
    if (typeof entry === 'object' && entry !== null && typeof entry.path === 'string' && typeof entry.words === 'number'
      && !(existsSync(join(root, entry.path)) && statSync(join(root, entry.path)).isDirectory())) {
      budgets.push({ document: entry.path, ceiling: entry.words, from: `${DECLARED} (${role})` });
    }
  }
  const declared = new Set(budgets.map((b) => b.document));
  const twice = [];
  for (const [document, ceiling] of Object.entries(readJson(join(root, MANIFEST)))) {
    if (document.startsWith('_')) continue; // the manifest's own prose
    if (declared.has(document)) twice.push(document);
    else budgets.push({ document, ceiling, from: MANIFEST });
  }
  return { budgets, twice };
}

if (isMain(import.meta.url)) {
  const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
  const { budgets, twice } = ceilings(repoRoot);
  const stale = twice.map((document) =>
    `${document}: given a ceiling in both ${DECLARED} and ${MANIFEST} — keep the manifest's, remove the other`);
  const over = [];
  const measured = [];

  for (const { document, ceiling, from } of budgets) {
    const file = join(repoRoot, document);
    if (!existsSync(file)) {
      // A ceiling naming a document that moved is a ceiling that silently stopped applying — which is
      // indistinguishable, from the outside, from a document comfortably under budget. A fact, so it fails.
      stale.push(`${document}: budgeted at ${ceiling} in ${from} but the file does not exist — the ceiling is stale`);
      continue;
    }

    const count = words(readFileSync(file, 'utf8'));
    measured.push({ document, count, ceiling, spare: ceiling - count });
    if (count > ceiling) over.push(`${document}: ${count} words of ${ceiling} — over by ${count - ceiling}`);
  }

  if (over.length > 0) {
    // Advisory: loud, quantified, and exits 0. An unquantified warning is one nobody acts on.
    console.warn(`doc-budgets: ${over.length} over budget (advisory)\n  ${over.join('\n  ')}`);
    console.warn(
      '\n  Relocate what belongs in another tier, then condense what belongs here. Raise a ceiling where it\n'
      + '  is written only when the words need the space — and say why in the commit.');
  }

  if (stale.length > 0) {
    console.error(`doc-budgets: ${stale.length} stale ceiling(s)\n  ${stale.join('\n  ')}`);
    process.exit(1);
  }

  // The tightest document is the useful number on a clean run — a total says nothing about which one is
  // closest to its ceiling, and that is the only question this report can answer usefully.
  if (measured.length === 0) {
    console.log('doc-budgets: nothing measured');
  } else {
    const tightest = measured.reduce((a, b) => (a.spare <= b.spare ? a : b));
    console.log(over.length === 0
      ? `doc-budgets: ${measured.length} documents within budget — tightest is ${tightest.document} at `
        + `${tightest.count}/${tightest.ceiling} (${tightest.spare} to spare)`
      : `doc-budgets: ${measured.length} documents measured, ${over.length} over — see above`);
  }
}
