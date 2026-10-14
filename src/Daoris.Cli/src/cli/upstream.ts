import type { CliCommand } from '../platform/types.ts';
import { commandUpstream } from '../doctrine/upstream.ts';

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
