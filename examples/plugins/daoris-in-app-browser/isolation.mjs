import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { scopeContext } from './scoped-context.mjs';

/** The agents set to an isolated context: `{ "isolated": ["codex", "role:reviewer", "*"] }` in the plugin's data folder; none where nothing is set. */
export function readIsolation(dataFolder) {
  try {
    const parsed = JSON.parse(readFileSync(join(dataFolder, 'isolation.json'), 'utf8'));
    return Array.isArray(parsed?.isolated) ? parsed.isolated.filter(entry => typeof entry === 'string').map(entry => entry.trim().toLowerCase()) : [];
  } catch {
    return [];
  }
}

/**
 * Whether this agent is set to an isolated context. A word names the agent by its MCP client name (`codex` is `codex-mcp-client`),
 * `role:<role>` by the role its session took, and `*` every agent.
 */
export function isIsolated(setting, { client, role }) {
  const name = client?.toLowerCase();
  const taken = role?.toLowerCase();
  return setting.some(entry => entry === '*'
    || (entry.startsWith('role:') ? taken !== undefined && entry === `role:${taken}` : name !== undefined && name.startsWith(entry)));
}

/** The localStorage of each origin the person has a page open on, as a context's storage state holds it. */
async function openOrigins(person) {
  const origins = new Map();
  for (const page of person.pages()) {
    let origin;
    try {
      const url = new URL(page.url());
      if (url.protocol !== 'https:' && url.protocol !== 'http:') continue;
      origin = url.origin;
    } catch {
      continue;
    }
    if (origins.has(origin)) continue;
    try {
      const entries = await page.evaluate(() => Object.entries(localStorage));
      origins.set(origin, entries.map(([name, value]) => ({ name, value })));
    } catch {
      // A page that will not say is left out: the session starts without that origin's storage.
    }
  }
  return [...origins].map(([origin, localStorage]) => ({ origin, localStorage }));
}

/**
 * The context a session drives: the person's own where it is shared, otherwise a context of its own that starts with the
 * person's cookies and the local storage of the origins the person has open, and gives nothing back.
 */
export async function openContext(browser, { isolated }) {
  const person = browser.contexts()[0];
  if (!isolated) return person;
  const storageState = { cookies: await person.cookies(), origins: await openOrigins(person) };
  return browser.newContext(storageState.cookies.length || storageState.origins.length ? { storageState } : {});
}

/**
 * What a session drives, by what its folder's setting says of its agent: the person's context seen only through the session's own
 * pages where shared, or a context of its own where isolated. `end` closes what the session made and nothing else.
 */
export async function chooseContext(browser, dataFolder, agent) {
  const isolated = isIsolated(readIsolation(dataFolder), agent);
  const context = await openContext(browser, { isolated });
  if (isolated) return { context, isolated, end: () => context.close() };
  const view = scopeContext(context);
  return { context: view, isolated, end: () => view.close() };
}
