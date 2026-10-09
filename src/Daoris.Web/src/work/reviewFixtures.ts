import type { Quest, QuestSetUp } from '../api';

// A review's records as the host answers them (REVIEWENV1g): a chain's work quest and its set-up step in each state the gate
// reads, for the stories and the tests. Neutral names only.

/** The chain's work: done, in a repository whose rule asks for a review in `local`. */
export const WORK: Quest = {
  id: 'q1', from: 'ask:a1', to: 'reports', title: 'Add the compare setting to the report', body: 'Add the compare setting.',
  status: 'Done', filed: '2026-10-09T08:00:00Z', updated: '2026-10-09T09:00:00Z', workspace: 'aurora',
};

/** The set-up the step said, as the driver posted it with the commit it read from the step's tree. */
export const SET_UP: QuestSetUp = {
  commit: 'a1b2c3d4e5f60718293a4b5c6d7e8f9012345678',
  look: 'http://localhost:4200/v3/reports/42',
  shows: 'The report with the compare setting turned on, showing last month beside this one.',
  again: 'Open http://localhost:4200/v3/reports/42, then turn on Compare in the toolbar.',
  served: 'dist',
  session: 's9',
  local: true,
  at: '2026-10-09T09:40:00Z',
  machine: 'desk',
  sequence: 7,
};

/** A newer set-up, after the person's not yet: the label corrected. */
export const SET_UP_AGAIN: QuestSetUp = {
  ...SET_UP,
  commit: 'f0e1d2c3b4a5968778695a4b3c2d1e0f98765432',
  shows: 'The same report, with the label renamed as you said.',
  at: '2026-10-09T10:20:00Z',
  sequence: 9,
};

/** The set-up step, shown and waiting for the person's look. */
export const STEP_SHOWN: Quest = {
  id: 'q2', from: 'ask:a1', to: 'reports', title: 'Show #q1 in `local` for review',
  body: 'Set the work of #q1 up in `local` for the person\'s review.',
  status: 'Done', filed: '2026-10-09T09:05:00Z', updated: '2026-10-09T09:41:00Z', workspace: 'aurora', parent: 'q1',
  held: true, hold: 'unreviewed', setUpIn: 'local', setUps: [SET_UP],
};

/** The step while its session works: nothing shown yet. */
export const STEP_SETTING_UP: Quest = { ...STEP_SHOWN, status: 'Taken', held: false, hold: null, setUps: [], updated: '2026-10-09T09:06:00Z' };

/** The person said not yet, with their words. */
export const STEP_NOT_YET: Quest = {
  ...STEP_SHOWN,
  verdicts: [{
    said: 'not-yet', setUp: { machine: 'desk', sequence: 7 }, commit: SET_UP.commit,
    words: 'The label still reads the old name.', at: '2026-10-09T09:50:00Z', machine: 'desk',
  }],
};

/** Shown again after the not yet: two set-ups, the newest waiting. */
export const STEP_SHOWN_AGAIN: Quest = { ...STEP_NOT_YET, setUps: [SET_UP, SET_UP_AGAIN], updated: '2026-10-09T10:21:00Z' };

/** Reviewed, on its newest set-up. */
export const STEP_REVIEWED: Quest = {
  ...STEP_SHOWN_AGAIN,
  held: false, hold: null,
  verdicts: [
    ...STEP_NOT_YET.verdicts!,
    { said: 'reviewed', setUp: { machine: 'desk', sequence: 9 }, commit: SET_UP_AGAIN.commit, at: '2026-10-09T10:30:00Z', machine: 'desk' },
  ],
};

/** A deployed environment's set-up: nothing Daoris serves, so nothing it shows again. */
export const STEP_DEPLOYED: Quest = {
  ...STEP_SHOWN, title: 'Show #q1 in `dev` for review', setUpIn: 'dev',
  setUps: [{ ...SET_UP, look: 'https://dev.example.test/reports/42', served: null, local: false }],
};

/** A teammate's local set-up, kept on the machine that showed it: no address here. */
export const STEP_ELSEWHERE: Quest = {
  ...STEP_SHOWN, setUps: [{ ...SET_UP, look: null, machine: 'laptop', session: null }],
};

/** A set-up step the person's skip let go. */
export const STEP_SKIPPED: Quest = {
  ...STEP_SHOWN, held: false, hold: null,
  verdicts: [{ said: 'skipped', words: 'Only the docs changed.', at: '2026-10-09T09:45:00Z', machine: 'desk' }],
};
