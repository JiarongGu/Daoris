// Azure DevOps' REST API as this plugin calls it: where the checkout's origin says the repository is, each call with the handed token,
// and an answer the step cannot go on from thrown as a `Refusal` with the code the step fails by.
import { existsSync, readFileSync, statSync } from 'node:fs';
import { isAbsolute, join, resolve } from 'node:path';
import { kept, refused } from './credential.mjs';

const VERSION = 'api-version=7.1';
// Release management and the organization's `connectionData` are asked under a preview version, which a released API also takes.
const PREVIEW = 'api-version=7.1-preview';

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

/**
 * `{ base, releases, project, repository }` from an Azure DevOps address (HTTPS, a `visualstudio.com` one, or SSH v3): `base` the
 * organization's API root, `releases` its release management's; else null. A named base (DAORIS_HOST_API) answers both.
 */
export function azureOf(origin) {
  if (typeof origin !== 'string') return null;
  const text = origin.trim().replace(/^daoris::/, '');
  const decode = (part) => { try { return decodeURIComponent(part); } catch { return part; } };
  const named = process.env.DAORIS_HOST_API?.replace(/\/+$/, '');
  const cloud = (org) => ({ base: `${named ?? 'https://dev.azure.com'}/${org}`, releases: named ? `${named}/${org}` : `https://vsrm.dev.azure.com/${org}` });
  const hosted = (org) => (named ? { base: `${named}/${org}`, releases: `${named}/${org}` } : { base: `https://${org}.visualstudio.com`, releases: `https://${org}.vsrm.visualstudio.com` });
  let m = /^https?:\/\/(?:[^@/]+@)?dev\.azure\.com\/([^/]+)\/([^/]+)\/_git\/([^/?#]+?)\/?$/i.exec(text);
  if (m) return { ...cloud(m[1]), project: decode(m[2]), repository: decode(m[3]) };
  m = /^https?:\/\/(?:[^@/]+@)?([^./]+)\.visualstudio\.com\/(?:DefaultCollection\/)?([^/]+)\/_git\/([^/?#]+?)\/?$/i.exec(text);
  if (m) return { ...hosted(m[1]), project: decode(m[2]), repository: decode(m[3]) };
  m = /^(?:ssh:\/\/)?(?:[^@/]+@)?(ssh\.dev\.azure\.com|vs-ssh\.visualstudio\.com)[:/]v3\/([^/]+)\/([^/]+)\/([^/]+?)\/?$/i.exec(text);
  if (m) return { ...(m[1].toLowerCase() === 'ssh.dev.azure.com' ? cloud(m[2]) : hosted(m[2])), project: decode(m[3]), repository: decode(m[4]) };
  return null;
}

/** A thrown reason with the code the step fails with; a transient code is one the manifest lets Daoris retry. */
export class Refusal extends Error {
  constructor(code, message) {
    super(message);
    this.code = code;
  }
}

export const TRANSIENT = ['unavailable', 'rate-limited'];

async function ask(method, url, body, version) {
  const token = kept()?.token;
  if (!token) throw new Refusal('no-token', 'no personal access token is kept: run `daoris daoris-azure-devops login`');
  let response;
  try {
    response = await fetch(`${url}${url.includes('?') ? '&' : '?'}${version}`, {
      method,
      headers: {
        Authorization: `Basic ${Buffer.from(`:${token}`).toString('base64')}`,
        Accept: 'application/json',
        ...(body ? { 'Content-Type': 'application/json' } : {}),
      },
      body: body ? JSON.stringify(body) : undefined,
      signal: AbortSignal.timeout(30000),
    });
  } catch (error) {
    throw new Refusal('unavailable', `Azure DevOps could not be reached: ${error?.message ?? error}`);
  }
  let json = null;
  try {
    json = await response.json();
  } catch { /* an answer with no body, or a sign-in page */ }
  // A refused personal access token is sent on to a sign-in page (203) as often as it is answered 401.
  if ([401, 403, 203].includes(response.status) || (response.status === 200 && json === null)) {
    const why = typeof json?.message === 'string' ? `: ${json.message.slice(0, 160)}` : '';
    refused();
    throw new Refusal('unauthorized', `Azure DevOps refused the token (${response.status})${why}`);
  }
  if (response.status === 429) throw new Refusal('rate-limited', 'Azure DevOps is limiting this token for now');
  if (response.status >= 500) throw new Refusal('unavailable', `Azure DevOps answered ${response.status}`);
  return { status: response.status, json };
}

const apis = (repo) => `${encodeURIComponent(repo.project)}/_apis`;

/** A call on the repository's own Git API. */
export const call = (repo, method, path, body) =>
  ask(method, `${repo.base}/${apis(repo)}/git/repositories/${encodeURIComponent(repo.repository)}${path}`, body, VERSION);

/** A call on the project's API (builds, their timelines). */
export const project = (repo, method, path, body) => ask(method, `${repo.base}/${apis(repo)}${path}`, body, VERSION);

/** A call on the organization's API (`connectionData`). */
export const organization = (repo, method, path) => ask(method, `${repo.base}/_apis${path}`, null, PREVIEW);

/** A call on the project's release management API. */
export const releases = (repo, method, path, body) => ask(method, `${repo.releases}/${apis(repo)}/release${path}`, body, PREVIEW);

export const heads = (branch) => `refs/heads/${branch}`;
export const withoutHeads = (ref) => (typeof ref === 'string' ? ref.replace(/^refs\/heads\//, '') : null);

/** The pull requests from a branch, all statuses. */
export async function fromBranch(repo, branch) {
  const found = await call(repo, 'GET', `/pullrequests?searchCriteria.sourceRefName=${encodeURIComponent(heads(branch))}&searchCriteria.status=all`);
  return found.status === 200 && Array.isArray(found.json?.value) ? found.json.value : [];
}

/** The address a person opens: the repository's web address and the pull request's id. */
export const addressOf = (repo, pr) => `${pr.repository?.webUrl ?? `${repo.base}/${encodeURIComponent(repo.project)}/_git/${encodeURIComponent(repo.repository)}`}/pullrequest/${pr.pullRequestId}`;

/** The pull request's id an address names, else undefined. */
export const idOf = (address) => (typeof address === 'string' ? /\/pullrequest\/(\d+)\/?$/i.exec(address)?.[1] : undefined);

/**
 * Which of the pull requests from a branch is the one asked about: among those into the line where there are
 * any, a completed one, else an active one, else the newest abandoned one.
 */
export function choose(found, line) {
  const into = found.filter((each) => withoutHeads(each?.targetRefName) === line);
  const pool = line && into.length > 0 ? into : found;
  const newest = (list) => [...list].sort((a, b) => String(b.closedDate ?? b.creationDate ?? '').localeCompare(String(a.closedDate ?? a.creationDate ?? ''))
    || Number(b.pullRequestId) - Number(a.pullRequestId))[0];
  return newest(pool.filter((each) => each?.status === 'completed'))
    ?? newest(pool.filter((each) => each?.status === 'active'))
    ?? newest(pool.filter((each) => each?.status === 'abandoned'))
    ?? null;
}

/** Where a unit's checkout says its repository is, or a `no-origin` refusal. */
export function located(unit) {
  const repo = azureOf(originOf(unit.root));
  if (!repo) throw new Refusal('no-origin', `${unit.root} has no origin that names an Azure DevOps repository`);
  return repo;
}

/** Words cut to what an evidence message holds. */
export const fit = (text) => (text.length <= 300 ? text : `${text.slice(0, 299)}…`);
