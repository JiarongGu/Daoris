import type { AttentionActId, IntegrationWait } from './attention';
import type { Attention, AttentionActs } from './AttentionRow';

/** Which handler each act needs. */
export const HANDLER: Record<AttentionActId, keyof AttentionActs> = {
  publish: 'publish',
  choose: 'publish',
  retry: 'retry',
  approve: 'answer',
  refuse: 'answer',
  trust: 'trust',
  'accept-departure': 'acceptDeparture',
  'accept-rule': 'acceptRule',
  'decline-rule': 'declineRule',
  'sign-in': 'signIn',
  'sign-in-new': 'signInNew',
  read: 'read',
  reviewed: 'reviewed',
  'not-yet': 'notYet',
  'show-again': 'showAgain',
  'opinion-again': 'opinionAgain',
  'opinion-anyway': 'opinionAnyway',
  'let-ask': 'helper',
  'not-now': 'helper',
  'hand-over': 'handOver',
  'keep-agent': 'handOver',
  'allow-once': 'permission',
  'refuse-once': 'permission',
  'refuse-always': 'permission',
  'go-on-answer': 'answerAlone',
  'keep-answer': 'answerAlone',
  happened: 'held',
  'did-not': 'held',
  'stop-step': 'held',
  'record-close': 'held',
  'resend-close': 'held',
  'stop-close': 'held',
  'open-records': 'openRecords',
  'again-step': 'held',
  'again-check': 'held',
  'stop-check': 'held',
  'ask-fix-check': 'held',
  'stop-partial': 'held',
  'git-try-again': 'gitTryAgain',
  'git-set-up': 'gitSetUp',
  'git-keep-own': 'gitSetUp',
  'git-copy-key': 'gitSetUp',
  'git-sign-in': 'gitSetUp',
  'git-open': 'gitSetUp',
  'git-compare': 'gitSetUp',
  'git-key-page': 'gitKeyPage',
  'git-choose': 'gitChoose',
  'git-use-own': 'gitChoose',
  'git-read-only': 'gitReadOnly',
  'git-allow': 'gitAnswer',
  'git-refuse': 'gitAnswer',
  'quiet-carry-on': 'quiet',
  'quiet-hand-over': 'quiet',
  'quiet-stop': 'quiet',
  'quiet-leave': 'quiet',
  'pass-over': 'passOver',
  'ask-again': 'passOver',
  'go-on': 'passOver',
  'task-done': 'passOver',
  'task-approve': 'passOver',
  'task-reject': 'passOver',
};

/** The kinds that wait in a circle rather than in a repository. */
export const IN_A_CIRCLE: ReadonlySet<Attention['kind']> = new Set(['proposal', 'intake']);

/** An integration row's kind, said by its wait. */
export const INTEGRATION_LABEL: Readonly<Record<IntegrationWait['wait'], string>> = {
  decide: 'work.attention.integrationDecide',
  superseded: 'work.attention.integrationSuperseded',
  card: 'work.attention.integrationCard',
  declaration: 'work.attention.integrationDeclaration',
};

/** The kinds whose door is an agent's page, named by the agent rather than *Open*: the door says where it goes. */
export const AT_AN_AGENT: ReadonlySet<Attention['kind']> = new Set(['account-wait', 'signed-out']);

/**
 * The kinds whose door is their one control, since their answer needs reading first: a park's question in
 * Sessions, an intake's on its ask, a review in Sessions with its review open. Each is a button naming that.
 */
export const DOOR_ONLY: Partial<Record<Attention['kind'], (item: Attention) => [string, Record<string, unknown>?]>> = {
  parked: () => ['work.attention.door.answer'],
  intake: (item) => ['work.attention.door.intake', { id: item.id }],
  review: () => ['work.attention.door.review'],
  // A refused move back to the person is looked at on its session's page, where its tree and its helper's card are.
  'helper-ended': () => ['work.attention.door.open'],
  // The decisions are made on the session's page, in its Memory section, where each row's words are read first.
  memory: () => ['work.attention.door.open'],
  // A decide, an answer and a request are pressed on the repository's Integration tab, beside the candidate and its steps.
  // A declaration owed acceptance is read and accepted on the repository's set-up page instead; the row never accepts it.
  integration: (item) => [item.integration?.wait === 'declaration' ? 'work.attention.door.integrationDeclaration' : 'work.attention.door.integration'],
  // A terminal's press is answered on its card, which shows every value it carries; the row never answers it.
  confirmation: () => ['work.attention.door.confirmation'],
  // What else goes below the floor is chosen on the *Disk space* card's press, where every unit is listed first.
  space: () => ['work.attention.door.space'],
};
