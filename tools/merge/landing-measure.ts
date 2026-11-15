/**
 * What a landing records of its own cost: one line per landed branch in `local/metrics/landings.jsonl` (the context its builder read
 * before its first edit, its wall time and tokens, main's red rate), written after the commit that lands it. Both landings (`--commit`
 * and the queue's promotion) call `measureLanding`; a measure that fails is lost, never the landing.
 */
import { recordLanding } from '../dev-metrics/landing.mjs';

export type LandedCommit = { commit: string; branch: string };

/** Each landed branch's record through `record`; never throws. */
export function measureLanding(landed: readonly LandedCommit[], record: (landing: LandedCommit) => void): void {
  for (const each of landed) {
    try {
      record(each);
    } catch {
      continue;
    }
  }
}

/** The landing's own measure, written under `home`; used by both routes. */
export function measureAfterLanding(home: string, landed: readonly LandedCommit[]): void {
  measureLanding(landed, (each) => { recordLanding(home, { ...each, at: new Date().toISOString() }); });
}

/** The queue candidate's members, each landed at the candidate's tip. */
export const landedOf = (candidate: { tip: string; members: { branch: string }[] }): LandedCommit[] =>
  candidate.members.map((member) => ({ commit: candidate.tip, branch: member.branch }));
