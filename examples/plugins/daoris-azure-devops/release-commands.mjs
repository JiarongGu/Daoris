// The release's read-only command, which a person or a planner calls to propose release targets: `discover-releases` lists, for a
// repository named by its origin's address, what each pipeline releases through (discover.mjs). It writes nothing and chooses nothing.
import { within } from './browser.mjs';
import { Refusal, azureOf, call } from './azure.mjs';
import { discover, discoveryWords } from './discover.mjs';
import { ways } from './pull-request-commands.mjs';

export const DISCOVER = 'discover-releases';

const TELL = 'These are candidates only: declare the one each deployable releases on, by id, in its release node\'s options '
  + '(deployable, pipeline, releaseDefinition, stage, action, deploysOn); none is chosen here.';

async function discovered(args) {
  const repo = azureOf(args.origin);
  if (!repo) throw new Refusal('no-origin', `\`${args.origin}\` names no Azure DevOps repository`);
  const repositoryId = (await call(repo, 'GET', '')).json?.id ?? repo.repository;
  const pipeline = /^\d+$/.test(String(args.pipeline ?? '').trim()) ? Number(args.pipeline) : null;
  const found = await discover({ repo, repositoryId, pipeline });
  return { said: [discoveryWords(found), TELL], exit: 0, data: found };
}

/** One round of `discover-releases`; a refusal said as its line, exit 1. */
export function discoverCommand(args = {}) {
  return within(async () => {
    try {
      return ways(await discovered(args));
    } catch (error) {
      if (error instanceof Refusal) return ways({ said: [error.message], exit: 1 });
      throw error;
    }
  });
}
