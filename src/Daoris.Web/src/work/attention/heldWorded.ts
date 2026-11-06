import type { TFunction } from 'i18next';
import i18n from '../../locales/i18n';
import { checkWhy } from '../../workflow/runSaid';
import { CLOSE_SENDER, type HeldAttempt, type HeldVerification, heldFailed, heldWhy } from '../head/milestone';
import type { Attention } from './AttentionRow';
import type { PushedAttention } from './attentionPushed';

/**
 * A plugin step's attempt held for the person (FABRIC-PERFORM1g, the fabric events design §D6): the step and its attempt, its unit,
 * and why nothing else can say what became of it; a word recorded stays a row, saying so, until the driver's next look applies it. A held
 * check on a verification run is said as that verification (`verificationWorded`).
 */
export function heldWorded(row: PushedAttention, attempt: HeldAttempt): Attention {
  return {
    id: row.id,
    kind: 'work',
    since: row.since,
    where: row.where ?? attempt.unit,
    ...(attempt.verification && !attempt.resolution
      ? verificationWorded(i18n.t, attempt, attempt.verification)
      : {
          title: i18n.t('work.milestone.held.title', { node: attempt.node, attempt: attempt.attempt }),
          detail: attempt.resolution
            ? i18n.t('work.milestone.held.worded', { node: attempt.node, attempt: attempt.attempt, word: i18n.t(`work.milestone.held.word.${attempt.resolution}`) })
            // FABRIC-CLOSE1q: a held close's outcome is its door's answer, so its row says what the door answered and that no second close is made.
            : attempt.plugin === CLOSE_SENDER
              ? i18n.t('work.milestone.held.close.detail', { why: heldWhy(i18n.t, attempt) })
              // FABRIC-PERFORM1k: a failure kept for the person waits for their new attempt, not their word on what became of it.
              : i18n.t(heldFailed(attempt) ? 'work.milestone.held.failedDetail' : 'work.milestone.held.detail', { why: heldWhy(i18n.t, attempt), step: attempt.step }),
        }),
    held: attempt,
  };
}

/**
 * A failed or unknown verification (the pipeline design §4.3, §4.5): its repository and verdict, then what it judged (its line's batch since
 * the base, the line's first verification, or the branch or commit a person asked of) and what waits on the person. An unknown holds its line
 * until they run it again or say not now, which the next verification's batch takes in; a failure runs again only on their word.
 */
export function verificationWorded(t: TFunction, attempt: HeldAttempt, verify: HeldVerification): Pick<Attention, 'title' | 'detail'> {
  const failed = heldFailed(attempt);
  const commit = verify.commit.slice(0, 7);
  const judged = verify.line
    ? verify.base
      ? t('work.verify.batch', { line: verify.line, commit, base: verify.base.slice(0, 7) })
      : t('work.verify.first', { line: verify.line, commit })
    : verify.branch ? t('work.verify.branch', { branch: verify.branch, commit }) : t('work.verify.commit', { commit });
  return {
    title: failed
      ? verify.gate ? t('work.verify.failed', { repository: verify.repository, gate: verify.gate }) : t('work.verify.failedNone', { repository: verify.repository })
      : t('work.verify.unknown', { repository: verify.repository, why: checkWhy(t, verify.why) }),
    detail: t(failed ? 'work.verify.failedDetail' : verify.line ? 'work.verify.holdsLine' : 'work.verify.unknownDetail', { judged }),
  };
}
