// The release manager's acts as its own tools: `create-release` and `start-stage` make their fact, and `release-status` reads the stage
// to its approval and its end. Each reads its fact first, whoever made it, makes it only where it is not there yet (a release keyed by its
// effect, a stage started once), and answers a receipt as its data: the plugin's own reading of Azure DevOps after the act, which Daoris
// journals on the step the agent holds and which settles it. The target is the act's arguments, as the repository's delivery knowledge
// names it, held to the route of the merged build's branch. No act here approves: the approval is the approver role's act, `approve-release`.
import { Refusal, azureOf } from './azure.mjs';
import { within } from './browser.mjs';
import { ways } from './pull-request-commands.mjs';
import { releaseStep } from './release-steps.mjs';

/** Each act's command, the fact its receipt names and the release step whose reading it shares. */
export const ACTS = {
  'create-release': { fact: 'release-created', step: 'release-create' },
  'start-stage': { fact: 'stage-started', step: 'release-start' },
  'release-status': { fact: 'stage-ended', step: 'release-watch' },
};

const releaseOf = (url) => (/releaseId=(\d+)/.exec(String(url ?? ''))?.[1] ? Number(/releaseId=(\d+)/.exec(url)[1]) : undefined);

/** The receipt of one reading: reached, waiting (read once, not yet so) or failed, with its words, page, credit and the role it waits on. */
function receiptOf(fact, answer, waitsOn) {
  const waiting = answer.state !== 'reached' && answer.code === 'not-yet';
  const state = answer.state === 'reached' ? 'reached' : waiting ? 'waiting' : 'failed';
  const evidence = answer.evidence ?? {};
  return { fact, state, ...(state === 'failed' ? { code: answer.code } : {}), ...(releaseOf(evidence.url) ? { release: releaseOf(evidence.url) } : {}),
    ...(evidence.url ? { url: evidence.url } : {}), ...(evidence.by ? { by: evidence.by } : {}), message: evidence.message ?? '',
    ...(waiting && waitsOn ? { waitsOn } : {}) };
}

async function acted(command, args) {
  const repo = azureOf(args.origin);
  if (!repo) throw new Refusal('no-origin', `\`${args.origin}\` names no Azure DevOps repository`);
  const id = String(args['pull-request'] ?? '').trim();
  const unit = { repo, pullRequest: /^\d+$/.test(id) ? `/pullrequest/${id}` : id };
  const options = { pipeline: args.pipeline, releaseDefinition: args['release-definition'], stage: args.stage, action: 'create-and-start', findSeconds: 0.001, everySeconds: 1 };
  let waitsOn = null;
  const read = (step) => releaseStep(step, { invocation: `act-${command}`, options, subjectRevision: id }, unit, () => {},
    (frame) => { waitsOn = frame.actor ?? waitsOn; }, true);
  const { fact, step } = ACTS[command];
  let answer = await read(step);
  // A stage not ended yet is read for its approval: one pending waits on its approver, whose press alone gives it.
  if (command === 'release-status' && answer.code === 'not-yet') {
    waitsOn = null;
    const approval = await read('release-approve');
    if (approval.code === 'not-yet' && waitsOn === 'approver') answer = approval;
  }
  const receipt = receiptOf(fact, answer, waitsOn);
  return { said: [receipt.message], exit: receipt.state === 'failed' ? 1 : 0, data: { receipt } };
}

/** One round of a release act; a refusal before any reading said as its line and a failed receipt, exit 1. */
export function actCommand(command, args = {}) {
  return within(async () => {
    try {
      return ways(await acted(command, args));
    } catch (error) {
      if (error instanceof Refusal) {
        return ways({ said: [error.message], exit: 1, data: { receipt: { fact: ACTS[command].fact, state: 'failed', code: error.code, message: error.message } } });
      }
      throw error;
    }
  });
}
