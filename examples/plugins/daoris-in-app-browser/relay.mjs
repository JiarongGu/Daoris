/**
 * A loopback WebSocket in front of a browser's DevTools socket: Playwright connects here, and what the browser says passes
 * through `filter` (a factory taking the function that sends to Playwright and the one that sends to the browser, and returning
 * the function that receives each message of the browser's, as text; it may have an `end`). What Playwright says goes through as it is.
 *
 * @param ws The `WebSocket` and `WebSocketServer` classes of the `ws` package (Playwright's own bundle carries it).
 * @returns The relay's address, and `close`.
 */
export async function startRelay({ upstream, ws: { WebSocket, WebSocketServer }, filter }) {
  const server = new WebSocketServer({ host: '127.0.0.1', port: 0 });
  await new Promise((resolve, reject) => {
    server.once('listening', resolve);
    server.once('error', reject);
  });

  server.on('connection', client => {
    const host = new WebSocket(upstream, { perMessageDeflate: false, maxPayload: 256 * 1024 * 1024 });
    const waiting = [];
    const toHost = text => {
      if (host.readyState === 1) host.send(text);
      else waiting.push(text);
    };
    const fromHost = filter(text => { if (client.readyState === 1) client.send(text); }, toHost);

    host.on('open', () => waiting.splice(0).forEach(text => host.send(text)));
    host.on('message', data => fromHost(data.toString()));
    host.on('close', () => client.close());
    host.on('error', () => client.close());
    client.on('message', data => toHost(data.toString()));
    client.on('close', () => {
      fromHost.end?.();
      host.close();
    });
    client.on('error', () => host.close());
  });

  return {
    url: `ws://127.0.0.1:${server.address().port}/`,
    close: () => new Promise(resolve => {
      for (const client of server.clients) client.terminate();
      server.close(() => resolve());
    }),
  };
}
