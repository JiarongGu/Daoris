import type { CliCommand, CommandArgs } from '../platform/types.ts';
import { DaorisError, type ExitCode } from '../platform/errors.ts';
import { commandFamilyCheck } from '../family/check.ts';
import { commandFamilyMaterialize } from '../family/materialize.ts';

const VERBS: Record<string, (args: CommandArgs) => ExitCode> = { check: commandFamilyCheck, materialize: commandFamilyMaterialize };

export const command: CliCommand = {
  name: 'family',
  kind: 'doctrine',
  usage: [
    '  family check         the family beside this repository: each member\'s uses',
    '                       against the layers, and each pin one full commit on its',
    '                       producer\'s line (offline; reads ../daoris.family.json)',
    '  family materialize   each pin of this repository written as its producer\'s',
    '                       tree at the pinned commit, into pinned/<producer> (offline)',
  ],
  options: [
    '  --family <file>      the family file to read (family only)',
    '  --into <folder>      where materialize writes, inside this repository (family only)',
  ],
  run: (args) => {
    const verb = VERBS[args.argv[0] ?? ''];
    if (!verb) throw new DaorisError('usage: daoris family check|materialize [--family <file>] [--into <folder>]');
    return verb(args);
  },
};
