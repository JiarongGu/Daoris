import type { CliCommand } from '../platform/types.ts';
import { commandCheck } from '../drift.ts';

export const command: CliCommand = {
  name: 'check',
  kind: 'doctrine',
  usage: [
    '  check                drift, staleness, index freshness (offline); the core budget is reported,',
    '                       never enforced',
  ],
  run: commandCheck,
};
