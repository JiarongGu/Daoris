import { createServer, type Server, type Socket } from 'node:net';
import { once } from 'node:events';
import { randomUUID } from 'node:crypto';

/** What the stand-in does with a press (the driver tests' `StandInPress`). */
export type StandInPress = 'answer' | 'refuse' | 'drop' | 'hold';

/** Whether a named pipe can be served here: Windows only, as the driver's own pipe tests are. */
export const ON_WINDOWS = process.platform === 'win32';

/** A request as the stand-in was sent it. */
export type Sent = Record<string, unknown> & { id: string; module: string; type: string; payload?: unknown; key?: string };

/**
 * A stand-in for the running Daoris's pipe (RPC1f): the wire of the terminal RPC design §6 as RPC1a's `InstancePipe` speaks it, a
 * hello, a press and `RESULT`, each answered as a test sets it, with every line it was sent kept. The twin of the driver tests'
 * `InstanceStandIn.cs`, case for case: in process, on a name of the test home's own, a `node:net` server on `\\.\pipe\<name>`.
 */
export class InstanceStandIn {
  /** The home it answers as. */
  homeId: string;
  /** The hello's refusal code, or null to answer it. */
  refuseHello: string | null = null;
  /** The `why` a `stopping` refusal names. */
  stoppingWhy = 'close';
  press: StandInPress = 'answer';
  /** A press's answer. */
  data: unknown = { done: true, message: 'done' };
  /** An answer made from the route and its payload, where one press's answer differs from the next's; else `data`. */
  respond: ((type: string, payload: unknown) => unknown) | null = null;
  /** A press's refusal code. */
  code = 'confirm-in-window';
  /** A progress line told before the answer, or null for none. */
  progress: string | null = null;
  /** Whether a press is first told to wait for the person in the window (RPC1c). */
  confirming = false;
  /** `RESULT`'s state for a press it was asked about: `answered` (with `data`), `running`, `unknown`, or `id-taken`. */
  result: 'answered' | 'running' | 'unknown' | 'id-taken' = 'answered';
  connections = 0;
  /** Every request it was sent, the hello and `RESULT` among them. */
  readonly sent: Sent[] = [];

  readonly name: string;
  private readonly server: Server;
  private readonly sockets = new Set<Socket>();

  private constructor(name: string, homeId: string, server: Server) {
    this.name = name;
    this.homeId = homeId;
    this.server = server;
  }

  /** A stand-in serving `name`, answering as the home whose id is `homeId`. */
  static async serve(name: string, homeId: string): Promise<InstanceStandIn> {
    let standIn: InstanceStandIn | null = null;
    const server = createServer((socket) => standIn!.converse(socket));
    standIn = new InstanceStandIn(name, homeId, server);
    server.listen(pipePath(name));
    await once(server, 'listening');
    return standIn;
  }

  /** The presses it was sent: every request that is not the pipe's own. */
  get presses(): Sent[] {
    return this.sent.filter((each) => each.module !== 'DAORIS.PIPE');
  }

  /** The `RESULT`s it was asked. */
  get results(): Sent[] {
    return this.sent.filter((each) => each.type === 'RESULT');
  }

  async close(): Promise<void> {
    for (const socket of this.sockets) socket.destroy();
    await new Promise<void>((done) => this.server.close(() => done()));
  }

  private converse(socket: Socket): void {
    this.connections += 1;
    this.sockets.add(socket);
    socket.on('close', () => this.sockets.delete(socket));
    socket.on('error', () => { /* The terminal went, or the stand-in is ending. */ });
    let held = '';
    let dropped = false;
    socket.on('data', (chunk) => {
      held += chunk.toString('utf8');
      for (let at = held.indexOf('\n'); at !== -1 && !dropped; at = held.indexOf('\n')) {
        const line = held.slice(0, at);
        held = held.slice(at + 1);
        dropped = this.answer(socket, JSON.parse(line) as Sent);
      }
    });
  }

  /** Answers one request as the test set; true where the connection was dropped. */
  private answer(socket: Socket, request: Sent): boolean {
    this.sent.push(request);
    const { id, module, type } = request;
    if (module === 'DAORIS.PIPE' && type === 'HELLO') {
      write(socket, this.refuseHello !== null
        ? refusal(id, this.refuseHello, this.refuseHello === 'stopping' ? { why: this.stoppingWhy } : null)
        : success(id, { protocol: 1, home: this.homeId, version: '0', pid: process.pid }));
      return false;
    }

    if (module === 'DAORIS.PIPE' && type === 'RESULT') {
      const asked = (request.payload as { id: string }).id;
      if (this.result === 'id-taken') {
        write(socket, refusal(id, 'id-taken', null));
        return false;
      }
      write(socket, success(id, this.result === 'answered'
        ? { state: 'answered', answer: JSON.parse(success(asked, this.data)) as unknown }
        : { state: this.result }));
      return false;
    }

    if (this.confirming) {
      write(socket, JSON.stringify({
        category: 'notification',
        id: randomUUID(),
        payload: [{ module: 'DAORIS.PIPE', type: 'CONFIRMING', payload: { id, expires: new Date(Date.now() + 120_000).toISOString() } }],
      }));
    }

    switch (this.press) {
      case 'drop':
        socket.destroy();
        return true;
      case 'hold':
        return false;
      case 'refuse':
        write(socket, refusal(id, this.code, null));
        return false;
    }

    if (this.progress !== null) {
      write(socket, JSON.stringify({
        category: 'notification',
        id: randomUUID(),
        payload: [{ module: 'SHENORA.REQUESTS', type: 'REQUEST_UPDATED', payload: { id, detail: { text: this.progress } } }],
      }));
    }

    write(socket, success(id, this.respond ? this.respond(type, request.payload) : this.data));
    return false;
  }
}

/** Where Windows serves a pipe of this name. */
export const pipePath = (name: string): string => `\\\\.\\pipe\\${name}`;

/** A pipe name of the home whose id is `id`, as the driver's `PipeName.For` makes it, its session a number of this test's own. */
export const pipeOf = (id: string): string => `daoris-${id}-${100_000 + Math.floor(Math.random() * 900_000)}`;

const success = (id: string, data: unknown): string => JSON.stringify({ category: 'ipc', id, success: true, data });

const refusal = (id: string, code: string, parameters: unknown): string =>
  JSON.stringify({ category: 'ipc', id, success: false, error: { code, message: `refused ${code}.`, parameters } });

function write(socket: Socket, line: string): void {
  socket.write(`${line}\n`);
}
