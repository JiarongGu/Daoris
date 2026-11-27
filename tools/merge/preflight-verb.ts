/**
 * `--preflight [--base <ref>] [--bound <minutes>]`: the merge's fast plan for this worktree's branch, run here within its bound, the
 * censuses the declaration names first, its run kept as receipts and a summary record. What only the merge tool's script holds (its
 * gate runner, brief, admission and interrupt) is handed in.
 */
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fastPlan, scopedKind } from './fast-plan.ts';
import { count } from './format.ts';
import { git, MAIN } from './git.ts';
import { verdictsHome } from './home.ts';
import { SCRATCH } from './paths.ts';
import { readPlan, slug } from './plan.ts';
import { PREFLIGHT_BOUND_MINUTES, planLines, preflightFacts, preflightRun } from './preflight.ts';
import { firstGates, keepRun } from './preflight-record.ts';

/* eslint-disable @typescript-eslint/no-explicit-any -- the merge tool's script is untyped */
export type PreflightDeps = {
  scopedChoice: (home: string, tree: string, options: Record<string, unknown>) => any;
  scopedGates: (home: string, tree: string, plan: any[], choice: any, dir: string) => { gates: any[] };
  runRecorded: (root: string, gates: any[], dir: string, options: Record<string, unknown>) => Promise<any[]>;
  briefRed: (root: string, failed: any[], options: Record<string, unknown>) => Promise<unknown>;
  mainMarker: (root: string, dir: string) => unknown;
  machineAdmission: (env: NodeJS.ProcessEnv, options: Record<string, unknown>) => any;
  stopOnInterrupt: (trees: any) => () => void;
  clearLogs: (dir: string) => void;
  holdersSentence: (home: string | null) => string | null;
  contentTree: (root: string) => string;
  startHome: string | null;
};

export async function preflightVerb(root: string, options: { base?: string | null; bound?: number; oneAtATime?: boolean }, deps: PreflightDeps): Promise<number> {
  const started = Date.now();
  const facts = preflightFacts(root, options.base ?? MAIN, git);
  for (const line of facts.lines) console.log(line);
  const home = verdictsHome(root);
  const plan = readPlan(root);
  const choice = deps.scopedChoice(home, root, { plan, base: facts.from, tip: facts.tip });
  const set = fastPlan(choice.set, (name) => scopedKind(plan.find((gate: { name: string }) => gate.name === name)));
  const dir = join(root, SCRATCH, `merge-${slug(facts.branch)}`);
  deps.clearLogs(dir);
  const declaration = join(root, 'daoris.gates.json');
  const gates = firstGates(deps.scopedGates(home, root, plan, { ...choice, set }, dir).gates, existsSync(declaration) ? readFileSync(declaration, 'utf8') : null);
  const bound = options.bound ?? PREFLIGHT_BOUND_MINUTES;
  const run = preflightRun({ branch: facts.branch, gates: gates.map((gate) => gate.name), set, boundMinutes: bound, started, holders: () => deps.holdersSentence(deps.startHome) });
  for (const line of planLines(set, bound)) console.log(line);
  const unhook = deps.stopOnInterrupt(run.trees);
  run.start();
  let results: any[];
  try {
    const hooks = run.hooks(deps.machineAdmission(process.env, { home: deps.startHome, patience: `the preflight gives up at its bound of ${bound} min` }));
    results = await deps.runRecorded(root, gates, dir, { keepGoing: true, oneAtATime: options.oneAtATime, branch: facts.branch, kind: 'preflight', byMemory: true, base: facts.from, changed: choice.changed, ...hooks });
  } finally {
    run.stop();
    unhook();
  }
  const { lines, code, failed } = run.summary(results);
  const kept = keepRun({
    root, checkout: home, home: deps.startHome, scratch: dir, git, id: `${facts.tip.slice(0, 12)}-${started}`, trigger: process.env.DAORIS_PREFLIGHT_TRIGGER || 'agent',
    branch: facts.branch, base: facts.from, baseTip: facts.baseTip, tip: facts.tip, tree: deps.contentTree(root), startedAt: started, now: Date.now, results, waits: run.waits(), plan, set,
  });
  for (const line of kept.lines) console.log(line);
  if (failed.length) {
    await deps.briefRed(root, failed, {
      dirs: [dir], title: `Red brief: the preflight of ${facts.branch}`, mark: deps.mainMarker(root, dir),
      ran: `${count(failed.length, 'gate')} of ${results.length} failed on ${facts.branch} at ${facts.tip.slice(0, 7)}, judged against ${facts.base} at ${facts.baseTip.slice(0, 7)}.`,
    });
  }
  for (const line of lines) console.log(line);
  return code;
}
