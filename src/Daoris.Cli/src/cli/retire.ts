import type { CliCommand } from '../platform/types.ts';
import { commandRetire } from '../manage.ts';

export const command: CliCommand = {
  name: 'retire',
  kind: 'management',
  usage: [
    "  retire [name]        take a repository off this machine's registry. Ends the",
    '                       registration ONLY — no file, history or doctrine is touched',
  ],
  run: commandRetire,
};
