// The types of `listen-port.mjs`, for the CLI's tests that listen their stand-ins through it. The helper is plain JavaScript
// so the tools' own tests import it unbuilt; this declares what it exports.

import type { Server } from 'node:net';

export const BAD_PORTS: ReadonlySet<number>;

export function listenFetchable(server: Server, options?: { host?: string; first?: number }): Promise<string>;
