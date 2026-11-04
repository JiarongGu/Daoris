#!/usr/bin/env node
/**
 * The orientation index: where things are in this repository, generated from the code and the decisions, so a
 * session starts from a lookup rather than a search, which costs it more before its first edit than the edit does.
 * Each entry names the file and line to open.
 *
 * ## What it writes
 *
 * `docs/index/`, every file generated and none edited by hand: `README.md` (the indexes kept by hand first, then
 * what each generated file answers, and every large file), `routes.md`, `verbs.md`, `catalogues.md`, `fixtures.md`,
 * `decisions.md`, `registered.md`, and `outlines/<path>.md` for each file over `LARGE`. The README's order is the canon's
 * `templates/index.md`: an index kept by hand is named, never restated.
 *
 * By kind, not by lane: a lookup starts from what is sought (a route, a verb, a key, a fixture), and the
 * route table joins two lanes, the page's call and the module's handler. An outline is a file of its own,
 * because one large file's outline can fill a page.
 *
 * ## Written in each checkout, never committed
 *
 * Line numbers move with nearly every change, so `docs/index/` is ignored and written where it is read:
 * `tools/index-fresh.mjs` says when. `--check` builds the index in memory and fails, naming each file, when
 * `docs/index/` is not what the tree says, and on each mark a decision's note carries that is refused.
 *
 * The files read are the ones `git add -A` would stage, so the index speaks for the tree as it would be
 * committed. Text is read with LF endings, sizes too, so a CRLF checkout writes the same index as the
 * release runner. Paths sort by code unit, never by locale.
 *
 *   node tools/orient-index.mjs            write docs/index/
 *   node tools/orient-index.mjs --check    exit 1 naming each stale file
 *   [--root <dir>]                         another work tree's top (the tests' fixtures)
 *
 * Exit codes: 0 written or fresh · 1 stale · 2 a tool error (usage, not a work tree's top).
 */
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';
import { applyIndex, planIndex, staleness } from './orient-index/plan.mjs';
import { INDEX } from './orient-index/common.mjs';
import { markFindings } from './orient-index/decisions.mjs';
export { repositoryFiles as listFiles } from './fsx.mjs';
export { INDEX, LARGE } from './orient-index/common.mjs';
export { continued } from './orient-index/inside.mjs';
export { outlineCSharp, outlineScript, outlineMarkdown } from './orient-index/outline.mjs';
export { firstArgument } from './orient-index/routes.mjs';
export { noteLabel, noteSpan, readDecision, noteMark, readMarks, markFindings } from './orient-index/decisions.mjs';
export { planIndex, staleness, applyIndex } from './orient-index/plan.mjs';

if (isMain(import.meta.url)) {
  const args = process.argv.slice(2);
  const rootAt = args.indexOf('--root');
  const unknown = args.filter((arg, i) => arg !== '--check' && arg !== '--root' && !(rootAt >= 0 && i === rootAt + 1));
  if (unknown.length || (rootAt >= 0 && !args[rootAt + 1])) {
    console.error('usage: node tools/orient-index.mjs [--check] [--root <dir>]');
    process.exit(2);
  }
  const root = rootAt >= 0 ? resolve(args[rootAt + 1]) : join(dirname(fileURLToPath(import.meta.url)), '..');
  let planned;
  try {
    planned = planIndex(root);
  } catch (error) {
    console.error(`orient-index: ${error.message}`);
    process.exit(2);
  }
  if (args.includes('--check')) {
    const stale = staleness(root, planned);
    if (stale.length) {
      console.error(`orient-index: ${INDEX}/ is stale in ${stale.length} file(s); node tools/orient-index.mjs writes it again:`);
      for (const { path, fact } of stale) console.error(`  ${path}: ${fact}`);
      process.exit(1);
    }
    const refused = markFindings(root);
    if (refused.length) {
      console.error(`orient-index: ${refused.length} mark(s) a decision's note carries are refused (docs/2026-10-30-knowledge-freshness-design.md §3.1):`);
      for (const { file, line, fact } of refused) console.error(`  ${file}:${line} ${fact}`);
      process.exit(1);
    }
    console.log(`orient-index: ${INDEX}/ is fresh, ${planned.size} files`);
  } else {
    const changed = applyIndex(root, planned);
    console.log(`orient-index: ${INDEX}/ written, ${changed.length} of ${planned.size} files changed`);
  }
}
