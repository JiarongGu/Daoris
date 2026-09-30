import type { CliCommand } from '../types.ts';
import { commandRemote } from '../remotes.ts';

export const command: CliCommand = {
  name: 'remote',
  kind: 'management',
  usage: [
    "  remote [verb]        this machine's remotes, one per workspace:",
    '                         list                      what is wired (keys redacted)',
    "                         add <workspace> --url U   wire a workspace's deployment",
    '                         remove <workspace>        unwire it here; the',
    '                                                   deployment is untouched',
  ],
  options: [
    '  --url <url>          the deployment a workspace syncs with (remote add)',
    '  --key <key>          its key; or DAORIS_REMOTE_KEY, or typed in (remote add).',
    '                       Never printed back — only its audit prefix',
  ],
  run: commandRemote,
};
