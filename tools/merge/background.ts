/**
 * A background run (MERGENOKILL1, D346): the heavy tier after a landing (`tools/heavy-run.mjs`) and the CI beside the merges
 * (`startBesideRun`). Nobody waits on it, so it gives the machine to the runs someone does wait on: it runs below normal priority,
 * and its environment marks it (`BACKGROUND`), so each place it or what it starts asks of the machine's admission is a background
 * one, taken only while no blocking run waits (`tools/machine-admission.mjs`).
 */
import { constants, setPriority as osSetPriority } from 'node:os';

/** The variable, `1`, that marks a run and what it starts as background in the machine's admission. */
export const BACKGROUND = 'DAORIS_ADMISSION_BACKGROUND';

type SetPriority = (pid: number, priority: number) => void;

/**
 * The run lowers its own process to below normal: on Windows a process created by a below-normal one starts in that class, so
 * every gate's shell, npm, dotnet, test host and git runs below normal with no race, and the run ends with the process, so nothing
 * is restored. Below normal, not idle: idle starves behind a busy machine and a Process half's timing tests would flake. I/O
 * priority is left out (Node has no API for it, and whether children inherit background mode is unsettled).
 * Returns what went wrong, or null.
 */
export function lowerPriority(setPriority: SetPriority = osSetPriority): string | null {
  try {
    setPriority(0, constants.priority.PRIORITY_BELOW_NORMAL);
    return null;
  } catch (error) {
    return error instanceof Error ? error.message : String(error);
  }
}

/** The start of a background run: its environment marked, its priority lowered. Returns what went wrong lowering it, or null. */
export function startBackground({ setPriority = osSetPriority, env = process.env }: { setPriority?: SetPriority; env?: Record<string, string | undefined> } = {}): string | null {
  env[BACKGROUND] = '1';
  return lowerPriority(setPriority);
}
