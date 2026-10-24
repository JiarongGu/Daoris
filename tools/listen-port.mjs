/**
 * Import-safe: a test server's listen on a port Node's fetch will reach (FLAKE1's sighting of 2026-10-24).
 *
 * The system's port 0 hands out any free port, and the WHATWG Fetch standard refuses a list of them ("bad port",
 * https://fetch.spec.whatwg.org/#port-blocking), which Node's fetch follows: a stand-in once handed 2049 was never asked.
 */

/**
 * The Fetch standard's bad ports, as its port-blocking table lists them (undici's `badPorts`, which Node's fetch runs, is the
 * same list). `listen-port.test.mjs` holds that Node's fetch refuses each one, so a port the standard adds that this lacks is
 * found by the next sighting rather than guessed.
 */
export const BAD_PORTS = new Set([
  1, 7, 9, 11, 13, 15, 17, 19, 20, 21, 22, 23, 25, 37, 42, 43, 53, 69, 77, 79, 87, 95, 101, 102, 103, 104, 109, 110, 111, 113,
  115, 117, 119, 123, 135, 137, 139, 143, 161, 179, 389, 427, 465, 512, 513, 514, 515, 526, 530, 531, 532, 540, 548, 554, 556,
  563, 587, 601, 636, 989, 990, 993, 995, 1719, 1720, 1723, 2049, 3659, 4045, 4190, 5060, 5061, 6000, 6566, 6665, 6666, 6667,
  6668, 6669, 6679, 6697, 10080,
]);

/** How many listens it takes before it gives up: one blocked port in a row is rare, twenty is a broken system. */
const TRIES = 20;

function listenOnce(server, port, host) {
  return new Promise((resolve, reject) => {
    const failed = (error) => reject(error);
    server.once('error', failed);
    server.listen(port, host, () => {
      server.off('error', failed);
      resolve(server.address().port);
    });
  });
}

/**
 * Listens `server` on `host`, on port 0, and listens again while the port it was handed is one fetch refuses; its url,
 * `http://host:port`. `first` is the port the first listen asks for (a test hands a blocked one; 0 otherwise). A listen
 * that fails is the caller's error, never retried.
 */
export async function listenFetchable(server, { host = '127.0.0.1', first = 0 } = {}) {
  let port = await listenOnce(server, first, host);
  for (let tries = 1; BAD_PORTS.has(port); tries += 1) {
    await new Promise((resolve) => server.close(resolve));
    if (tries >= TRIES) throw new Error(`the system handed ${TRIES} ports in a row that fetch refuses, the last ${port}`);
    port = await listenOnce(server, 0, host);
  }
  return `http://${host}:${port}`;
}
