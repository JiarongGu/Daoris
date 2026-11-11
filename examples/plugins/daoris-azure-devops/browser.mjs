// Azure DevOps through the person's signed-in session in Daoris's own browser, for when no token is kept or Azure DevOps refuses it.
// Daoris hands this process the browser's DevTools endpoint as DAORIS_BROWSER (the manifest's `hooks.browser`). The plugin opens a tab of
// its own there, in the browser's signed-in profile (a context of its own would carry no sign-in), and holds only that tab: it never
// selects or navigates another, so a session driving the browser beside it cannot move it. Each API call is a `fetch` run INSIDE that
// page with the page's own credentials (`credentials: 'include'`); nothing here reads a cookie, a storage, a header the page sends or the
// browser's cookie store, so no bearer token or cookie ever reaches this process, its data, its logs or its evidence.
// A sign-in that lapses (a 401, a 203, or a sign-in page for JSON) is renewed once by navigating the page to the
// organization, which renews the single sign-on silently; a second lapse is a clear failure, not a second renewal.
import { AsyncLocalStorage } from 'node:async_hooks';

const store = new AsyncLocalStorage();

/** The DevTools endpoint Daoris handed this process, or null where it carries no browser (a headless host, the plugin run by hand). */
export const endpoint = () => process.env.DAORIS_BROWSER?.replace(/\/+$/, '') || null;

/** Whether a call can be made through the browser now. */
export const available = () => endpoint() !== null && typeof WebSocket === 'function';

const WAIT = 30000;

/** One connection to the browser's DevTools endpoint: calls answered by id, events handed to whoever waits for them. */
class Cdp {
  constructor(socket) {
    this.socket = socket;
    this.next = 0;
    this.pending = new Map();
    this.events = [];
    socket.addEventListener('message', (message) => {
      let frame;
      try { frame = JSON.parse(String(message.data)); } catch { return; }
      if (frame.id !== undefined) {
        const wait = this.pending.get(frame.id);
        this.pending.delete(frame.id);
        if (wait) frame.error ? wait.reject(new Error(String(frame.error.message ?? 'the browser refused the call'))) : wait.resolve(frame.result ?? {});
      } else {
        for (const wait of [...this.events]) if (wait.test(frame)) { this.events.splice(this.events.indexOf(wait), 1); wait.resolve(frame); }
      }
    });
    socket.addEventListener('close', () => {
      for (const wait of this.pending.values()) wait.reject(new Error('the browser closed the connection'));
      this.pending.clear();
    });
  }

  send(method, params = {}, sessionId) {
    const id = ++this.next;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => { this.pending.delete(id); reject(new Error(`the browser did not answer ${method}`)); }, WAIT);
      this.pending.set(id, { resolve: (value) => { clearTimeout(timer); resolve(value); }, reject: (error) => { clearTimeout(timer); reject(error); } });
      this.socket.send(JSON.stringify({ id, method, params, ...(sessionId ? { sessionId } : {}) }));
    });
  }

  /** The next event `test` accepts, within WAIT; `listen` first, so an event that comes before the call returns is not missed. */
  listen(test) {
    let wait;
    const promise = new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error('the page did not finish loading')), WAIT);
      wait = { test, resolve: (frame) => { clearTimeout(timer); resolve(frame); } };
      this.events.push(wait);
    });
    return promise;
  }

  close() {
    try { this.socket.close(); } catch { /* already closed */ }
  }
}

async function connect(address) {
  const version = await fetch(`${address}/json/version`, { signal: AbortSignal.timeout(WAIT) }).then((response) => response.json());
  const url = typeof version?.webSocketDebuggerUrl === 'string' ? version.webSocketDebuggerUrl : null;
  if (!url) throw new Error('the browser names no DevTools address');
  const socket = new WebSocket(url);
  await new Promise((resolve, reject) => {
    socket.addEventListener('open', resolve, { once: true });
    socket.addEventListener('error', () => reject(new Error('the browser could not be connected to')), { once: true });
  });
  return new Cdp(socket);
}

/** The page-side call: a `fetch` with the page's own credentials, answered as `{ status, type, text }`. Run in the page, never here. */
const PAGE_CALL = `async (request) => {
  const response = await fetch(request.url, {
    method: request.method,
    credentials: 'include',
    headers: { 'X-TFS-FedAuthRedirect': 'Suppress', Accept: 'application/json', ...(request.body === null ? {} : { 'Content-Type': 'application/json' }) },
    body: request.body === null ? undefined : request.body,
  });
  const text = await response.text();
  return { status: response.status, type: response.headers.get('content-type') ?? '', text };
}`;

/** One tab of this process's own, held by its target. */
class Page {
  constructor(cdp, targetId, session) {
    this.cdp = cdp;
    this.targetId = targetId;
    this.session = session;
  }

  static async open(cdp) {
    const { targetId } = await cdp.send('Target.createTarget', { url: 'about:blank', background: true });
    try {
      const { sessionId } = await cdp.send('Target.attachToTarget', { targetId, flatten: true });
      await cdp.send('Page.enable', {}, sessionId);
      return new Page(cdp, targetId, sessionId);
    } catch (error) {
      await cdp.send('Target.closeTarget', { targetId }).catch(() => {});
      throw error;
    }
  }

  async evaluate(expression) {
    const { result, exceptionDetails } = await this.cdp.send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true }, this.session);
    if (exceptionDetails) throw new Error(String(exceptionDetails.exception?.description ?? exceptionDetails.text ?? 'the page failed'));
    return result?.value;
  }

  async navigate(url) {
    const loaded = this.cdp.listen((frame) => frame.method === 'Page.loadEventFired' && frame.sessionId === this.session);
    loaded.catch(() => {});
    const done = await this.cdp.send('Page.navigate', { url }, this.session);
    if (done.errorText) throw new Error(`the page could not open ${new URL(url).origin}: ${done.errorText}`);
    await loaded;
  }

  origin() {
    return this.evaluate('location.origin');
  }

  call(method, url, body) {
    return this.evaluate(`(${PAGE_CALL})(${JSON.stringify({ url, method, body: body ? JSON.stringify(body) : null })})`);
  }

  async close() {
    await this.cdp.send('Target.closeTarget', { targetId: this.targetId }).catch(() => {});
  }
}

/** A scope's use of the browser: one connection, one tab for each origin asked, and the ways its calls were made. */
class Use {
  constructor() {
    this.cdp = null;
    this.pages = new Map();
    this.ways = new Set();
    this.declined = false;
  }

  async page(origin) {
    this.cdp ??= await connect(endpoint());
    let page = this.pages.get(origin);
    if (!page) {
      page = await Page.open(this.cdp);
      this.pages.set(origin, page);
    }
    return page;
  }

  async close() {
    for (const page of this.pages.values()) await page.close();
    this.pages.clear();
    this.cdp?.close();
    this.cdp = null;
  }
}

/** Run `work` in a scope that closes the tabs it opened however it ends, so none is left behind. */
export async function within(work) {
  if (store.getStore()) return work();
  const use = new Use();
  try {
    return await store.run(use, work);
  } finally {
    await use.close();
  }
}

const used = () => store.getStore();

/** Note a call made in `way`: `token`, or `browser` (`afterRefusal`: the kept token is refused). */
export function noted(way, afterRefusal = false) {
  const use = used();
  if (!use) return;
  use.ways.add(way);
  if (afterRefusal) use.declined = true;
}

/** Whether the scope's browser calls follow a refused token. */
export const declined = () => used()?.declined === true;

/** The way this scope's calls went, said for evidence; empty where none went. */
export function wayWords() {
  const use = used();
  if (!use || use.ways.size === 0) return '';
  if (use.ways.has('browser')) return use.declined ? 'Way: the browser (the kept token was refused).' : 'Way: the browser.';
  return 'Way: the token.';
}

/** The ways as a word for data: `browser`, `token`, or null where none. */
export const wayOf = () => (used()?.ways.has('browser') ? 'browser' : used()?.ways.has('token') ? 'token' : null);

/** A sign-in the page lost: a 401, the sign-in redirect's 203, or a page of HTML for JSON. */
const lapsed = (reply) => [401, 203].includes(reply.status) || (reply.status < 300 && !/json/i.test(reply.type) && reply.text.trim() !== '');

/**
 * `method url` made from the page, as `{ status, json }`, or `{ lapsed: true }` where the sign-in lapsed even after one renewal.
 * `root` is the organization's address on the call's own host, which the page is taken to first and to renew by.
 */
export async function through(method, url, body, root) {
  const use = used();
  if (!use) throw new Error('a call through the browser is made inside `within`');
  const origin = new URL(url).origin;
  const page = await use.page(origin);
  const home = `${root}/_apis/connectionData?api-version=7.1-preview`;
  if ((await page.origin()) !== origin) await page.navigate(home);
  let reply = await page.call(method, url, body);
  if (lapsed(reply)) {
    // One renewal: going to the organization renews the single sign-on silently where the person is still signed in.
    await page.navigate(home);
    reply = await page.call(method, url, body);
    if (lapsed(reply)) return { lapsed: true };
  }
  let json = null;
  try { json = JSON.parse(reply.text); } catch { /* an answer with no body */ }
  return { status: reply.status, json };
}
