import type { CliCommand } from '../types.ts';
import { commandAnalyze } from '../analyze.ts';

export const command: CliCommand = {
  name: 'analyze',
  kind: 'doctrine',
  usage: [
    '  analyze [packs...]   what adopting would do here: collisions, duplicates, budget.',
    '                       Writes nothing; --json for an agent to act on',
  ],
  run: commandAnalyze,
};
