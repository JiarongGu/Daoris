/**
 * Build once per tree. A gate that only runs tests of a project an earlier gate of the run passed on the same tree runs with
 * `--no-build`; the build is keyed by the project and every option but the ones that choose or log tests, which build nothing.
 */

const RUN_ONLY = new Set(['--filter', '--settings', '--logger', '--results-directory', '--blame', '--no-build', '--no-restore']);

/** The build key of each `dotnet test` segment of a command, in order; none for a command that runs no dotnet test. */
export function buildKeys(run: string): string[] {
  return run.split(' && ').flatMap((segment) => {
    const words = segment.trim().match(/"[^"]*"|'[^']*'|\S+/g) ?? [];
    if (words[0] !== 'dotnet' || words[1] !== 'test' || !words[2] || words[2].startsWith('-')) return [];
    const project = words[2].replace(/^["']|["']$/g, '').split(/[\\/]+/).filter((part) => part && part !== '.').join('/');
    const options: string[] = [];
    for (let i = 3; i < words.length; i += 1) {
      const word = words[i] ?? '';
      const [name] = word.split('=');
      if (!RUN_ONLY.has(name ?? '')) options.push(word);
      else if (!word.includes('=') && name !== '--no-build' && name !== '--no-restore') i += 1;
    }
    return [['dotnet test', project, ...options].join(' ')];
  });
}

/** The command told not to build, unless it already is. */
export const withoutBuild = (segment: string): string => (/\s--no-build\b/.test(segment) ? segment : `${segment.trim()} --no-build`);

export class BuildLedger {
  readonly #built = new Map<string, string>();

  /** A gate of this run passed on `tree`: each project its command built stands built there. */
  passed(gate: string, tree: string, run: string): void {
    for (const key of buildKeys(run)) if (!this.#built.has(`${tree}\0${key}`)) this.#built.set(`${tree}\0${key}`, gate);
  }

  /** The command with `--no-build` on each segment whose build a passed gate made on this tree, and the gates that made them. */
  reuse(tree: string, run: string): { run: string; by: string[] } {
    const by: string[] = [];
    const segments = run.split(' && ').map((segment) => {
      const [key] = buildKeys(segment);
      const gate = key === undefined ? undefined : this.#built.get(`${tree}\0${key}`);
      if (gate === undefined) return segment;
      if (!by.includes(gate)) by.push(gate);
      return withoutBuild(segment);
    });
    return { run: segments.join(' && '), by };
  }
}
