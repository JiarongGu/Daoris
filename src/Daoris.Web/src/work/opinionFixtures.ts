import type { OpinionDetail, OpinionGate, OpinionPass } from './opinion';

// The second opinion's gate in each state (XAGENT1g; the second-agent design §8.5), as `DriverModule.Opinion` and
// `OpinionDetail` answer them: the stories and the tests draw the same records. Neutral names only.

const TIP = '4f9c2a7e1b3d5c6a8e0f2b4d6a8c0e2f4a6b8c0d';
const BASE = '1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b';

const reviewer = { reviewer: 'codex-acp', product: 'Codex', maker: 'OpenAI', label: 'another-maker' } as const;

/** One gate, its reviewer the rule's first. */
export const gateOf = (state: string, more: Partial<OpinionGate> = {}): OpinionGate => ({
  state, holds: true, required: false, opinion: 'o1', ...reviewer, reviewing: 'r7c1', findings: 2, disputes: 0, since: 0, ...more,
});

/** The first pass that read the work: one `must` the working session rejected, one `should` it fixed. */
export const FIRST: OpinionPass = {
  id: 'o1', occasion: 'landing', pass: 'first', state: 'given', base: BASE, tip: TIP, commits: 3, minutes: 20, ...reviewer,
  reviewing: 'r7c1', handedTo: 's42', read: 'src/catalog/ and its tests, the three commits since the line', limits: 'did not run the build',
  findings: [
    {
      number: 1, weight: 'must', where: 'src/catalog/page.ts:42', claim: 'The last row of each page is dropped: the loop stops one short.',
      consequence: 'A list of exactly one page loses its last item.', reproduce: 'Load a list of 20 with a page of 20: 19 show.',
      sure: 'likely', proposal: 'Use `<=` in the loop’s bound, or slice to `page * size`.',
      beside: { answer: { said: 'rejected', evidence: 'The bound is exclusive on purpose: index 20 is past the end, and the test lists 20.' }, counts: { counts: 'rejected' }, rechecked: 'stands', disputed: true },
    },
    {
      number: 2, weight: 'should', where: 'general', claim: 'No test names the empty list.', sure: 'sure',
      beside: { answer: { said: 'fixed', commit: '9e8d7c6' }, counts: { counts: 'fixed', fix: '9e8d7c6b5a4f3e2d1c0b' }, disputed: false },
    },
  ],
};

/** Each pass's detail as the gate reads it. */
export const DETAIL: OpinionDetail = { tier: 'agent', covers: true, passes: 1, first: FIRST, recheck: null };

/** The gates, by state. */
export const GATES = {
  waitsChain: gateOf('waits-chain', { opinion: null, reviewing: null, findings: null, later: 'q7' }),
  notAsked: gateOf('not-asked', { opinion: null, reviewing: null, findings: null }),
  reading: gateOf('reading', { findings: null }),
  withSession: gateOf('with-session'),
  readAgain: gateOf('read-again'),
  disputed: gateOf('disputed', { disputes: 1, answers: `o1:${TIP}:disputed:1:0` }),
  unavailableRequired: gateOf('unavailable', { required: true, opinion: null, reviewing: null, findings: null, code: 'cooling', until: '2026-10-09T15:00:00Z' }),
  unavailable: gateOf('unavailable', { holds: false, opinion: null, reviewing: null, findings: null, code: 'no-reviewer' }),
  commitsSince: gateOf('commits-since', { since: 2, answers: `o1:${TIP}:commits-since:0:2` }),
  settled: gateOf('settled', { holds: false }),
  settledNothing: gateOf('settled', { holds: false, findings: 0 }),
  anyway: gateOf('anyway', { holds: false, disputes: 1, person: 'anyway' }),
  unread: gateOf('unread', { opinion: null, findings: null, says: 'Whether this work waits for a second opinion could not be read: the service did not answer.' }),
} satisfies Record<string, OpinionGate>;

/** A pass that raised nothing: what it read stands in its findings' place. */
export const NOTHING: OpinionDetail = { tier: 'agent', covers: true, passes: 1, first: { ...FIRST, findings: [] } };
