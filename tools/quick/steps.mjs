/** The steps of `npm run quick`: the index and the contracts written (`WRITERS`, first and alone), then each check of the merge's `cli`
 * gate (`tools/verify-checks/steps.mjs`, the one list, the layers' census's source counts among them) but its time judgements. Quick
 * judges no test time: the merge's gates and the full set judge it, on classes measured again alone, so load never reddens quick. */
import { CHECK_STEPS } from '../verify-checks/steps.mjs';

export const WRITERS = CHECK_STEPS.filter((step) => step.writes).map((step) => step.run);

/** The gate's steps that judge test time, which quick leaves out. */
export const TIME_JUDGED = ['node tools/time-ratchet.mjs'];
export const TIME_LINE = 'quick: judges no test time; the merge\'s gates and the full set judge it on classes measured again alone (node tools/time-ratchet.mjs, node tools/layers-census.mjs)';

/** The commands of the `cli` gate's steps, in order. */
export function checkSteps() {
  return CHECK_STEPS.map((step) => step.run);
}

/** Every step quick runs, in order, each once. */
export function quickSteps() {
  return [...new Set([...WRITERS, ...checkSteps().filter((step) => !TIME_JUDGED.includes(step))])];
}
