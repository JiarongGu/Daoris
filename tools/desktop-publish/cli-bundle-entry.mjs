// The entry of the single file an install carries as its doctrine tool (`app/cli/daoris.mjs`): `bin/daoris.mjs`'s two acts, the floor
// asked and the dispatcher run, with the dispatcher bundled in rather than imported at run time. Only the bundler reads this file.
import { nodeFloorRefusal } from '../../src/Daoris.Cli/bin/node-floor.mjs';
import { runCli } from '../../src/Daoris.Cli/src/cli.ts';

const refusal = nodeFloorRefusal(process.versions.node);
if (refusal !== null) {
  console.error(refusal);
  process.exitCode = 2;
} else {
  process.exitCode = await runCli(process.argv.slice(2), process.cwd());
}
