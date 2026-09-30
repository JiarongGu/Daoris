import type { CliCommand } from '../types.ts';
import { commandInit } from '../commands.ts';

export const command: CliCommand = {
  name: 'init',
  kind: 'doctrine',
  usage: [
    '  init                 detect what this repo has, write daoris.json',
  ],
  options: [
    '  --harness <name>     the layout init writes: claude-code (the default), or agents —',
    '                       knowledge and skills under .agents/, mirrored for Claude Code (init only)',
  ],
  run: commandInit,
};
