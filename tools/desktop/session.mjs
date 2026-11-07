import { existsSync, readdirSync, readFileSync, statSync, unlinkSync } from 'node:fs';
import { join } from 'node:path';
import { repoRoot, scratchRoot } from './paths.mjs';
import { fail } from './run.mjs';
import { assemblyExe, prune } from './executables.mjs';

export const DESKTOP_PROJECT = join(repoRoot, 'src', 'Daoris.Desktop', 'Daoris.Desktop.App');
export const HTTP_PROJECT = join(repoRoot, 'src', 'Daoris.Service', 'Daoris.Service.Http');
export const MCP_PROJECT = join(repoRoot, 'src', 'Daoris.Service', 'Daoris.Service.Mcp');
export const DRIVER_HOST_PROJECT = join(repoRoot, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Host');
export const WEB = join(repoRoot, 'src', 'Daoris.Web');
export const RUN_FILE = join(scratchRoot, 'run.json');
export const SHOTS = join(scratchRoot, 'screenshots');

export const readRun = () => (existsSync(RUN_FILE) ? JSON.parse(readFileSync(RUN_FILE, 'utf8')) : null);

/**
 * Which shell the instruments address — the one the last `run` started.
 *
 * 🔴 Every one of them matches by executable PATH, which is what stops this loop photographing or
 * killing an install the owner actually uses. That rule does not change when the target IS an
 * install; what changes is which path, and the run file already records it. Falling back to the
 * checkout keeps every existing invocation behaving exactly as it did.
 */
export const targetExe = () => readRun()?.exe ?? assemblyExe(DESKTOP_PROJECT);

/**
 * The running applications this loop started: the one its record names by pid, when it names one.
 *
 * 🔴 By pid, not by path. After `run --install`, the record names the install's application,
 * and the person's own start of that install runs from the same path: a kill by path closes the
 * window they opened themselves. A record without a pid keeps the path's answer.
 */
export function startedHere(live, record) {
  return Number.isInteger(record?.pid) ? live.filter((pid) => pid === record.pid) : live;
}

/** `--install <dir>`, taken off an argument list. */
export function takeInstall(args) {
  const at = args.indexOf('--install');
  if (at === -1) return null;
  const directory = args[at + 1];
  if (!directory) fail('usage: --install <folder you published to>');
  args.splice(at, 2);
  return directory;
}

export const ago = (path) => {
  const minutes = Math.round((Date.now() - statSync(path).mtimeMs) / 60000);
  return minutes < 1 ? 'just now' : minutes < 90 ? `${minutes}m ago` : `${Math.round(minutes / 60)}h ago`;
};

export function pruneShots() {
  if (!existsSync(SHOTS)) return;
  const entries = readdirSync(SHOTS)
    .filter((file) => file.endsWith('.png'))
    .map((file) => {
      const path = join(SHOTS, file);
      const stat = statSync(path);
      return { path, at: stat.mtimeMs, size: stat.size };
    });
  for (const entry of prune(entries)) unlinkSync(entry.path);
}

/**
 * Take a `--window <name>` off an argument list, and say which window is asked for.
 *
 * One flag for both instruments, deliberately: `eval` and `click` reach a *page* over CDP and `shot`
 * photographs a *window* through the OS, and a tool that needed a URL parameter for one and a
 * caption for the other would be a tool people get wrong. Null is the application's own window.
 */
export function takeWindow(args) {
  const at = args.indexOf('--window');
  if (at === -1) return null;

  const name = args[at + 1];
  if (!name) fail('usage: --window <monitor|browser|session:ID>');
  args.splice(at, 2);
  return name;
}
