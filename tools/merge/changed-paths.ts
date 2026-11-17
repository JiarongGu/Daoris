/**
 * The changed paths a gate's steps are handed: `MERGE_BRANCH_CHANGED`, the paths as JSON, set on every step of a run the merge scopes by
 * reach (a merge in place but `--full`, a preflight, the queue's scoped run) and on none of a whole run, so a gate that reads it
 * (`npm run verify:checks`) runs what the paths reach, and runs whole where it is unset. A list too long for one environment variable
 * is not handed: the gate runs whole. The start environment's own is never passed on (`GATE_REMOVED` takes every `MERGE_BRANCH_` name).
 */
import { mergeChanges } from './changes.ts';

export const CHANGED_VARIABLE = 'MERGE_BRANCH_CHANGED';
/** Under Windows' bound of 32,767 characters a variable, with room for the rest of the environment's names. */
const MOST_CHARACTERS = 30_000;

/** The variable that hands `changed` to a step, or none where there is no list or it is too long to hand. */
export function changedEnvironment(changed: readonly string[] | null | undefined): Record<string, string> {
  if (!changed) return {};
  const text = JSON.stringify(changed);
  return text.length > MOST_CHARACTERS ? {} : { [CHANGED_VARIABLE]: text };
}

/** The changed paths an environment hands, or null where it hands none or what it hands is not a list of paths. */
export function changedPaths(env: Record<string, string | undefined>): string[] | null {
  const text = env[CHANGED_VARIABLE];
  if (!text) return null;
  try {
    const parsed: unknown = JSON.parse(text);
    return Array.isArray(parsed) && parsed.every((each) => typeof each === 'string') ? parsed : null;
  } catch {
    return null;
  }
}

type StepOptions = { env?: Record<string, string> | null } & Record<string, unknown>;
type Step = (command: string, cwd: string, fd: number, options?: StepOptions) => Promise<unknown>;

/** `step`, each command of it handed `changed`; `step` itself where there is no list. */
export function withChanged<S extends Step>(step: S, changed: readonly string[] | null | undefined): S {
  const handed = changedEnvironment(changed);
  if (!Object.keys(handed).length) return step;
  return ((command, cwd, fd, options = {}) => step(command, cwd, fd, { ...options, env: { ...options.env, ...handed } })) as S;
}

/** What the merge in place hands its gates: its changed paths, or none on a `--full` merge, which runs every gate whole. */
export const mergeChanged = (root: string, full: boolean | undefined): string[] | null => (full ? null : mergeChanges(root));
