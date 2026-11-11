import { spawn, type ChildProcess } from 'node:child_process';
import { PATIENCE } from '../../test/_fixture.ts';

/** What one bounded invocation answered: its exit status, null where it did not start or its tree outlived the end, and all it wrote with the bound's words. */
export interface Bounded { status: number | null; out: string }

export interface BoundedOptions {
  cwd: string;
  env: NodeJS.ProcessEnv;
  /** The bound: past it the invocation is ended with its tree. */
  ms: number;
  /** Ends a tree by its root's pid, answering whether the root is gone (`endTree`). */
  end: (pid: number) => boolean;
  /** How long after the end the invocation's output may stay held before it is answered without it. */
  grace?: number;
  /** The invocations a case has running: this one is in it until it closes, and stays in it where its tree outlived the end. */
  started: Set<ChildProcess>;
}

/**
 * A node process of a case, bounded by `ms`: one that gives no verdict by then is ended with its tree. Its answer waits for `close`,
 * which comes only once every process holding its output is gone; an end that leaves one (refused, or a grandchild no longer under
 * the child) is answered `grace` after it, naming the leak and leaving the child in `started`, so a case never waits on it unbounded.
 */
export function boundedNode(args: string[], { cwd, env, ms, end, grace = PATIENCE, started }: BoundedOptions): Promise<Bounded> {
  const child = spawn(process.execPath, args, { cwd, env, windowsHide: true });
  started.add(child);
  let out = '';
  child.stdout.on('data', (chunk) => { out += String(chunk); });
  child.stderr.on('data', (chunk) => { out += String(chunk); });
  return new Promise((done) => {
    let held: NodeJS.Timeout | undefined;
    const bound = setTimeout(() => {
      out += `\n[the test ended this invocation: no verdict within ${ms} ms (TOOLTESTHANG1)]\n`;
      const gone = end(child.pid!);
      held = setTimeout(() => {
        out += `\n[its tree outlived the end${gone ? '' : ', which taskkill refused'}: pid ${child.pid} or what it started still holds its output after ${grace} ms, left running (TOOLTESTHANG1)]\n`;
        done({ status: null, out });
      }, grace);
    }, ms);
    const ended = (status: number | null) => {
      clearTimeout(bound);
      clearTimeout(held);
      started.delete(child);
      done({ status, out });
    };
    child.on('close', ended);
    child.on('error', (error) => { out += `${error.message}\n`; ended(null); });
  });
}
