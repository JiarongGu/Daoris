import type { CliCommand } from '../platform/types.ts';
import { commandWire } from '../service/manage.ts';

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
