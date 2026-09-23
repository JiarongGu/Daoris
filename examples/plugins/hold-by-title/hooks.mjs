// The smallest plugin that SPEAKS (D64 §4): a process the driver starts with its loop, asks before a
// start costs anything, tells of every ending, and stops with the loop.
//
// The wire is JSON-RPC 2.0, one frame per line, on this process's stdin and stdout. Nothing else is
// on stdout — a line that is not a frame is shown to the person under this plugin's name and then
// ignored — and stderr is the plugin's own console, relayed the same way.
//
// Where this process is, and what it keeps, are TOLD rather than guessed:
//   DAORIS_PLUGIN_ID      this plugin's id, as its manifest declares it
//   DAORIS_PLUGIN_FOLDER  the install folder — replaced wholesale by an update, so never written to
//   DAORIS_PLUGIN_DATA    the data folder beside it, which an update never touches
//   DAORIS_HOME           the Daoris home the driver runs against
import { appendFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { createInterface } from 'node:readline';

const send = (frame) => process.stdout.write(`${JSON.stringify(frame)}\n`);
const say = (text) => console.error(text);

const data = process.env.DAORIS_PLUGIN_DATA;
mkdirSync(data, { recursive: true });
say(`up as ${process.env.DAORIS_PLUGIN_ID}`);

for await (const line of createInterface({ input: process.stdin })) {
  if (!line.trim()) continue;
  const frame = JSON.parse(line);

  switch (frame.method) {
    // The handshake: the driver says which wire it speaks and which points the manifest declared;
    // the plugin answers with its wire and the points it ACTUALLY listens on — a subset, never more.
    case 'initialize':
      send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: frame.params.points } });
      break;

    // A decision. The driver planned a start; the first plugin to say `hold` ends the waterfall, and
    // its reason becomes the quest's own reason for sitting — so it must name what a person would do.
    case 'hook/quest/consider': {
      const held = frame.params.quest.title.includes('[hold]');
      send({
        jsonrpc: '2.0',
        id: frame.id,
        result: held
          ? { kind: 'hold', reason: 'its title asks to be held — take `[hold]` out of the title to let it run' }
          : { kind: 'allow' },
      });
      break;
    }

    // An observation. Nothing said here changes anything; the record already moved on the exit code
    // and the quest's own state. This plugin keeps a line per ending in ITS data folder.
    case 'hook/session/ended': {
      const { session, quest, repository, state, adapter } = frame.params;
      appendFileSync(join(data, 'ended.log'), `${session} ${quest ?? '-'} ${repository} ${state} ${adapter ?? '-'}\n`);
      send({ jsonrpc: '2.0', id: frame.id, result: {} });
      break;
    }

    // The driver is stopping, or this plugin was switched off. Leave; the driver ends a process that
    // does not.
    case 'shutdown':
      process.exit(0);
      break;

    default:
      // A request this plugin does not know is answered in the protocol's own vocabulary rather than
      // left hanging — an unanswered request is a driver waiting on its timeout.
      if (frame.id !== undefined) {
        send({ jsonrpc: '2.0', id: frame.id, error: { code: -32601, message: `no ${frame.method}` } });
      }
  }
}
