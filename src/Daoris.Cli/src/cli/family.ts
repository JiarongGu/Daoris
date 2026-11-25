import type { CliCommand } from '../platform/types.ts';
import { commandFamily } from '../family/check.ts';

export const command: CliCommand = {
  name: 'family',
  kind: 'doctrine',
  usage: [
    '  family check         the family beside this repository: each member\'s uses',
    '                       against the layers, and each pin one full commit on its',
    '                       producer\'s line (offline; reads ../daoris.family.json)',
  ],
  options: [
    '  --family <file>      the family file to check (family only)',
  ],
  run: commandFamily,
};
