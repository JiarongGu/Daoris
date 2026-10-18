// A brainstorm from any terminal (BRAINSTORM1a3; D176 points 1, 2 and 8): the twin of the driver host's
// `daoris-driver brainstorm`, for a person, or a session in any repository Daoris drives, that holds this CLI.
//
// It writes nothing and spawns nothing. It reaches the running Daoris through `service.ts`, by the local host's
// machine-only door, `/api/brainstorms`: the host writes the request the driver's look takes, and answers show, send,
// clear and list from the home's records, as the driver's verb reads them. A brainstorm is kept on the machine that asked
// it, never on a shared host and never as a quest operation, so a service elsewhere is never asked.
//
// A MANAGEMENT command: opt-in, never run by a gate, and nothing a doctrine command reaches imports it.

import { DaorisError } from '../platform/errors.ts';
import { request } from './service.ts';
import type { CommandArgs } from '../platform/types.ts';
import type { ExitCode } from '../platform/errors.ts';

export const BRAINSTORM_USAGE =
  'usage: daoris brainstorm "<question>" [--repository <name>] [--agents <a,b>]\n'
  + '       daoris brainstorm show|send|clear <id>\n'
  + '       daoris brainstorm list';

/** Why only this machine's service is asked: the sentence `request` refuses a service elsewhere with. */
const HERE = 'a brainstorm is kept on the machine that asked it (D176 point 8)';

const DOOR = '/api/brainstorms';

/** One brainstorm as the door answers it: where it stands, its packet's digest and the digest the person sent. */
interface Standing {
  id: string;
  question: string;
  state: string;
  code: string | null;
  packet: string | null;
  sent: string | null;
}

/** The packet as frozen (`packet.json`, D176 point 2), read only for what the person is shown. */
interface Packet {
  digest?: string;
  question?: string;
  repository?: string | null;
  tier?: string | null;
  sources?: {
    id?: string; kind?: string; path?: string; lines?: number[] | null; revision?: string | null;
    why?: { words?: string; tier?: string }; kept?: number; of?: number; text?: string;
  }[];
  leftOut?: { path?: string; why?: string }[];
}

const usage = (): never => {
  throw new DaorisError(BRAINSTORM_USAGE);
};

export async function commandBrainstorm({ argv, write }: CommandArgs): Promise<ExitCode> {
  const dry = argv.includes('--dry-run');
  const [first, ...rest] = argv.filter((each) => each !== '--dry-run');
  if (first === undefined || first.startsWith('--')) return usage();

  if (first === 'list') {
    if (rest.length > 0) usage();
    const brainstorms = (await ask('GET', DOOR)) as unknown as Standing[];
    if (!Array.isArray(brainstorms) || brainstorms.length === 0) write('No brainstorm on this machine.');
    for (const each of Array.isArray(brainstorms) ? brainstorms : []) {
      const line = String(each.question ?? '').split('\n')[0]!;
      write(`${each.id}  ${each.state}  ${line.length > 80 ? `${line.slice(0, 80)}…` : line}`);
    }
    return 0;
  }

  if (first === 'show' || first === 'send' || first === 'clear') {
    if (rest.length !== 1 || rest[0]!.startsWith('--')) usage();
    const id = rest[0]!;
    const at = `${DOOR}/${encodeURIComponent(id)}`;
    if (first === 'show') return show(id, await ask('GET', at), write);
    if (dry) {
      write(`daoris: would ${first} brainstorm \`${id}\``);
      return 0;
    }

    const answered = await ask(first === 'send' ? 'POST' : 'DELETE', first === 'send' ? `${at}/send` : at);
    write(`daoris: ${String(answered.message ?? `brainstorm \`${id}\`: done.`)}`);
    if (first === 'send') write(`  read the document with \`daoris brainstorm show ${id}\` once its passes end.`);
    return 0;
  }

  // The question, then its flags in pairs, as the driver's verb reads them: anything else is the usage.
  let repository: string | null = null;
  let agents: string[] = [];
  for (let at = 0; at < rest.length; at += 2) {
    const value = rest[at + 1];
    if (value === undefined || value.startsWith('--')) usage();
    if (rest[at] === '--repository') repository = value!.trim() || null;
    else if (rest[at] === '--agents') agents = value!.split(',').map((each) => each.trim()).filter(Boolean);
    else usage();
  }

  if (dry) {
    write(`daoris: would ask a brainstorm: ${first}`
      + (repository ? ` (of \`${repository}\`)` : '') + (agents.length ? `, of ${agents.join(', ')}` : ''));
    return 0;
  }

  const asked = await ask('POST', DOOR, { question: first, repository, agents });
  const id = (asked.brainstorm as Standing | undefined)?.id ?? '';
  write(`daoris: ${String(asked.message ?? `brainstorm \`${id}\` asked.`)}`);
  write(`  read it with \`daoris brainstorm show ${id}\` before you send it with \`daoris brainstorm send ${id}\`.`);
  return 0;
}

/**
 * One call to the door, its answer on a 2xx; otherwise the host's own sentence as a refusal, and a host that has no such door at
 * all (a shared host maps none, D176 point 8) said to keep no brainstorm.
 */
async function ask(method: 'GET' | 'POST' | 'DELETE', path: string, body?: unknown): Promise<Record<string, unknown>> {
  const { status, json, url } = await request(method, path, body, { here: HERE });
  if (status >= 200 && status < 300) return json ?? {};
  if (typeof json?.error === 'string') throw new DaorisError(json.error, 1);
  if (status === 404 || status === 405) {
    throw new DaorisError(
      `the service at ${url} keeps no brainstorm: a brainstorm is kept by the Daoris running on this machine, and a shared host `
      + 'keeps none (D176 point 8).', 1);
  }

  throw new DaorisError(`the service answered ${status}`, 1);
}

/** What `show` says: the packet whole before it goes and while it runs, the document once it ended, or that it waits. */
function show(id: string, answered: Record<string, unknown>, write: (line: string) => void): ExitCode {
  const brainstorm = answered.brainstorm as Standing | undefined;
  const frozen = answered.frozen as Packet | null | undefined;
  if (!brainstorm || !frozen) {
    write(`Brainstorm \`${id}\` waits for Daoris's next look, which freezes its packet.`);
    return 0;
  }

  if (brainstorm.state === 'ended' && typeof answered.document === 'string') {
    write(answered.document);
    return 0;
  }

  write(packetText(id, frozen).replace(/\n$/, ''));
  write(brainstorm.state === 'frozen' && !brainstorm.sent
    ? `Nothing has gone to any agent. Send it with \`daoris brainstorm send ${id}\`, or clear it.`
    : brainstorm.state === 'frozen' ? "Sent: it goes at Daoris's next look."
      : brainstorm.state === 'running' ? 'Sent, and running.'
        : `Ended (${brainstorm.code ?? 'no document'}): ${brainstorm.code === 'no-agent'
          ? 'no agent could answer, and the packet is kept' : 'no document was kept'}.`);
  return 0;
}

/**
 * The packet whole, as the person reads it before it goes: the question, each source with where it came from and what was kept
 * of it, and what was left out. The driver's `BrainstormCommand.Packet` says it at its terminal in the same words; the two share
 * no code (`twins.md`), and a change to one is a change to the other.
 */
export function packetText(id: string, packet: Packet): string {
  let text = `Brainstorm \`${id}\`, packet \`${packet.digest ?? ''}\`\n\n`;
  for (const line of String(packet.question ?? '').split('\n')) text += `> ${line.replace(/\r$/, '')}\n`;
  const sources = packet.sources ?? [];
  text += '\n' + (packet.repository == null
    ? 'No repository: the question alone.\n'
    : `${sources.length} sources of \`${packet.repository}\`, found by ${packet.tier ?? 'no search'}.\n`);
  for (const source of sources) {
    const [from, to] = source.lines ?? [];
    text += `\n${source.id}  ${source.kind}  ${source.path}`
      + (from !== undefined ? `:${from}-${to ?? from}` : '')
      + (source.revision ? ` at ${source.revision.slice(0, 12)}` : ' at a revision not read')
      + `  (${source.kept} of ${source.of} characters; found by "${source.why?.words ?? ''}", ${source.why?.tier ?? ''})\n`;
    for (const line of String(source.text ?? '').split('\n')) text += `    ${line.replace(/\r$/, '')}\n`;
  }

  for (const left of packet.leftOut ?? []) text += `\nLeft out: ${left.path} (${left.why})\n`;
  return `${text}\n`;
}
