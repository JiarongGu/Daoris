/**
 * The real-case catalogue run on an installed Daoris: files one case's real work through the installed command against a
 * scratch repository the installed `repository create` makes, follows it by the command's reads, and writes a receipt.
 *   node tools/real-case.mjs --list
 *   node tools/real-case.mjs <case> --home <data> --service <url> --workspace-dir <folder> [--workspace <name>] [--daoris <command>]
 *        [--workflow <id>] [--through "<words>"] [--with-word] [--note-intervention "<what you did>"]… [--timeout-min <n>]
 *        [--poll-sec <n>] [--out <folder>]
 *
 * It presses nothing a person decides unless `--with-word` says the person's word is given, and never pushes, publishes or touches
 * a remote. The receipt (JSON and text) goes under `local/real-case/` unless `--out` names a folder. Exit codes: 0 the case passed with
 * no intervention, 1 it failed, needed an intervention or waits on the person's word, 2 a tool error.
 */
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { isMain, writeAtomic } from './fsx.mjs';
import { cases, caseById } from './real-case/catalogue.mjs';
import { installedCommand } from './real-case/installed.mjs';
import { follow } from './real-case/follow.mjs';
import { receiptOf, receiptText } from './real-case/receipt.mjs';

const VALUES = new Set(['home', 'service', 'workspace-dir', 'workspace', 'daoris', 'workflow', 'through', 'note-intervention', 'timeout-min', 'poll-sec', 'out', 'case']);

export function parse(argv) {
  const options = { notes: [] };
  for (let i = 0; i < argv.length; i++) {
    const flag = argv[i];
    if (flag === '--list' || flag === '--with-word') { options[flag === '--list' ? 'list' : 'withWord'] = true; continue; }
    if (!flag.startsWith('--')) { options.case ??= flag; continue; }
    const name = flag.slice(2);
    if (!VALUES.has(name)) throw new Error(`unknown argument ${flag}`);
    const value = argv[++i];
    if (!value || value.startsWith('--')) throw new Error(`${flag} needs a value`);
    if (name === 'note-intervention') options.notes.push(value);
    else options[name.replace(/-(\w)/g, (_, c) => c.toUpperCase())] = value;
  }
  return options;
}

export function catalogueText() {
  return cases.map(c => c.built
    ? `${String(c.id).padStart(2)}  built    ${c.title}`
    : `${String(c.id).padStart(2)}  needs    ${c.title}\n                 - ${c.needs.join('\n                 - ')}`).join('\n') + '\n';
}

/** Run one case: `installed` is the installed command's stand-in or the real one; `sleep` and `clock` are injectable. */
export async function runCase(options, { installed, sleep = (ms) => new Promise(r => setTimeout(r, ms)), clock = Date.now } = {}) {
  const caseDef = caseById(options.case);
  if (!caseDef.built) throw new Error(`case ${caseDef.id} is listed, not built; it needs:\n  - ${caseDef.needs.join('\n  - ')}`);
  const stamp = new Date(clock()).toISOString().replace(/[-:T]/g, '').slice(0, 14);
  const repo = { name: `rcase-${caseDef.id}-${stamp}` };
  const log = await follow({
    caseDef, repository: repo.name, workspaceDir: options.workspaceDir, workspace: options.workspace, workflow: options.workflow, through: options.through,
    installed, withWord: Boolean(options.withWord), sleep, clock,
    pollMs: Number(options.pollSec ?? 5) * 1000, timeoutMs: Number(options.timeoutMin ?? 20) * 60_000,
  });
  for (const note of options.notes) log.interventions.push({ at: new Date(clock()).toISOString(), act: note, why: 'noted by the operator' });
  const receipt = receiptOf({ caseDef, repository: repo.name, workspace: options.workspace, service: options.service, log, withWord: Boolean(options.withWord) });
  return { receipt, text: receiptText(receipt), stamp };
}

export function writeReceipt({ receipt, text, stamp }, out) {
  mkdirSync(out, { recursive: true });
  const base = join(out, `${stamp}-case${receipt.case}`);
  writeAtomic(`${base}.json`, JSON.stringify(receipt, null, 2) + '\n');
  writeAtomic(`${base}.txt`, text);
  return base;
}

async function main(argv) {
  const options = parse(argv);
  if (options.list) { process.stdout.write(catalogueText()); return 0; }
  if (!options.case) throw new Error('name a case (see --list)');
  if (!options.home) throw new Error('--home names the installed Daoris home (its data folder)');
  const installed = installedCommand({ command: options.daoris, home: options.home, service: options.service });
  const done = await runCase(options, { installed });
  const base = writeReceipt(done, options.out ?? join('local', 'real-case'));
  process.stdout.write(done.text + `receipt: ${base}.json\n`);
  return done.receipt.result === 'passed' ? 0 : 1;
}

if (isMain(import.meta.url)) {
  main(process.argv.slice(2)).then(code => { process.exitCode = code; }, error => { process.stderr.write(`real-case: ${error.message}\n`); process.exitCode = 2; });
}
