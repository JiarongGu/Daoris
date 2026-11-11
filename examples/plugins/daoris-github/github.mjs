// GitHub's REST API as this plugin calls it: where a checkout's origin is, the repository an address names, one call with the kept
// token, and the pull requests from a branch. The step's process and the pull request's commands share it.
import { existsSync, readFileSync, statSync } from 'node:fs';
import { isAbsolute, join, resolve } from 'node:path';
import { kept, refused } from './credential.mjs';

/** `origin`'s address as the checkout's git configuration writes it: read as a file, never by running git. */
export function originOf(root) {
  try {
    let folder = join(root, '.git');
    if (statSync(folder).isFile()) {
      // A linked work tree's `.git` is a file naming its own folder, whose `commondir` holds the configuration.
      const gitdir = /^gitdir:\s*(.+)$/m.exec(readFileSync(folder, 'utf8'))?.[1].trim();
      if (!gitdir) return null;
      folder = isAbsolute(gitdir) ? gitdir : resolve(root, gitdir);
      const common = join(folder, 'commondir');
      if (existsSync(common)) folder = resolve(folder, readFileSync(common, 'utf8').trim());
    }
    const config = readFileSync(join(folder, 'config'), 'utf8');
    const section = /\[remote "origin"\]([^[]*)/.exec(config)?.[1] ?? '';
    return /^\s*url\s*=\s*(.+?)\s*$/m.exec(section)?.[1] ?? null;
  } catch {
    return null;
  }
}

/** `{ host, owner, repo }` from an address, under Daoris's `daoris::` routing or not; null for one that names no such repository. */
export function repoOf(origin) {
  if (typeof origin !== 'string') return null;
  const text = origin.trim().replace(/^daoris::/, '');
  if (/^([A-Za-z]:[\\/]|[\\/.]|file:)/.test(text)) return null;
  const m = /^(?:[a-z][a-z0-9+.-]*:\/\/)?(?:[^@/]+@)?([^/:]+)(?::\d+)?[:/]+([^/]+)\/([^/]+?)(?:\.git)?\/*$/i.exec(text);
  return m ? { host: m[1].toLowerCase(), owner: m[2], repo: m[3] } : null;
}

function apiOf(host) {
  const named = process.env.DAORIS_HOST_API;
  if (named) return named.replace(/\/+$/, '');
  return host === 'github.com' ? 'https://api.github.com' : `https://${host}/api/v3`;
}

/** One API call: its status and JSON, or a thrown `Refusal` saying why nothing can be told (a transient code where it may pass). */
export class Refusal extends Error {
  constructor(code, message) {
    super(message);
    this.code = code;
  }
}

export const TRANSIENT = ['unavailable', 'rate-limited'];

export async function call(repo, method, path, body) {
  const token = kept()?.token;
  if (!token) throw new Refusal('no-token', 'no token is kept: run `daoris daoris-github login`');
  let response;
  try {
    response = await fetch(`${apiOf(repo.host)}${path}`, {
      method,
      headers: {
        Authorization: `Bearer ${token}`,
        Accept: 'application/vnd.github+json',
        'X-GitHub-Api-Version': '2022-11-28',
        'User-Agent': 'daoris-github',
        ...(body ? { 'Content-Type': 'application/json' } : {}),
      },
      body: body ? JSON.stringify(body) : undefined,
      signal: AbortSignal.timeout(30000),
    });
  } catch (error) {
    throw new Refusal('unavailable', `GitHub could not be reached: ${error?.message ?? error}`);
  }
  let json = null;
  try {
    json = await response.json();
  } catch { /* an answer with no body */ }
  if (response.status === 401 || response.status === 403 && !/rate limit/i.test(json?.message ?? '')) {
    refused();
    throw new Refusal('unauthorized', `GitHub refused the token (${response.status}): ${json?.message ?? 'no reason given'}`);
  }
  if (response.status === 429 || response.status === 403) throw new Refusal('rate-limited', 'GitHub is limiting this token for now');
  if (response.status >= 500) throw new Refusal('unavailable', `GitHub answered ${response.status}`);
  return { status: response.status, json };
}

export const repoPath = (repo) => `/repos/${encodeURIComponent(repo.owner)}/${encodeURIComponent(repo.repo)}`;

/** The pull requests from a branch, all states. */
export async function fromBranch(repo, branch) {
  const found = await call(repo, 'GET', `${repoPath(repo)}/pulls?head=${encodeURIComponent(`${repo.owner}:${branch}`)}&state=all&per_page=30`);
  return found.status === 200 && Array.isArray(found.json) ? found.json : [];
}

export const newest = (list) => [...list].sort((a, b) => b.number - a.number)[0];

/** GitHub's state in Daoris's words: open, completed (merged) or abandoned (closed unmerged). */
export const wordOf = (pr) => (pr.state === 'open' ? 'open' : pr.merged_at ? 'completed' : pr.state === 'closed' ? 'abandoned' : 'unknown');
