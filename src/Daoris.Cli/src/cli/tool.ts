import type { CliCommand } from '../platform/types.ts';
import { LOOK_BOUND_MS, REDIRECTS, commandTool } from '../tools/toolinstall.ts';
import { isAddress } from '../tools/tools.ts';
import { daorisHome } from '../platform/home.ts';
import { instanceDoor, releaseFetcher } from '../service/service.ts';

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
  // TOOLS4: a download and a look reach a maker's host and a resource location through the one module that may
  // reach a network, handed in here with the address rule every hop is held to, so the tools never hold a socket. So is the door
  // a change goes through (RPC1f): the Tools place's route while Daoris runs on this home, alone under the home's lease when none does.
  run: (args) => commandTool(args, releaseFetcher({ hop: isAddress, redirects: REDIRECTS, bound: LOOK_BOUND_MS }), process.env,
    instanceDoor({ home: daorisHome(), write: args.write })),
};
