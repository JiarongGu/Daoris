#!/usr/bin/env node
/**
 * The always-read prose, against its ceilings.
 *
 * ## Why
 *
 * D28 put a byte budget on the always-loaded **canon**, and the discipline it created is the reason
 * that budget is worth having: when it goes red the answer is to split principle from detail, never to
 * raise the limit — and CANON6 found 126 bytes of genuine duplication rather than spending any.
 *
 * This repository's *own* standing orders had no such budget. `CLAUDE.md` is read at the start of every
 * session, the backlog's handover with it, and both had grown by accretion with nothing to say so. That
 * gap is what DOCS2 closes, adopting the discipline from deepseek-harness's `verify-doc-budgets`
 * (`docs/2026-09-21-dsh-evaluation.md` §3) — two ecosystems that arrived at the same rule from opposite
 * ends, which is the D17 bar for believing it. **Nothing was ported**, so no notice is owed: the
 * mechanism below is fifty lines of counting, and what converged is the *rule*.
 *
 * ## What is budgeted, and what deliberately is not
 *
 * A ceiling belongs on a document that is **read whole**. Those are the five in the manifest: the
 * standing orders, the backlog, the consuming story, the forward sequence, the contract.
 *
 * It does **not** belong on an append-only record — the decision log, the task archive, the fix log,
 * the changelogs. Those are read by lookup, they grow by design, and a ceiling on one is a rule that
 * eventually says to delete history. Saying which is which is most of this tool's content; the counting
 * is the easy half.
 *
 * ## It reports; it does not fail
 *
 * Over a ceiling prints loudly and exits 0 (D54, the owner's call, applied here for consistency with
 * the canon's own budget). A word count is a judgement, not a fact this tool established: 3,601 words
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
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const MANIFEST = 'tools/doc-budgets.json';

/**
 * Whitespace-separated tokens. Stated rather than left to be inferred: a count nobody can reproduce is
 * a ceiling nobody can argue with. Tables, code and links all count, because a reader pays for them.
 */
const words = (text) => text.split(/\s+/).filter(Boolean).length;

const budgets = JSON.parse(readFileSync(join(repoRoot, MANIFEST), 'utf8'));
const stale = [];
const over = [];
const measured = [];

for (const [document, ceiling] of Object.entries(budgets)) {
  if (document.startsWith('_')) continue; // the manifest's own prose

  const file = join(repoRoot, document);
  if (!existsSync(file)) {
    // A ceiling naming a document that moved is a ceiling that silently stopped applying — which is
    // indistinguishable, from the outside, from a document comfortably under budget. A fact, so it fails.
    stale.push(`${document}: budgeted at ${ceiling} but the file does not exist — the ceiling is stale`);
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
    '\n  Relocate what belongs in another tier, then condense what belongs here. Raise a ceiling in\n'
    + `  ${MANIFEST} only when the words need the space — and say why in the commit.`);
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
