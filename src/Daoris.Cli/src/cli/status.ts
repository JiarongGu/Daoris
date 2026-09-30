import type { CliCommand } from '../types.ts';
import { commandStatus } from '../commands.ts';

export const command: CliCommand = {
  name: 'status',
  kind: 'doctrine',
  usage: [
    '  status               summary of packs, drift, local files, and any pending',
    "                       canon update; --machine adds this machine's wiring;",
    '                       --json for an agent to act on',
  ],
  options: [
    "  --machine            report this machine's wiring too (status only)",
  ],
  run: commandStatus,
};
