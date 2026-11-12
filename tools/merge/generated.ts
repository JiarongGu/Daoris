/**
 * Generated files a gate checks are written by the merge first, not failed for. A gate declares the command that makes its
 * generated input current (`regenerate` in `daoris.gates.json`); the merge runs each such command once before its gates, stages
 * what it wrote where git tracks it, and a gate then finds the file current.
 */

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

/** Each command run through `run`, then `stage` once for what changed; the first that fails ends it, saying why. */
export function regenerate(
  commands: Regeneration[],
  { run, stage }: { run: (command: string) => { status: number | null; output: string }; stage: () => void },
): Regenerated {
  const lines: string[] = [];
  for (const { command, gates } of commands) {
    const ran = run(command);
    const said = ran.output.trim().split('\n').at(-1) ?? '';
    if (ran.status !== 0) return { ok: false, lines: [...lines, `could not regenerate for ${gates.join(', ')}: ${command} (${said})`] };
    lines.push(`regenerated before ${gates.join(', ')}: ${command} (${said})`);
  }
  if (commands.length) stage();
  return { ok: true, lines };
}
