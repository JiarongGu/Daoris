import type { CliCommand } from '../types.ts';
import { commandWire } from '../manage.ts';

export const command: CliCommand = {
  name: 'wire',
  kind: 'management',
  usage: [
    '  wire <repo> --workspace W',
    '                       move a registered repository to workspace W; only its',
    '                       workspace changes, and no file is touched',
  ],
  run: commandWire,
};
