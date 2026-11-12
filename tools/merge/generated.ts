/**
 * Generated inputs a gate checks are written by the merge first, not failed for. A gate declares the command that makes its
 * generated input current (`regenerate` in `daoris.gates.json`); the merge runs each such command once before its gates, so a
 * gate finds the file current. A generated folder git tracks is `generated`'s: written and staged by the merge tool.
 */

import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

export type Folder = { folder: string; run: string };

/** The folders `daoris.gates.json`'s `generated` declares, each with the command that writes it; `refuse` makes the error. */
export function readFolders(root: string, refuse: (message: string, code?: number) => Error): Folder[] {
  const file = join(root, 'daoris.gates.json');
  if (!existsSync(file)) return [];
  let parsed: { generated?: unknown };
  try {
    parsed = JSON.parse(readFileSync(file, 'utf8')) as { generated?: unknown };
  } catch (error) {
    throw refuse(`daoris.gates.json is not valid JSON: ${error instanceof Error ? error.message : String(error)}`);
  }
  const entries = Array.isArray(parsed?.generated) ? (parsed.generated as Array<{ paths?: unknown; run?: unknown }>) : [];
  return entries.map((entry) => {
    if (typeof entry?.paths !== 'string' || !entry.paths.trim() || typeof entry?.run !== 'string' || !entry.run.trim()) {
      throw refuse("daoris.gates.json: every generated entry needs both 'paths' and 'run'");
    }
    return { folder: entry.paths.trim().replace(/\/+$/, ''), run: entry.run };
  });
}

/** Whether a path is one a merge writes rather than merges, by the folders `generated` names. */
export const inFolders = (path: string, folders: Folder[]): boolean => folders.some(({ folder }) => path.startsWith(`${folder}/`));

/** Each folder written from the tree as it stands and staged; one line each. A command that fails refuses the merge's gates. */
export function writeFolders(root: string, { env, stage, refuse, say }: {
  env: NodeJS.ProcessEnv; stage: (folder: string) => void; refuse: (message: string, code?: number) => Error; say: (line: string) => void;
}): void {
  for (const { folder, run } of readFolders(root, refuse)) {
    // A repository's own script gets a gate's environment, not the invoker's shell.
    const ran = spawnSync(run, { cwd: root, encoding: 'utf8', shell: true, windowsHide: true, env });
    if (ran.status !== 0) throw refuse(`${run} could not write ${folder}/ into the merge:\n${(ran.stderr || ran.stdout || '').trim()}`, 1);
    stage(folder);
    say(`${folder}/: written from the merged tree (${ran.stdout.trim().replace(/^[\w-]+: /, '')})`);
  }
}

export type Regeneration = { command: string; gates: string[] };
export type Regenerated = { ok: boolean; lines: string[] };

/** The distinct regeneration commands of the gates chosen, each with the gates that declare it, in the plan's order. */
export function regenerations(gates: Array<{ name: string; regenerate?: string }>): Regeneration[] {
  const found = new Map<string, string[]>();
  for (const gate of gates) {
    if (typeof gate.regenerate !== 'string' || !gate.regenerate.trim()) continue;
    found.set(gate.regenerate, [...(found.get(gate.regenerate) ?? []), gate.name]);
  }
  return [...found].map(([command, names]) => ({ command, gates: names }));
}

/** Each command run through `run`; the first that fails ends it, saying why. */
export function regenerate(
  commands: Regeneration[],
  { run }: { run: (command: string) => { status: number | null; output: string } },
): Regenerated {
  const lines: string[] = [];
  for (const { command, gates } of commands) {
    const ran = run(command);
    const said = ran.output.trim().split('\n').at(-1) ?? '';
    if (ran.status !== 0) return { ok: false, lines: [...lines, `could not regenerate for ${gates.join(', ')}: ${command} (${said})`] };
    lines.push(`regenerated before ${gates.join(', ')}: ${command} (${said})`);
  }
  return { ok: true, lines };
}
