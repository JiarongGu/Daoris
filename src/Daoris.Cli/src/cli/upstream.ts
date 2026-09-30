import type { CliCommand } from '../types.ts';
import { commandUpstream } from '../upstream.ts';

export const command: CliCommand = {
  name: 'upstream',
  kind: 'doctrine',
  usage: [
    '  upstream <file>      promote a locally-edited canonical file back to the canon',
  ],
  options: [
    '  --all                promote every drifted file (upstream only)',
  ],
  run: commandUpstream,
};
