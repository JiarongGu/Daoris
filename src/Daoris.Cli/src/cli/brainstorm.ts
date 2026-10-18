import type { CliCommand } from '../platform/types.ts';
import { commandBrainstorm } from '../service/brainstorm.ts';

export const command: CliCommand = {
  name: 'brainstorm',
  kind: 'management',
  usage: [
    '  brainstorm [verb]    a question put to this machine\'s agents (D176), through',
    '                       the Daoris running here, and kept on this machine:',
    '                         "<question>" [--repository R] [--agents A,B]',
    '                                                   ask it; Daoris freezes its',
    '                                                   packet at its next look',
    '                         show <id>                 the packet whole, or the',
    '                                                   document once it ended',
    '                         send <id>                 your press: the packet goes',
    '                         clear <id>                its records and packet go',
    '                         list                      each, and where it stands',
  ],
  run: commandBrainstorm,
};
