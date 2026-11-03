import type { CliCommand } from '../platform/types.ts';
import { commandStatus } from '../doctrine/commands.ts';

export const command: CliCommand = {
  name: 'status',
  kind: 'doctrine',
  usage: [
    '  status               summary of packs, drift, local files, and any pending',
    '                       canon update; --json for an agent to act on',
  ],
  run: commandStatus,
};
