import type { CliCommand } from '../types.ts';
import { commandTool } from '../tools.ts';

export const command: CliCommand = {
  name: 'tool',
  kind: 'management',
  usage: [
    '  tool [verb]          the programs Daoris runs beside its agents — Git, Node.js,',
    "                       PowerShell, GitHub CLI, Azure CLI — each the system's, managed,",
    '                       or a file you name ($DAORIS_HOME/tools.json); no file is the',
    "                       system's, as before:",
    '                         list                      how each is run, and the file it is',
    '                         path <tool>               the file Daoris runs for it, or why none',
    '                         use <tool> system         the one on PATH, as before',
    '                         use <tool> file <path>    an executable you name',
  ],
  // TOOLS2: the file and which program; the download that makes a version managed is TOOLS4's.
  run: (args) => commandTool(args),
};
