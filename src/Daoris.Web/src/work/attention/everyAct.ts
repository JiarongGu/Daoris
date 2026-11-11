import { type AttentionActs } from './AttentionRow';

/** Every act a row may hand, each made by <paramref name="act"/>: a test's spies, a story's no-ops. One list, so a new act is added once. */
export function everyActOf(act: () => () => void): Required<AttentionActs> {
  return {
    publish: act(), retry: act(), answer: act(), trust: act(), acceptDeparture: act(), acceptRule: act(), declineRule: act(),
    signIn: act(), signInNew: act(), read: act(), reviewed: act(), notYet: act(), showAgain: act(), opinionAgain: act(), opinionAnyway: act(),
    helper: act(), held: act(), permission: act(), answerAlone: act(), openRecords: act(), handOver: act(), gitTryAgain: act(), gitSetUp: act(),
    gitChoose: act(), gitKeyPage: act(), gitReadOnly: act(), gitAnswer: act(), secondCheckSeen: act(), quiet: act(), passOver: act(), memoryEnd: act(),
  };
}
