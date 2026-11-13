import { mkdirSync, writeFileSync, existsSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { execGit } from '../fsx.mjs';

/**
 * A scratch repository for one case, a new folder in the workspace folder the caller names, with one commit so that work has a
 * base. It never reuses a folder and never touches a remote (it has none).
 */
export function createScratchRepository({ workspaceDir, caseId, stamp }) {
  if (!workspaceDir) throw new Error('--workspace-dir names the folder the scratch repository is made in');
  const name = `rcase-${caseId}-${stamp}`;
  const dir = resolve(workspaceDir, name);
  if (existsSync(dir)) throw new Error(`${name} already exists in the workspace folder`);
  mkdirSync(dir, { recursive: true });
  writeFileSync(join(dir, 'README.md'), `# ${name}\n\nA scratch repository made for real case ${caseId}. Safe to delete.\n`);
  const git = (...args) => execGit(['-C', dir, '-c', 'user.name=Daoris real case', '-c', 'user.email=real-case@localhost', ...args], { stdio: ['ignore', 'pipe', 'pipe'] });
  git('init', '-q', '-b', 'main');
  git('add', '-A');
  git('commit', '-q', '-m', 'Start the scratch repository');
  return { name, dir };
}
