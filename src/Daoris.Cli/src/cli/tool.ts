import type { CliCommand } from '../platform/types.ts';
import { runDriverProgram } from '../platform/driverprogram.ts';

export const command: CliCommand = {
  name: 'tool',
  kind: 'management',
  usage: [
    '  tool [verb]          the programs Daoris runs beside its agents — Git, Node.js,',
    "                       PowerShell, GitHub CLI, Azure CLI — each the system's, managed,",
    '                       or a file you name ($DAORIS_HOME/tools.json); no file is the',
    "                       system's, as before:",
    '                         list                      how each is run, the file, and the',
    '                                                   versions downloaded',
    '                         path <tool>               the file Daoris runs for it, or why none',
    '                         use <tool> system         the one on PATH, as before',
    '                         use <tool> managed [<version>]',
    '                                                   a version kept in the home: downloaded',
    '                                                   if absent, and verified; no version is',
    '                                                   the newest the lists named at the last look',
    '                         use <tool> file <path>    an executable you name',
    '                         download <tool> [<version>]',
    '                                                   fetch and verify it; nothing switches',
    '                         update <tool>             managed: the newest the lists named',
    '                         delete <tool> <version>   a downloaded version nothing uses',
    '                         locations [add|remove <address>]',
    '                                                   the lists versions come from: yours in',
    '                                                   order, then the one built in',
    '                         look                      fetch every location; say what is newer',
  ],
  // PUSH1f2c (D194 point 1): the whole family is Daoris's driver program's, `daoris-driver tool` with the same words, the one row
  // that builds it; this writes nothing before it starts. The program reaches a tool's maker and a resource location itself, its
  // download staged under its own process and published under the home's lease (the alone-path design §1.4).
  run: (args) => (args.driverProgram ?? ((family, words) => runDriverProgram(family, words)))('tool', args.argv),
};
