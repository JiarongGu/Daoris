import type { CliCommand } from '../types.ts';
import { commandDoctor } from '../twins.ts';

export const command: CliCommand = {
  name: 'doctor',
  kind: 'doctrine',
  usage: [
    '  doctor               report local documents that look like canonical ones',
    '                       under a different name (advisory; never fails)',
  ],
  run: commandDoctor,
};
