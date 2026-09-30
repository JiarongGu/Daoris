import type { CliCommand } from '../types.ts';
import { commandInit } from '../commands.ts';

export const command: CliCommand = {
  name: 'init',
  kind: 'doctrine',
  usage: [
    '  init                 detect what this repo has, write daoris.json',
  ],
  run: commandInit,
};
