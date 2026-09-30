/**
 * A minimal DevTools-Protocol client — enough to ask a running WebView2 what it is showing.
 *
 * Zero dependencies: Node ships a global `WebSocket`, which is all CDP needs. That matters here for
 * the same reason it matters in the CLI — a dev tool that drags in a driver library is a dev tool
 * nobody can run on a fresh clone without a network.
 *
 * Adapted from the family sibling's `devtools/scripts/cdp.mjs`, read read-only, and deliberately
 * trimmed: that copy also LAUNCHES a headless Chromium (profile directories, Edge-sync flags, target
 * disambiguation across restored tabs), because four of its tools drive a browser. Nothing here ever
 * launches a browser — the desktop shell is started by `desktop.mjs run`, which knows the port it
 * asked for — so the launching half would be carried code with no caller. What was kept is the part
 * that was bought with incidents: the id-matched client, and the rule that you identify a page
 * before reporting its answer.
 */

/**
 * Find a port nobody is listening on, starting at `preferred`.
 *
 * ⚠ A port is not ours because a browser answers on it. The sibling's suite once ran entirely inside
 * an unrelated application's WebView2 that happened to hold its hard-coded port: the checks that only
 * needed *a* page passed, and the ones that cared which host they were in read exactly like an app
 * regression. Pick a free one, and attach only to something we started.
 */
/**
 * Both loopback addresses. The engine binds its debug port to `localhost`, which one machine resolves to
 * IPv4 and another (or the same one, another day) to IPv6 alone: a port asked only on 127.0.0.1 then read
 * as free while the shell held it on [::1], and every instrument found nothing listening (LOOK3).
 */
export const LOOPBACKS = ['127.0.0.1', '[::1]'];

export async function freePort(preferred, span = 20) {
  for (let port = preferred; port < preferred + span; port += 1) {
    // ANY answer means something is listening — not just an OK one. Testing `response.ok` would call
    // a port held by some other HTTP server free, and the shell would then fail to bind it.
    let taken = false;
    for (const host of LOOPBACKS) {
      taken = await fetch(`http://${host}:${port}/json/version`, { signal: AbortSignal.timeout(700) })
        .then(() => true)
        .catch(() => false);
      if (taken) break;
    }
    if (!taken) return port;
  }
  throw new Error(`no free port in ${preferred}..${preferred + span - 1}`);
}

/**
 * Choose the page to drive.
 *
 * Not `targets.find(t => t.type === 'page')`: the first page is only the one you meant when nothing
 * else is open. It guards against attaching to a devtools window or a foreign target and reporting
 * its answer as the shell's.
 *
 * 🔴 **The shell no longer has exactly one page** (SURF8). A secondary window — the monitor, a
 * detached session — is a route into the same bundle, so it is a second WebView2 on the same debug
 * port, and which one comes back first is the browser's order rather than the caller's choice.
 * Measured: with the monitor open, `eval` attached to it and then refused, because the monitor's
 * URL carries a query the main window's does not.
 *
 * @param window - null for the application's own window (the page with no `window=`), or a window
 * name (`monitor`, `session:<id>`) for that one.
 */
export function pickPageTarget(targets, window = null) {
  const pages = (targets ?? [])
    .filter((t) => t.type === 'page' && !String(t.url).startsWith('devtools://'));

  const named = (target) => {
    try {
      return new URL(target.url).searchParams.get('window');
    } catch {
      // A target with no parseable URL is not a window anybody asked for.
      return null;
    }
  };

  return pages.find((target) => named(target) === window) ?? null;
}

/** The targets a CDP endpoint is serving, or null when nothing is listening there. */
export async function targetsAt(port) {
  for (const host of LOOPBACKS) {
    const response = await fetch(`http://${host}:${port}/json/list`, { signal: AbortSignal.timeout(2000) })
      .catch(() => null);
    if (response) return response.json();
  }
  return null;
}

/** One socket, id-matched replies. */
export class Cdp {
  constructor(url) {
    this.ws = new WebSocket(url);
    this.id = 0;
    this.pending = new Map();
  }

  open() {
    return new Promise((resolve, reject) => {
      this.ws.addEventListener('open', () => resolve(this));
      // Wrapped rather than passing `reject` straight in: the raw handler rejects with an Event, and
      // "[object Event]" in a failed run says nothing about what went wrong.
      this.ws.addEventListener('error', () => reject(new Error('CDP socket failed')));
      this.ws.addEventListener('message', (event) => {
        const message = JSON.parse(event.data);
        const slot = this.pending.get(message.id);
        if (!slot) return;
        this.pending.delete(message.id);
        if (message.error) slot.reject(new Error(message.error.message));
        else slot.resolve(message.result);
      });
    });
  }

  send(method, params = {}) {
    const id = (this.id += 1);
    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      this.ws.send(JSON.stringify({ id, method, params }));
    });
  }

  /** Evaluate an expression, awaiting a promise result, and return its JSON value. */
  async evaluate(expression) {
    const result = await this.send('Runtime.evaluate', {
      expression,
      awaitPromise: true,
      returnByValue: true,
    });
    if (result.exceptionDetails) {
      throw new Error(result.exceptionDetails.exception?.description ?? 'the expression threw');
    }

    // An expression with no value — a bare statement, or one the page navigated away from — has no
    // `result` at all, and reading `.value` off undefined turns a benign nothing into a TypeError.
    return result.result?.value;
  }

  close() {
    try {
      this.ws.close();
    } catch {
      /* already gone */
    }
  }
}
