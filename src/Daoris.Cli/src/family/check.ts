// `daoris family check`: the family beside this repository judged with no service.
//
// The family file is found beside this repository's checkout, a tree read back to it (`member.ts`; `--family` names another), each
// member's checkout is the folder of its name there, and every pin is read against its producer's checkout from git's own files.
// It writes nothing. Exit 0 green (warnings said), 1 where anything is refused, 2 for a tool error (a file of the wrong shape).

import { existsSync, readFileSync, statSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { DaorisError, type ExitCode } from '../platform/errors.ts';
import type { CommandArgs } from '../platform/types.ts';
import {
  LOCK_FILE, edgeCount, judgeFamily, readFamily, readLock, readablePins, usesOf, type Finding, type MemberState,
} from './family.ts';
import { openRepository, type Repository } from './gitread.ts';
import { familyOf } from './member.ts';
import { judgePin, type PinOk } from './pins.ts';

const USAGE = 'usage: daoris family check [--family <file>]';

const plural = (count: number, one: string, many: string) => `${count} ${count === 1 ? one : many}`;

function readJson(file: string): unknown {
  try {
    return JSON.parse(readFileSync(file, 'utf8'));
  } catch (error) {
    throw new DaorisError(`${file} is not JSON: ${(error as Error).message}`);
  }
}

/** A member as the folder `checkout` declares it, its own checkout or a tree of it: its `uses` and its pins. */
export function memberAt(checkout: string, name: string): MemberState {
  if (!existsSync(checkout) || !statSync(checkout).isDirectory()) return { checkout: false, uses: [], pins: [] };
  const manifest = join(checkout, 'daoris.json');
  const json = existsSync(manifest) ? readJson(manifest) as { domain?: { uses?: unknown } } : {};
  const lock = join(checkout, LOCK_FILE);
  return {
    checkout: true,
    uses: usesOf(json?.domain?.uses, name),
    pins: existsSync(lock) ? readLock(readFileSync(lock, 'utf8'), lock) : [],
  };
}

export function optionOf(argv: readonly string[], name: string, usage: string): string | undefined {
  const at = argv.indexOf(name);
  if (at < 0) return undefined;
  const value = argv[at + 1];
  if (value === undefined || value.startsWith('--')) throw new DaorisError(usage);
  return value;
}

export function commandFamilyCheck({ root, argv, write }: Pick<CommandArgs, 'root' | 'argv' | 'write'>): ExitCode {
  const named = optionOf(argv, '--family', USAGE);
  const { file } = familyOf(root, named);

  if (!existsSync(file)) {
    if (named) throw new DaorisError(`no family file at ${file}`);
    write(`no family file at ${file}: no family, nothing to check`);
    return 0;
  }

  const family = readFamily(readFileSync(file, 'utf8'), file);
  const folder = dirname(file);
  const members = new Map(family.layers.flat().map((name) => [name, memberAt(join(folder, name), name)] as const));

  const repositories = new Map<string, Repository | null>();
  const repositoryOf = (producer: string) => {
    if (!repositories.has(producer)) repositories.set(producer, openRepository(join(folder, producer)));
    return repositories.get(producer)!;
  };
  const said: (Finding | PinOk)[] = [
    ...readablePins(family, members).map((pin) => judgePin(pin, repositoryOf(pin.producer))),
    ...judgeFamily(family, members),
  ];

  write(`the family at ${file}: ${plural(members.size, 'member', 'members')} in ${plural(family.layers.length, 'layer', 'layers')}, `
    + `${plural(edgeCount(family, members), 'edge', 'edges')}`);
  for (const verdict of ['ok', 'warned', 'refused'] as const) {
    for (const finding of said.filter((f) => f.verdict === verdict)) write(`  ${verdict.padEnd(8)} ${finding.message}`);
  }
  const refused = said.filter((f) => f.verdict === 'refused').length;
  const warned = said.filter((f) => f.verdict === 'warned').length;
  write(refused > 0 ? `the family is refused: ${plural(refused, 'finding', 'findings')}` : `the family is green${warned > 0 ? ` (${warned} warned)` : ''}`);
  return refused > 0 ? 1 : 0;
}
