/** A Process half's plan by reach, as a CI run's status and its lines say it. */
import { when } from '../merge/clock.ts';
import { classCount } from '../merge/format.ts';

/**
 * A Process half's plan in its CI run's status: how many classes its census read, the whole half and why or null, the
 * oldest verdict it keeps, each class it runs with why, and each it keeps with the verdict's time and commit and how it stands.
 */
export function reachStatus(plan) {
  return {
    census: plan.census ?? null,
    whole: plan.whole,
    composed: plan.composed ?? null,
    sweep: plan.sweep,
    run: plan.run,
    kept: plan.kept.map((each) => ({ name: each.name, at: each.verdict.at ?? null, commit: each.verdict.commit ?? null, standing: each.standing })),
  };
}

/** A Process half's plan as its CI run says it, a line and the classes it runs or keeps. */
export function reachLines(name, plan) {
  const names = (list) => list.map((each) => each.name.split('.').at(-1));
  if (plan.whole) return [`  ${name}: the whole half runs${plan.census ? ` (${classCount(plan.census)})` : ''}: ${plan.whole}`];
  const shownNames = (list) => `${names(list).slice(0, 8).join(', ')}${list.length > 8 ? `, and ${list.length - 8} more` : ''}`;
  if (plan.composed) {
    return [`  ${name}: passes without running (CISCOPE1b): all ${classCount(plan.kept.length)} kept, each with a pass that stands on this tree, the oldest from ${when(plan.sweep)}; ${plan.composed}`];
  }
  return [
    `  ${name}: ${plan.run.length} of ${classCount(plan.census)} run by reach (${shownNames(plan.run)}); ${plan.kept.length} kept (${shownNames(plan.kept)}), each with a pass that stands on this tree, the oldest from ${when(plan.sweep)}`,
  ];
}
