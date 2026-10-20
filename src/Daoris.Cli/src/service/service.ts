// **The only module in this CLI permitted to touch the network**, and the reason the offline
// guarantee is scoped rather than absolute.
//
// D8 says `check` works offline, because it runs inside build gates and a gate that can fail on a
// network call is not a gate. That is about the DOCTRINE operations — `check`, `sync`, `index`,
// `upstream` — all of which are pure local hashing against the lock and stay that way.
//
// The MANAGEMENT commands are a different thing (D50): explicit, opt-in conversations with a
// knowledge service — `connect`, `retire`, `import` — never run by a gate and never on the path of
// anything that is. A repository that never runs them loses nothing but discoverability. They all
// speak through this one module, so the guarantee stays testable as one sentence: exactly one file
// here reaches the network, and nothing a doctrine command imports can reach it.
//
// Two tests enforce exactly that shape. The second is the one that matters — a gate breaks by an
// innocuous import three modules deep acquiring a socket, not by an obvious `fetch`.
//
// It reaches one other kind of host (AGT2b): a vendor's release channel, for `agent pin` (and for
// `agent update`, which asks the channel for its newest release and then pins it, USE1a). That is the
// same shape of conversation — a person asked for it, and no gate runs it — so it lives here rather
// than giving the toolchain a socket of its own; the dispatcher hands the fetcher in.
//
// And since TOOLS4, a tool's maker and a resource location, for `daoris tool download|use … managed|update|look`
// (D121 §3.6, §3.7): the same fetcher, its hops held to the address rule the dispatcher names.
//
// And since RPC1f, the running Daoris's own pipe (D171 point 3, the terminal RPC design §9): a management verb that edits what a
// window's route edits sends that route while Daoris runs, so the instance's own handler judges, writes and publishes it, and takes
// the home's lease where none runs (`instanceDoor`). Node opens a Windows named pipe with `net.connect`, so the wire is here too.

import { createWriteStream, mkdirSync, readdirSync, readFileSync, statSync, unlinkSync } from 'node:fs';
import { createHash, randomBytes, randomUUID } from 'node:crypto';
import { once } from 'node:events';
import { connect, type Socket } from 'node:net';
import { dirname, join } from 'node:path';
import { finished } from 'node:stream/promises';
import { DaorisError, RefusalError, type ExitCode } from '../platform/errors.ts';
import { writeTextAtomic } from '../platform/fsx.ts';
import { daorisHome, homeEntry, homeId, isHomeId, THIS_INSTALL } from '../platform/home.ts';
import { leaseForVerb, leaseHolder, leaseNamed } from '../platform/lease.ts';
import type { InstanceRoute, VerbDoor, VerbPress } from '../platform/door.ts';
import type { Fetcher } from '../agents/channels.ts';

const NO_SERVICE =
  'no DAORIS_SERVICE_URL — this command talks to a knowledge service, and needs\n'
  + '  to know where one is. Set it in your environment; a local service is usually\n'
  + '  http://localhost:5177. Everything else daoris does works without one.';

/**
 * Where the service is, and the key it wants — supplied by the environment, never committed. With no variable, a `daoris`
 * the install carries finds the service the running desktop holds (FINDDAORIS1, D171 point 1, `runningService`), and refuses
 * saying Daoris is not running there and how to start it. A checkout reads no address file: on a machine with an install, the
 * account's `DAORIS_HOME` names that install's live home, and a test that cleared the variable must never reach it.
 */
export async function endpoint(
  env: NodeJS.ProcessEnv = process.env, get: Get = fetch, install: string | null = THIS_INSTALL,
): Promise<{ url: string; key: string | null }> {
  const found = await located(env, get, install);
  if ('why' in found) throw new DaorisError(found.why ? `${NO_SERVICE}\n  ${found.why}` : NO_SERVICE);
  return { url: found.url, key: env.DAORIS_SERVICE_KEY ?? null };
}

/** The service's address: the variable, else (installed) the running desktop's, else why there is none ('' where none was looked for). */
async function located(env: NodeJS.ProcessEnv, get: Get, install: string | null): Promise<{ url: string } | { why: string }> {
  const named = env.DAORIS_SERVICE_URL?.trim().replace(/\/+$/, '');
  if (named) return { url: named };
  const home = install ? daorisHome(env, install) : null;
  return install && home ? runningService(home, install, get) : { why: '' };
}

/** The address file the running desktop leaves in its home (the driver's `RunningAddress.FileName`). */
export const RUNNING_FILE = 'running.json';

/**
 * The service the running desktop holds for `home` (FINDDAORIS1, D171 point 1), from the address it left there: taken only
 * while the process it names lives, the address is this machine's (a terminal's verbs send this machine's paths, D46), and
 * its status door answers as this home's Daoris host: naming its search tier and this home's id (HOMEID1, `home.json`),
 * asked with redirects off, since an address that sends its asker elsewhere has not answered. This CLI cannot read another
 * process's start, so a process id the system gave to another process since lives to it: the id is what tells. Otherwise why
 * not, in a sentence that says how to start Daoris. The driver's `RunningService` holds the same contract in C#.
 */
export async function runningService(
  home: string, install: string, get: Get = fetch, lives: (pid: number) => boolean = processLives,
): Promise<{ url: string; pipe?: string } | { why: string }> {
  const start = join(install, 'Daoris.exe');
  const entry = runningEntry(home);
  if (!entry || !lives(entry.pid)) return { why: `Daoris is not running on ${home}: start it (${start}), and run this again once its window is up.` };
  if (!isLocalService(entry.url)) {
    return {
      why: `the address Daoris left in ${home} (${entry.url}) is not on this machine, and only this machine's own service `
        + 'is taken from it; set DAORIS_SERVICE_URL to reach another.',
    };
  }

  let answered: { home: string | null } | null = null;
  try {
    const response = await get(`${entry.url}/api/status`, { method: 'GET', redirect: 'manual', signal: AbortSignal.timeout(2_000) });
    const status: unknown = await response.json().catch(() => null);
    if (response.ok && typeof status === 'object' && status !== null && typeof (status as Record<string, unknown>).tier === 'string') {
      const named = (status as Record<string, unknown>).home;
      answered = { home: isHomeId(named) ? named : null };
    }
  } catch {
    answered = null;
  }

  if (!answered) {
    return {
      why: `Daoris is running on ${home} (pid ${entry.pid}), and nothing answered as its service at ${entry.url}: run this `
        + `again once its window is up, or start it again (${start}).`,
    };
  }

  const mine = homeId(home);
  return mine && answered.home === mine
    ? { url: entry.url, ...(entry.pipe && entry.home === mine ? { pipe: entry.pipe } : {}) }
    : {
      why: `Daoris is running on ${home} (pid ${entry.pid}), and the service at ${entry.url} answered as `
        + `${answered.home ? "another Daoris home's" : "no Daoris home's"}, so nothing was sent to it: start Daoris on this home `
        + `(${start}), or set DAORIS_SERVICE_URL to the service you mean.`,
    };
}

/**
 * What every Daoris pipe's name begins with (RPC1a, the terminal RPC design §4.1): `daoris-<the home's id>-<the Windows
 * session's id>`. The driver's `PipeName.Prefix` is its copy; the two share no code (`twins.md`).
 */
export const PIPE_PREFIX = 'daoris-';

/** Whether `name` is the pipe of the home whose id is `id`: the prefix, that id and a session's number, nothing else (the driver's `PipeName.IsOf`). */
export function isPipeOf(name: unknown, id: unknown): name is string {
  if (typeof name !== 'string' || !isHomeId(id)) return false;
  const start = `${PIPE_PREFIX}${id}-`;
  return name.startsWith(start) && /^[0-9]{1,10}$/.test(name.slice(start.length));
}

/**
 * What a home's address file names, or null when there is none or it does not read. Its `pipe` (RPC1a) is read only where it
 * is the pipe of the home the file names, as the driver's `RunningAddress.Read` reads it.
 */
function runningEntry(home: string): { url: string; pid: number; home?: string; pipe?: string } | null {
  try {
    const entry: unknown = JSON.parse(readFileSync(homeEntry(home, 'running'), 'utf8'));
    if (typeof entry !== 'object' || entry === null) return null;
    const { url, pid, home: named, pipe } = entry as Record<string, unknown>;
    if (typeof url !== 'string' || url.length === 0 || !Number.isInteger(pid)) return null;
    return {
      url: url.replace(/\/+$/, ''),
      pid: pid as number,
      ...(isHomeId(named) ? { home: named } : {}),
      ...(isPipeOf(pipe, named) ? { pipe } : {}),
    };
  } catch {
    return null;
  }
}

/** Whether a process runs with this id: signal 0 tests and sends nothing; one not ours to signal still runs. */
function processLives(pid: number): boolean {
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return (error as NodeJS.ErrnoException).code === 'EPERM';
  }
}

// ------------------------------------------------------------------------------------------- the running Daoris's pipe (RPC1f)

/** The pipe the dev loop and a rehearsal name, as they name the service (§7.4): the driver's `InstanceDoor.Variable`. */
export const INSTANCE_VARIABLE = 'DAORIS_INSTANCE';

/** The pipe's own module and the wire's version, as `InstancePipe` names them (the driver's `InstanceClient.PipeModule`, `Protocol`). */
const PIPE_MODULE = 'DAORIS.PIPE';
const PIPE_PROTOCOL = 1;

/** The instance's refusals that are the person's word on a press (RPC1c): refused in the window, or nobody confirmed in two minutes. */
const PERSON_ANSWERS: ReadonlySet<string> = new Set(['refused', 'expired']);

/** How long the pipe is given to connect and answer the hello (§7.2: five seconds), and a press its answer before `RESULT` is asked. */
const ANSWER_MS = 5_000;
const WAIT_MS = 30 * 60_000;

/** How long a sent press is kept to be asked again: the instance's journal keeps its lines as long (§6.1, the driver's `SentPresses.Kept`). */
const SENT_KEPT_MS = 24 * 60 * 60_000;

/** What a verb finds when it looks for the running Daoris (§7.2's rows; the driver's `InstanceFound`). */
export type InstanceFound =
  /** No instance on this home, or a workspace build, which reads no address file: the verb acts alone. */
  | 'none'
  /** A headless `drive` loop holds the home: alone under the lease's rule, until a headless loop serves the pipe. */
  | 'headless'
  /** The instance answered its hello on this home: the verb is its client. */
  | 'answers'
  /** The instance lives and its pipe did not answer, or it names none: a press is refused, never done alone. */
  | 'silent'
  /** The hello was refused (`protocol`, `home`, `stopping`), or the pipe refused this terminal (PIPEOWNER1): a press is refused, saying which. */
  | 'refused';

/** What a verb found, with the sentence that says it. */
export interface InstanceLook {
  readonly found: InstanceFound;
  readonly why: string;
  /** Lets the connection go where the instance answered; nothing otherwise. */
  close(): void;
}

/** Where a verb looks for the running Daoris, and where it says what it hears: the process's own, or a test's. */
export interface InstanceWorld {
  /** The home the verb acts on, or null where it has none. */
  readonly home: string | null;
  /** The variables; the process's where absent. */
  readonly env?: NodeJS.ProcessEnv;
  /** The install this `daoris` runs from, null for a checkout (which reads no address file); this package's where absent. */
  readonly install?: string | null;
  /** How the address's status door is asked; `fetch` where absent. */
  readonly get?: Get;
  /** Whether a process lives; signal 0 where absent. */
  readonly lives?: (pid: number) => boolean;
  /** Where the answer's words and the route's progress are said: the verb's own `write`. */
  readonly write: (line: string) => void;
  /** Where the door says it waits for the window, so the output stays the answer's; the standard error where absent. */
  readonly say?: (line: string) => void;
  /** How long the pipe is given to connect and answer the hello, in milliseconds. */
  readonly answer?: number;
  /** How long a press is waited on before its answer is asked for by `RESULT`, in milliseconds. */
  readonly wait?: number;
  /** How long a verb acting alone waits on another act's lease before it is refused, in milliseconds (§5: thirty seconds). */
  readonly leaseWait?: number;
  /** How the pipe is opened; `net.connect` where absent. A test's hands the refusal of a pipe's ACL, which Node cannot serve. */
  readonly connect?: (path: string) => Socket;
}

type Envelope = Record<string, unknown>;

/**
 * What a pipe that refused this terminal is said as (PIPEOWNER1), never as a pipe that did not answer: the driver's
 * `InstanceDoor.NotThisUsers`, word for word (`twins.md`).
 */
const NOT_THIS_USERS = 'Its pipe belongs to another Windows account or elevation.';

/** The codes Node's connect says a pipe's ACL refused it by: Windows' access denied, which libuv says as `EPERM`. */
const REFUSED_CODES: ReadonlySet<string> = new Set(['EPERM', 'EACCES']);

/** A connect to the pipe: the client where it connected; else whether the pipe refused this terminal, or nothing took it in time. */
interface PipeConnect {
  readonly client: PipeClient | null;
  readonly refused: boolean;
}

/** Where Windows serves a pipe of this name; the CLI opens it as the driver's `NamedPipeClientStream` does, by name on this machine. */
const pipePath = (name: string): string => `\\\\.\\pipe\\${name}`;

/**
 * A terminal's connection to the running Daoris's pipe (the driver's `InstanceClient`): UTF-8 lines of JSON, LF; the hello first;
 * then the kit's request envelope with `key` beside it; answers the kit's `{ category: "ipc", id, success, data?, error? }`, and a
 * route's progress the kit's notification envelope naming the request's id. One request at a time.
 *
 * Node cannot ask Windows which process serves a pipe, so where the driver also holds the pipe's server to the address file's
 * process, this client takes the hello's answer on this home's id as what tells (`twins.md`).
 */
class PipeClient {
  /** The pipe's name. */
  readonly pipe: string;
  private readonly socket: Socket;
  private held = '';
  private readonly lines: string[] = [];
  private ended = false;
  private wake: (() => void) | null = null;

  private constructor(pipe: string, socket: Socket) {
    this.pipe = pipe;
    this.socket = socket;
    socket.on('data', (chunk: Buffer) => {
      this.held += chunk.toString('utf8');
      for (let at = this.held.indexOf('\n'); at !== -1; at = this.held.indexOf('\n')) {
        this.lines.push(this.held.slice(0, at));
        this.held = this.held.slice(at + 1);
      }
      this.poke();
    });
    const end = (): void => {
      this.ended = true;
      this.poke();
    };
    socket.on('close', end);
    socket.on('error', end);
  }

  /**
   * The connection to `pipe`; or none, `refused` where the pipe's ACL refused this terminal (PIPEOWNER1: Node reads no owner, so the
   * driver's refusal of an owner it does not take has no twin here), else where it did not take one within `within` milliseconds.
   */
  static connect(pipe: string, within: number, open: (path: string) => Socket = connect): Promise<PipeConnect> {
    return new Promise((resolve) => {
      const socket = open(pipePath(pipe));
      const failed = (error?: NodeJS.ErrnoException): void => {
        clearTimeout(timer);
        socket.destroy();
        resolve({ client: null, refused: REFUSED_CODES.has(error?.code ?? '') });
      };
      const timer = setTimeout(failed, within);
      socket.once('error', failed);
      socket.once('connect', () => {
        clearTimeout(timer);
        socket.removeListener('error', failed);
        resolve({ client: new PipeClient(pipe, socket), refused: false });
      });
    });
  }

  /** The hello's answer, the whole envelope, or null where none came within `within`. */
  hello(home: string | null, within: number): Promise<Envelope | null> {
    return this.request(randomUUID(), PIPE_MODULE, 'HELLO', { protocol: PIPE_PROTOCOL, home, client: 'daoris', version: cliVersion() },
      null, null, within);
  }

  /** `RESULT { id, key }`: how the press `id` ended, the whole envelope, or null where none came. */
  result(id: string, key: string, within: number): Promise<Envelope | null> {
    return this.request(randomUUID(), PIPE_MODULE, 'RESULT', { id, key }, null, null, within);
  }

  /**
   * One request, and its answer's whole envelope; null where the connection was lost or nothing answered within `within`. Each
   * notification on the way is handed to `told`.
   */
  async request(
    id: string, module: string, type: string, payload: unknown, key: string | null, told: ((envelope: Envelope) => void) | null,
    within: number,
  ): Promise<Envelope | null> {
    if (this.ended) return null;
    this.socket.write(`${JSON.stringify(key === null ? { id, module, type, payload } : { id, module, type, payload, key })}\n`);
    const until = Date.now() + within;
    for (;;) {
      const line = await this.next(until);
      if (line === null) return null;
      let read: unknown;
      try {
        read = JSON.parse(line);
      } catch {
        // Not the wire: nothing it says is an answer.
        continue;
      }
      if (!isObject(read)) continue;
      if (read.category === 'notification') {
        told?.(read);
        continue;
      }
      if (read.id === id) return read;
    }
  }

  close(): void {
    this.socket.destroy();
  }

  /** The next line, or null once the connection ended with none left or `until` passed. */
  private next(until: number): Promise<string | null> {
    if (this.lines.length > 0) return Promise.resolve(this.lines.shift()!);
    const left = until - Date.now();
    if (this.ended || left <= 0) return Promise.resolve(null);
    return new Promise((resolve) => {
      const timer = setTimeout(() => {
        this.wake = null;
        resolve(null);
      }, left);
      this.wake = () => {
        clearTimeout(timer);
        this.wake = null;
        resolve(this.lines.shift() ?? null);
      };
    });
  }

  private poke(): void {
    if (this.wake && (this.lines.length > 0 || this.ended)) this.wake();
  }
}

/** What was found, and the connection where the instance answered. */
interface Look {
  readonly found: InstanceFound;
  readonly why: string;
  readonly client: PipeClient | null;
}

/**
 * What the verb finds on its home (§7.4; the driver's `InstanceDoor.FindAsync`): the instance whose address file's process lives,
 * whose file and status answer this home's id, and whose pipe answers the hello on it; or the pipe `DAORIS_INSTANCE` names, whose
 * hello answers this home's id.
 *
 * @remarks
 * 🔴 **A workspace build reads no address file** (D171's FINDDAORIS1 note): on a machine with an install the account's `DAORIS_HOME`
 * names that install's live home, and a gate or a test that met a running Daoris would act on it. The dev loop and a rehearsal that
 * mean to reach an instance name its pipe in `DAORIS_INSTANCE`.
 */
export async function findInstance(world: InstanceWorld): Promise<InstanceLook> {
  const look = await find(world);
  return { found: look.found, why: look.why, close: () => look.client?.close() };
}

async function find(world: InstanceWorld): Promise<Look> {
  const mine = world.home ? homeId(world.home) : null;
  const named = (world.env ?? process.env)[INSTANCE_VARIABLE]?.trim();
  if (named) {
    return hello(named, mine, world, `the Daoris ${INSTANCE_VARIABLE} names (${named}) did not answer; nothing was done.`,
      `the Daoris ${INSTANCE_VARIABLE} names (${named}) refused this terminal; nothing was done.`);
  }

  const install = world.install === undefined ? THIS_INSTALL : world.install;
  if (!install || !world.home) return { found: 'none', why: 'a workspace build reads no address file.', client: null };

  const lives = world.lives ?? processLives;
  const holder = leaseHolder(world.home, lives);
  if (holder?.kind === 'headless') return { found: 'headless', why: `${world.home} is held by ${leaseNamed(holder)}.`, client: null };

  const entry = runningEntry(world.home);
  if (!entry || !lives(entry.pid)) return { found: 'none', why: 'Daoris is not running on this home.', client: null };

  // HOMEID1: only this home's instance. A file naming another home's id, or a status answering as another's, is not it.
  const answered = mine && (entry.home === undefined || entry.home === mine) ? await statusHome(entry.url, world.get ?? fetch) : null;
  if (!mine || (entry.home !== undefined && entry.home !== mine) || (answered !== null && answered !== mine)) {
    return { found: 'none', why: 'the Daoris running here answers as another home\'s.', client: null };
  }

  const running = `Daoris is running on this home (\`Daoris.exe\`, pid ${entry.pid}) and did not answer; nothing was done.`;
  if (!entry.pipe) {
    return {
      found: 'silent',
      why: `${running} It names no pipe for a terminal to send this to: a Daoris from before the terminal's pipe, or one that `
        + 'could not serve it, which its machine log says.',
      client: null,
    };
  }

  return hello(entry.pipe, mine, world, running,
    `Daoris is running on this home (\`Daoris.exe\`, pid ${entry.pid}) and refused this terminal; nothing was done.`);
}

/** The home id a Daoris host's status door names, asked with redirects off; null where it does not answer as one, or names none. */
async function statusHome(url: string, get: Get): Promise<string | null> {
  if (!isLocalService(url)) return null;
  try {
    const response = await get(`${url}/api/status`, { method: 'GET', redirect: 'manual', signal: AbortSignal.timeout(2_000) });
    const status: unknown = await response.json().catch(() => null);
    return response.ok && isObject(status) && typeof status.tier === 'string' && isHomeId(status.home) ? status.home : null;
  } catch {
    return null;
  }
}

/** The pipe connected and its hello said, or why not. */
async function hello(pipe: string, mine: string | null, world: InstanceWorld, silent: string, refused: string): Promise<Look> {
  const within = world.answer ?? ANSWER_MS;
  const seconds = `${Number((within / 1000).toFixed(1))} seconds`;
  const { client, refused: turnedAway } = await PipeClient.connect(pipe, within, world.connect);
  if (turnedAway) return { found: 'refused', why: `${refused} ${NOT_THIS_USERS}`, client: null };
  if (!client) return { found: 'silent', why: `${silent} Its pipe did not answer within ${seconds}.`, client: null };

  const answer = await client.hello(mine, within);
  if (!answer) {
    client.close();
    return { found: 'silent', why: `${silent} Its pipe did not answer the hello within ${seconds}.`, client: null };
  }

  if (answer.success === true && mine !== null && isObject(answer.data) && answer.data.home === mine) {
    return { found: 'answers', why: '', client };
  }

  client.close();
  const { code, message, why } = refusalOf(answer);
  return {
    found: 'refused',
    why: code === 'stopping' && why === 'update' ? 'Daoris is updating; run this again once it is back.'
      : code === 'stopping' ? 'Daoris is closing; run this again once it is back.'
        : code === 'protocol'
          ? `Daoris on this home speaks another version of the terminal's pipe (${message}); nothing was done: run this from the same `
            + 'build as the desktop.'
          : `the Daoris at ${pipe} runs on another home (${code ?? 'home'}: ${message}); nothing was done.`,
    client: null,
  };
}

/**
 * A press of `route` as §7.2's switch takes it (the driver's `InstanceDoor.PressAsync`): `alone` where no instance runs on this home
 * (or a headless loop holds it); sent where the instance answers, its answer said by `words`; refused, exit 2, where an instance lives
 * and does not answer or refuses the hello. A window's route is never sent. The exit code is the verb's; a refusal is thrown, as
 * every refusal of this CLI is, for the dispatcher to say.
 *
 * @remarks
 * 🔴 **A timeout never means "do it here"** (§7.2): once a request was sent, a lost answer is asked for by `RESULT` with the request's
 * key on a new connection, and what it says is said; the press is never sent again nor done alone.
 *
 * **A person's press is confirmed in the window** (RPC1c): the instance shows it there naming this terminal, and the door says it
 * waits as the instance tells it so (`CONFIRMING`); the person's refusal or the two minutes' end is the instance's sentence, exit 1.
 *
 * The press is kept before it is sent, `{ id, key, route, at }` in `<home>/pipe/sent/<id>.json` for a day, the driver's own record
 * (RPC1j), so `daoris-driver result <id>` can ask for an answer that never reached this terminal.
 */
export async function pressInstance(
  route: InstanceRoute, payload: unknown, alone: () => ExitCode | Promise<ExitCode>,
  words: (answer: unknown) => ExitCode | Promise<ExitCode>, world: InstanceWorld,
): Promise<ExitCode> {
  if (route.class === 'window') return alone();

  const look = await find(world);
  switch (look.found) {
    case 'none':
    case 'headless':
      return alone();
    case 'silent':
    case 'refused':
      throw new DaorisError(look.why, 2);
  }

  const client = look.client!;
  const id = randomUUID();
  const key = randomBytes(16).toString('hex');
  if (world.home) keepSent(world.home, { id, key, route: `${route.module}.${route.route}`, at: new Date() });
  let answer: Envelope | null;
  try {
    answer = await client.request(id, route.module, route.route, payload ?? null, key, (told) => tell(told, id, world),
      world.wait ?? WAIT_MS);
  } finally {
    client.close();
  }

  // The answer was lost (the connection, or the verb's wait over): asked for, never sent again, never done here (§6.1).
  if (answer === null) {
    const result = await resultOf(client.pipe, id, key, world);
    const data = result?.success === true && isObject(result.data) ? result.data : null;
    if (data?.state === 'answered' && isObject(data.answer)) {
      answer = data.answer;
    } else {
      throw new DaorisError(data?.state === 'unknown'
        ? `Daoris does not know how it ended (request ${id}): it stopped while this was under way, and nothing was done again here.`
        : `Daoris was still working on it (request ${id}), and its answer did not reach this terminal; nothing was done again here.`, 2);
    }
  }

  if (answer.success === true) return words(answer.data);
  const { code, message } = refusalOf(answer);
  // RPC1c: the person's refusal in the window, and the two minutes with no answer, are the person's word: a policy answer, exit 1.
  throw new DaorisError(`${message} (${code ?? ''})`, code !== null && PERSON_ANSWERS.has(code) ? 1 : 2);
}

/**
 * The door the management rows hand their verbs (§7.2, §5): a verb with a route is the running Daoris's client through
 * `pressInstance`; where none runs, and for a verb no route takes, it acts alone holding the home's lease for its act, waiting on
 * another verb's as the lease says and refused, exit 1, once that wait is over. Beside an instance's lease the finder did not find
 * (a workspace build, which reads no address file) it acts holding nothing, as the lease's twin does.
 */
export function instanceDoor(world: InstanceWorld): VerbDoor {
  return (press) => {
    const leased = () => underLease(world, press);
    return press.route ? pressInstance(press.route, press.payload, leased, press.words, world) : leased();
  };
}

/** The verb's own act, holding the home's lease while it acts (HOMELEASE1). */
async function underLease(world: InstanceWorld, press: VerbPress): Promise<ExitCode> {
  if (!world.home) return press.alone();
  const taken = await leaseForVerb(world.home, press.what, {
    ...(world.lives ? { lives: world.lives } : {}), ...(world.leaseWait !== undefined ? { within: world.leaseWait, poll: 10 } : {}),
  });
  if ('refusal' in taken) throw new DaorisError(taken.refusal, 1);
  try {
    return await press.alone();
  } finally {
    taken.lease.release();
  }
}

/** `RESULT` for a press whose answer was lost, asked on a new connection to the pipe the verb found; null where none came. */
async function resultOf(pipe: string, id: string, key: string, world: InstanceWorld): Promise<Envelope | null> {
  const within = world.answer ?? ANSWER_MS;
  const { client: again } = await PipeClient.connect(pipe, within, world.connect);
  if (!again) return null;
  try {
    const said = await again.hello(world.home ? homeId(world.home) : null, within);
    return said?.success === true ? await again.result(id, key, within) : null;
  } finally {
    again.close();
  }
}

/**
 * A route's progress, said as a line where it names this press and says something (§6); and that the press waits for the person in
 * the window (RPC1c), said on the door's own channel, so the output stays the answer's.
 */
function tell(told: Envelope, id: string, world: InstanceWorld): void {
  if (!Array.isArray(told.payload)) return;
  for (const each of told.payload as unknown[]) {
    if (!isObject(each) || !isObject(each.payload) || each.payload.id !== id) continue;
    if (each.module === PIPE_MODULE && each.type === 'CONFIRMING') {
      (world.say ?? sayOnError)("daoris: waiting for you to confirm this in Daoris's window…");
      continue;
    }
    const detail = each.payload.detail;
    if (isObject(detail) && typeof detail.text === 'string' && detail.text.length > 0) world.write(`  ${detail.text}`);
  }
}

/** A refusal's code, sentence and `why`, as the kit's error envelope carries them. */
function refusalOf(envelope: Envelope): { code: string | null; message: string; why: string | null } {
  if (!isObject(envelope.error)) return { code: null, message: 'Daoris answered no answer it could read', why: null };
  const { code, message, parameters } = envelope.error;
  return {
    code: typeof code === 'string' ? code : null,
    message: typeof message === 'string' ? message : 'it said no more',
    why: isObject(parameters) && typeof parameters.why === 'string' ? parameters.why : null,
  };
}

/** A press kept before it is sent (RPC1j): never a failure of the press, which is still sent and then cannot be asked again. */
function keepSent(home: string, sent: { id: string; key: string; route: string; at: Date }): void {
  try {
    const file = homeEntry(home, 'pipe', 'sent', `${sent.id}.json`);
    const folder = dirname(file);
    mkdirSync(folder, { recursive: true });
    for (const name of readdirSync(folder).filter((each) => each.endsWith('.json'))) {
      try {
        if (sent.at.getTime() - statSync(join(folder, name)).mtimeMs > SENT_KEPT_MS) unlinkSync(join(folder, name));
      } catch {
        // Held by another verb: left for the next look.
      }
    }
    writeTextAtomic(file, `${JSON.stringify({ id: sent.id, key: sent.key, route: sent.route, at: sent.at.toISOString() })}\n`);
  } catch {
    // A press whose record cannot be written is still sent: the verb then says its id and cannot be asked again.
  }
}

function sayOnError(line: string): void {
  process.stderr.write(`${line}\n`);
}

/** This build's version, as the hello names it. */
function cliVersion(): string {
  try {
    const read: unknown = JSON.parse(readFileSync(new URL('../../package.json', import.meta.url), 'utf8'));
    return isObject(read) && typeof read.version === 'string' ? read.version : '0';
  } catch {
    return '0';
  }
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * Whether a service URL points at this machine. The hostname, not a substring — a remote host named
 * `localhost.example.com` is exactly the trap a substring check walks into. An unparseable URL answers
 * false: when in doubt, the machine path stays home.
 */
export function isLocalService(url: string): boolean {
  try {
    const { hostname } = new URL(url);
    return hostname === 'localhost' || hostname === '127.0.0.1' || hostname === '::1' || hostname === '[::1]';
  } catch {
    return false;
  }
}

/**
 * One request, with the service's own sentence preserved.
 *
 * @remarks
 * A refusal here is an answer the service composed deliberately — who is addressable, what a retire
 * does not do, why an import found nothing. Replacing it with a status code would make the CLI say
 * less than the service said, which is the failure the driver's own client already learned once.
 *
 * A door whose records are this machine's alone (BRAINSTORM1a3: a brainstorm, D176 point 8) names why in `here`, and is asked of
 * this machine's own service only: a service elsewhere is refused before anything is sent, as `import` refuses a folder.
 */
export async function request(
  method: 'GET' | 'POST' | 'DELETE', path: string, body?: unknown, { here }: { here?: string } = {},
): Promise<{ status: number; json: Record<string, unknown> | null; url: string }> {
  const { url, key } = await endpoint();
  if (here !== undefined && !isLocalService(url)) {
    throw new DaorisError(`${url} is not this machine's service, and ${here}: nothing was sent.`, 1);
  }

  const response = await fetch(`${url}${path}`, {
    method,
    headers: {
      ...(body === undefined ? {} : { 'content-type': 'application/json' }),
      ...(key ? { authorization: `Bearer ${key}` } : {}),
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  }).catch((error: Error) => {
    throw new DaorisError(
      `could not reach the service at ${url} — ${error.message}\n`
      + '  Nothing else daoris does needs it; this only affects discoverability.');
  });

  const json = await response.json().catch(() => null) as Record<string, unknown> | null;
  return { status: response.status, json, url };
}

/** The service's own sentence, or a bare status when it did not compose one. */
export function refusal(status: number, json: Record<string, unknown> | null): string {
  return typeof json?.error === 'string' ? json.error : `the service answered ${status}`;
}

/** The records door the driver reads its strikes from, closed records included (DRV6). */
const RECORDS_PATH = '/api/sessions?includeClosed=true';

/** Why a read has no host to ask: no variable, and where the running desktop's address was looked for, why it was not taken. */
function noHost(why: string): string {
  const none = 'no DAORIS_SERVICE_URL is set, so this terminal knows no host to ask (the desktop\'s own is usually http://localhost:5177)';
  return why ? `${none}; ${why}` : none;
}

/**
 * This machine's session records, for `daoris driver retry` to count a quest's failures from as the driver does (RETRY1b):
 * the list the host answered, or why it could not be read, in words the retry's refusal carries. It never throws, so a
 * terminal that cannot read them says so and how to go on without them, rather than guessing a mark.
 *
 * @remarks
 * Only a host on this machine is asked: the strikes are this machine's decision about spending (D58), counted from the
 * records its own host keeps, and a shared deployment's records are the team's. The type is `strikes.ts`'s
 * `SessionRecords`, spelled here so this module imports nothing a management verb's pure half holds.
 */
export async function sessionRecords(
  env: NodeJS.ProcessEnv = process.env, get: Get = fetch, install: string | null = THIS_INSTALL,
): Promise<{ records: unknown[] } | { unread: string }> {
  const found = await located(env, get, install);
  if ('why' in found) return { unread: noHost(found.why) };
  const { url } = found;
  if (!isLocalService(url)) {
    return { unread: `DAORIS_SERVICE_URL names ${url}, which is not this machine, and the strikes are counted from this machine's own host` };
  }

  const key = env.DAORIS_SERVICE_KEY;
  let response: Response;
  try {
    response = await get(`${url}${RECORDS_PATH}`, { method: 'GET', headers: key ? { authorization: `Bearer ${key}` } : {} });
  } catch (error) {
    return { unread: `the service at ${url} did not answer: ${error instanceof Error ? error.message : String(error)}` };
  }

  const json: unknown = await response.json().catch(() => null);
  if (!response.ok) {
    const said = typeof json === 'object' && json !== null && typeof (json as Record<string, unknown>).error === 'string'
      ? `: ${(json as Record<string, unknown>).error as string}` : '';
    return { unread: `the service at ${url} answered ${response.status}${said}` };
  }

  return Array.isArray(json) ? { records: json } : { unread: `the service at ${url} answered no list of records` };
}

/**
 * Each registered repository's workspace and checkout on this machine, for `daoris driver review` to look for a procedure in
 * (REVIEWENV1a, design §1.1): the registry the host answers, or why it could not be read. It never throws, so a terminal that
 * cannot read it writes the rule and says it was not checked.
 *
 * @remarks
 * Only a host on this machine is asked: a checkout's path is answered only to a local caller (D46), and the check is of
 * this machine's checkouts. The type is `reviews.ts`'s `Checkout`, spelled here so this module imports nothing it holds.
 */
export async function registryCheckouts(
  env: NodeJS.ProcessEnv = process.env, get: Get = fetch, install: string | null = THIS_INSTALL,
): Promise<{ checkouts: { repository: string; workspace: string | null; root: string | null }[] } | { unread: string }> {
  const found = await located(env, get, install);
  if ('why' in found) return { unread: noHost(found.why) };
  const { url } = found;
  if (!isLocalService(url)) {
    return { unread: `DAORIS_SERVICE_URL names ${url}, which is not this machine, and only this machine's host answers where its checkouts are` };
  }

  const key = env.DAORIS_SERVICE_KEY;
  let response: Response;
  try {
    response = await get(`${url}/api/registry`, { method: 'GET', headers: key ? { authorization: `Bearer ${key}` } : {} });
  } catch (error) {
    return { unread: `the service at ${url} did not answer: ${error instanceof Error ? error.message : String(error)}` };
  }

  const json: unknown = await response.json().catch(() => null);
  if (!response.ok) return { unread: `the service at ${url} answered ${response.status}` };
  if (!Array.isArray(json)) return { unread: `the service at ${url} answered no list of repositories` };
  return {
    checkouts: json
      .filter((row): row is Record<string, unknown> => typeof row === 'object' && row !== null && typeof (row as Record<string, unknown>).repository === 'string')
      .map((row) => ({
        repository: row.repository as string,
        workspace: typeof row.workspace === 'string' ? row.workspace : null,
        root: typeof row.root === 'string' && row.root.length > 0 ? row.root : null,
      })),
  };
}

/**
 * How a fetcher may follow a host (TOOLS4, D121 §3.6). Absent, it follows redirects as `fetch` does, as a maker's
 * release channel always has been.
 */
export interface FetchRules {
  /**
   * Whether an address may be fetched from: every hop is held to it — the first address and each redirect, which
   * are then followed here, one at a time, rather than by `fetch`. A tool's download and a resource location are
   * held to `isAddress`: https://, or http:// to this machine.
   */
  hop?: (url: string) => boolean;
  /** How many redirects one fetch follows before it is refused. */
  redirects?: number;
  /** How long a small read (`bytes`) may wait, in milliseconds; a download (`save`) waits for the person's stop. */
  bound?: number;
}

/** What `fetch` is to this module: handed in by a test, which answers as a host without opening a socket. */
export type Get = (url: string, init: RequestInit) => Promise<Response>;

/**
 * A vendor's release channel, over HTTPS (AGT2b), and since TOOLS4 a tool's download and a resource location,
 * held to the rules the caller names. It fetches and nothing else: every judgement about what arrived — the
 * signature, the hashes, the version, the list — is `channels.ts`'s or `toolinstall.ts`'s, where it is tested
 * without a network.
 *
 * @remarks
 * A download is streamed to disk and hashed on the way, never held whole: a Claude Code binary is
 * over 200 MB. A 404 answers null, because "that version is not there" is an answer the caller words;
 * anything else that is not a 2xx is refused here with the address and the status, as `unreachable`.
 */
export function releaseFetcher(rules: FetchRules = {}, request: Get = fetch): Fetcher {
  return {
    async bytes(url) {
      const response = await get(url, rules.bound);
      return response ? Buffer.from(await response.arrayBuffer()) : null;
    },

    async save(url, to) {
      const response = await get(url);
      if (!response?.body) return null;

      const hash = createHash('sha256');
      let size = 0;
      const out = createWriteStream(to);
      try {
        for await (const chunk of response.body as AsyncIterable<Uint8Array>) {
          hash.update(chunk);
          size += chunk.length;
          if (!out.write(chunk)) await once(out, 'drain');
        }
      } finally {
        out.end();
        await finished(out);
      }

      return { sha256: hash.digest('hex'), size };
    },
  };

  async function get(url: string, bound?: number): Promise<Response | null> {
    const { hop, redirects = 10 } = rules;
    let at = url;
    for (let hops = 0; ; hops += 1) {
      if (hop && !hop(at)) {
        throw new RefusalError('address', hops === 0
          ? `${at} is not https://, or http:// to this machine — nothing was fetched from it.`
          : `${url} redirected to ${at}, which is not https://, or http:// to this machine — nothing was fetched from it.`);
      }

      const response = await request(at, {
        redirect: hop ? 'manual' : 'follow',
        headers: { 'user-agent': 'daoris' },
        ...(bound === undefined ? {} : { signal: AbortSignal.timeout(bound) }),
      }).catch((error: Error) => {
        throw new RefusalError('unreachable', error.name === 'TimeoutError'
          ? `${new URL(at).host} did not answer within ${Math.round(bound! / 1000)} seconds. Nothing was installed.`
          : `could not reach ${new URL(at).host} — ${error.message}. Nothing was installed.`);
      });

      const location = response.headers.get('location');
      if (hop && response.status >= 300 && response.status < 400 && location) {
        if (hops >= redirects) {
          throw new RefusalError('unreachable', `${url} redirected more than ${redirects} times — nothing was fetched from it.`);
        }
        at = new URL(location, at).href;
        continue;
      }

      if (response.status === 404) return null;
      if (!response.ok) throw new RefusalError('unreachable', `${at} answered ${response.status} — nothing was installed.`);
      return response;
    }
  }
}
