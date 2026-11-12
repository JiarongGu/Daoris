// A stand-in of Daoris's browser on this machine's loopback: the DevTools endpoint the plugin is handed as DAORIS_BROWSER, with tabs,
// and a page that runs an expression with a `fetch` that sends the person's session as a page does. The session is the stand-in
// Azure DevOps' (`api.session`); nothing real is reached. It keeps every call the plugin made, by tab, so a test can show what the
// plugin touched, and a tab of the person's that it must never touch.
import { after } from 'node:test';
import { createHash } from 'node:crypto';
import { createServer } from 'node:http';
import { runInNewContext } from 'node:vm';

const GUID = '258EAFA5-E914-47DA-95CA-C5AB0DC85B11';

/** A server frame, unmasked text, with the length forms a frame takes. */
function frame(text) {
  const body = Buffer.from(text);
  const head = body.length < 126 ? Buffer.from([0x81, body.length])
    : body.length < 65536 ? Buffer.from([0x81, 126, body.length >> 8, body.length & 255])
      : Buffer.concat([Buffer.from([0x81, 127, 0, 0, 0, 0]), Buffer.from([body.length >>> 24, (body.length >>> 16) & 255, (body.length >>> 8) & 255, body.length & 255])]);
  return Buffer.concat([head, body]);
}

/** The text frames a client's bytes hold (masked, whole), and what is left over. */
function frames(buffer) {
  const out = [];
  let at = 0;
  while (buffer.length - at >= 2) {
    const opcode = buffer[at] & 15;
    let length = buffer[at + 1] & 127;
    let head = 2;
    if (length === 126) { if (buffer.length - at < 4) break; length = buffer.readUInt16BE(at + 2); head = 4; }
    else if (length === 127) { if (buffer.length - at < 10) break; length = Number(buffer.readBigUInt64BE(at + 2)); head = 10; }
    if (buffer.length - at < head + 4 + length) break;
    const mask = buffer.subarray(at + head, at + head + 4);
    const data = Buffer.from(buffer.subarray(at + head + 4, at + head + 4 + length));
    for (let i = 0; i < data.length; i++) data[i] ^= mask[i % 4];
    if (opcode === 1) out.push(data.toString());
    at += head + 4 + length;
  }
  return [out, buffer.subarray(at)];
}

/**
 * The browser: `targets` (id → { url, closed }), `calls` (each `{ method, target }` the plugin's connection made), `navigations` (each
 * `{ target, url }`) and `evaluated` (each `{ target, expression }`). `elsewhere(url)` is another session opening and driving a tab of
 * its own. `person` is a tab of the person's, open before: the plugin must never use it. `api` is the stand-in Azure DevOps,
 * whose `session` the pages send and whose `navigated(url)` is told of each navigation.
 */
export async function browserStandIn(api, { person = 'https://dev.azure.com/acme/Proj/_git/app/pullrequest/9' } = {}) {
  const targets = new Map([['person-1', { url: person, closed: false, by: 'the person' }]]);
  const calls = [];
  const navigations = [];
  const evaluated = [];
  let ids = 0;
  const sessions = new Map();

  const origin = (url) => (/^https?:/.test(url) ? new URL(url).origin : 'null');
  const run = async (target, expression) => {
    const here = origin(target.url);
    const pageFetch = async (url, init = {}) => {
      if (origin(url) !== here) throw new TypeError('Failed to fetch');
      const headers = new Headers(init.headers);
      if (init.credentials === 'include' && headers.get('X-TFS-FedAuthRedirect') === 'Suppress' && api.session === 'in') headers.set('Cookie', 'stand-in-session=1');
      return fetch(url, { ...init, headers });
    };
    return runInNewContext(expression, { fetch: pageFetch, location: { origin: here }, JSON }, { timeout: 5000 });
  };

  const handle = async (send, message) => {
    const { id, method, params = {}, sessionId } = message;
    const target = sessionId ? sessions.get(sessionId) : null;
    calls.push({ method, target: target ?? null });
    const reply = (result) => send({ id, ...(sessionId ? { sessionId } : {}), result });
    if (method === 'Target.createTarget') {
      const made = `t-${++ids}`;
      targets.set(made, { url: params.url, closed: false, by: 'plugin' });
      return reply({ targetId: made });
    }
    if (method === 'Target.attachToTarget') {
      const session = `s-${++ids}`;
      sessions.set(session, params.targetId);
      return reply({ sessionId: session });
    }
    if (method === 'Target.closeTarget') {
      const held = targets.get(params.targetId);
      if (held) held.closed = true;
      return reply({ success: true });
    }
    if (method === 'Page.enable') return reply({});
    if (method === 'Page.navigate') {
      navigations.push({ target, url: params.url });
      targets.get(target).url = params.url;
      api.navigated?.(params.url);
      reply({ frameId: 'f-1' });
      return send({ method: 'Page.loadEventFired', params: { timestamp: 1 }, sessionId });
    }
    if (method === 'Runtime.evaluate') {
      evaluated.push({ target, expression: params.expression });
      try {
        const value = await run(targets.get(target), params.expression);
        return reply({ result: { type: typeof value, value } });
      } catch (error) {
        return reply({ result: { type: 'object' }, exceptionDetails: { text: 'Uncaught', exception: { description: String(error?.message ?? error) } } });
      }
    }
    return send({ id, error: { code: -32601, message: `${method} is not known to the stand-in` } });
  };

  const server = createServer((request, response) => {
    if (request.url === '/json/version') {
      response.writeHead(200, { 'content-type': 'application/json' });
      response.end(JSON.stringify({ Browser: 'stand-in', webSocketDebuggerUrl: `ws://127.0.0.1:${server.address().port}/devtools/browser/1` }));
    } else {
      response.writeHead(404).end();
    }
  });
  server.on('upgrade', (request, socket) => {
    const accept = createHash('sha1').update(request.headers['sec-websocket-key'] + GUID).digest('base64');
    socket.write(`HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: ${accept}\r\n\r\n`);
    let held = Buffer.alloc(0);
    const send = (value) => socket.writable && socket.write(frame(JSON.stringify(value)));
    socket.on('data', (chunk) => {
      const [texts, rest] = frames(Buffer.concat([held, chunk]));
      held = rest;
      for (const text of texts) void handle(send, JSON.parse(text));
    });
    socket.on('error', () => {});
  });
  await new Promise((done) => server.listen(0, '127.0.0.1', done));
  after(() => { server.closeAllConnections?.(); server.close(); });

  return {
    address: `http://127.0.0.1:${server.address().port}`,
    targets, calls, navigations, evaluated,
    /** Tabs the plugin opened and left open. */
    open: () => [...targets].filter(([, tab]) => tab.by === 'plugin' && !tab.closed).map(([name]) => name),
    /** Another session opens a tab of its own and drives it to `url`, which the plugin's tab must not follow. */
    elsewhere(url) {
      const made = `o-${++ids}`;
      targets.set(made, { url, closed: false, by: 'another session' });
      return made;
    },
  };
}
