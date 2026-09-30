import type { CliCommand } from '../types.ts';
import { commandBrowser } from '../browser.ts';

export const command: CliCommand = {
  name: 'browser',
  kind: 'management',
  usage: [
    "  browser [verb]       Daoris's browser: its favorites and its settings ($DAORIS_HOME/browser/),",
    '                       the same files the Settings screen keeps; read each time it starts:',
    "                         favorite list             what is kept, in the bar's order",
    '                         favorite add <address> [--title T]',
    '                                                   keep a page, in the Daoris folder on its bar',
    '                         favorite remove <address> stop keeping it',
    "                         extensions [offer|refuse] other software's Chrome extensions: offered",
    '                                                   for your approval, or refused',
    "                         use [daoris|edge]         Daoris's own browser, or your Edge on a",
    "                                                   profile of Daoris's",
    "                         links [system|daoris]     where links on Daoris's page open: the",
    "                                                   system's browser, or Daoris's",
  ],
  run: commandBrowser,
};
