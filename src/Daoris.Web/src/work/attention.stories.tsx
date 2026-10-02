import type { Meta, StoryObj } from '@storybook/react-vite';
import { AnsweredPark } from './AnsweredPark';
import { type Attention, AttentionRow } from './AttentionRow';
import { AwaitingIntake } from './AwaitingIntake';
import { RunningIntake } from './RunningIntake';
import { AwaitingPerson } from './AwaitingPerson';
import { TrustAsk } from './TrustAsk';

// The two surfaces of attention (design §4): the row Overview's band is made of, and the parked
// session's own answer. What is NOT here is a "resume" button — the ledger allows that move and a
// person makes it by answering, which the surface says rather than offering twice.

const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const PARKED: Attention = {
  id: 's1a2b3c4',
  kind: 'parked',
  title: 'Expose a streaming budget on the chunk API',
  where: 'engine',
  since: at(38),
  detail: 'Two ways forward; I recommend capping on the chunk API, which is what the quest asks for.',
};

/**
 * A quest parked on its failed sessions here (SESSUX1i, D126 §4.6): the quest, waiting since its last session ended, and
 * the driver's sitting sentence; its door is the quest's page, where Try again is.
 */
const PARKED_QUEST: Attention = {
  id: '7a82cc',
  kind: 'parked-quest',
  title: 'Read the media field names from config',
  where: 'engine',
  since: at(64),
  detail: '3 session(s) have failed on `#7a82cc` without landing anything — parked, because trying again spends an account rather than making progress. `daoris driver retry 7a82cc` starts it again once you know why.',
};

/** An ask waiting on a person (INT4d): its place is its circle, named as one. */
const PROPOSAL: Attention = {
  id: '7c1e9a04b2d5',
  kind: 'proposal',
  title: 'The chunk streamer stalls on a cold cache — cap its hydration per frame.',
  where: 'aurora',
  since: at(190),
  detail: 'The declarations propose engine, game. Nothing is published until you choose.',
};

/** A start the driver holds for the agent's trust (D73) — neutral paths, as every fixture here. */
const HOLD = {
  folder: 'C:/somewhere/family/engine',
  trustFile: 'C:/somewhere/data/harnesses/claude-code/work/.claude.json',
  quest: '7a82cc',
};

const TRUST_ROW: Attention = {
  id: HOLD.folder,
  kind: 'trust',
  title: HOLD.folder,
  where: 'engine',
  since: at(95),
  detail: 'The agent ignores this folder\'s own permissions.allow until you trust it there, so the driver is holding what would run in it.',
  trust: { folder: HOLD.folder, trustFile: HOLD.trustFile },
};

const meta: Meta = { title: 'Work/Attention' };
export default meta;

/**
 * The band's rows, in the band's order: parked, a quest parked on its failed sessions, the two kinds of ask, a quest
 * nobody can take, a long title, one that said nothing — and last, a parked row with no door, as a browser shows it.
 */
export const Rows: StoryObj = {
  render: () => (
    <ul className="m-0 max-w-2xl list-none border border-line bg-raised p-0">
      <AttentionRow item={PARKED} onOpen={() => {}} />
      <AttentionRow item={PARKED_QUEST} onOpen={() => {}} />
      {/* A folder waiting on the person's trust (D73): the folder, what it holds, and why. */}
      <AttentionRow item={TRUST_ROW} onOpen={() => {}} />
      <AttentionRow
        item={{
          ...PROPOSAL,
          id: '3e4f5a6b7c8d',
          kind: 'intake',
          title: 'Tidy the release notes.',
          since: at(12),
          detail: 'published nothing: the declarations did not settle ask `#3e4f5a6b7c8d`, so it asks you rather than guess — its question ends its transcript.',
        }}
        onOpen={() => {}}
      />
      <AttentionRow item={PROPOSAL} onOpen={() => {}} />
      <AttentionRow
        item={{
          id: '7a82cc',
          kind: 'unanswerable',
          title: 'Expose a streaming budget on the chunk API',
          where: 'retired',
          since: at(19_000),
          detail: '`retired` is not on this deployment\'s register, so no agent will ever pull this quest.',
        }}
        onOpen={() => {}}
      />
      <AttentionRow item={{ ...PARKED, id: 'quiet', detail: null, since: at(4) }} onOpen={() => {}} />
      <AttentionRow
        item={{
          ...PARKED,
          id: 'cjk',
          title: '让世界流式加载在每一帧内限制水合工作量，并把预算暴露在区块 API 上，供上层调度器读取',
          where: '世界流式加载引擎',
          detail: '有两条路可走；我建议在区块 API 上限流，这正是委托所要求的。',
        }}
        onOpen={() => {}}
      />
      {/* No door: a parked session in a browser, which has no Sessions to open it in. */}
      <AttentionRow item={{ ...PARKED, id: 'browser' }} />
    </ul>
  ),
};

/**
 * Quests parked on their failed sessions (SESSUX1i): one as English says it, and one with a long 中文 title and the
 * sitting sentence as 中文 says it from the number the tick carries; the title truncates to its line.
 */
export const ParkedQuestRows: StoryObj = {
  render: () => (
    <ul className="m-0 max-w-2xl list-none border border-line bg-raised p-0">
      <AttentionRow item={PARKED_QUEST} onOpen={() => {}} />
      <AttentionRow
        item={{
          ...PARKED_QUEST,
          id: 'cjk-quest',
          title: '让世界流式加载在每一帧内限制水合工作量，并把预算暴露在区块 API 上，供上层调度器读取',
          detail: '`#cjk-quest` 上已有 3 个会话失败，且没有落地任何工作——已挂起，因为继续尝试只会消耗账户，而不会有进展。弄清原因后，`daoris driver retry cjk-quest` 会让它重新开始。',
        }}
        onOpen={() => {}}
      />
    </ul>
  ),
};

const ANALYSIS = 'Two ways forward.\n\n1. Cap hydration in the scheduler — smaller change, but the budget then lives away from the API that spends it.\n2. Cap it on the chunk API itself — touches more call sites, and the budget ends up where the work is.\n\nI recommend the second: the quest asks for the budget on the chunk API, and option 1 would leave that promise half kept.';

/** The parked session's own surface: the analysis, then exactly three moves. */
export const Parked: StoryObj = {
  render: () => (
    <div className="max-w-3xl">
      <AwaitingPerson note={ANALYSIS} onResolve={() => {}} />
    </div>
  ),
};

/** A park that said nothing. Rarer, and still only a person can clear it. */
export const ParkedWithNothingSaid: StoryObj = {
  render: () => (
    <div className="max-w-3xl">
      <AwaitingPerson note={null} onResolve={() => {}} />
    </div>
  ),
};

/** A move in flight: every one of them is held, so nobody resolves the same session twice. */
export const Resolving: StoryObj = {
  render: () => (
    <div className="max-w-3xl">
      <AwaitingPerson note={ANALYSIS} pending onResolve={() => {}} />
    </div>
  ),
};

/**
 * ANSWER1c (D131): the park answered, for up to one look of the driver's. The record is still parked with the answer
 * set, and the same session goes on with it then: the card shows what the person said and says so, asking nothing.
 * A blank answer is the service's own *carry on.*, and a 中文 one is content, never translated.
 */
export const Answered: StoryObj = {
  render: () => (
    <div className="grid max-w-3xl gap-3">
      <AnsweredPark answer={'The second: cap it on the chunk API.\n\nApply it to dev first, and leave the scheduler alone.'} />
      <AnsweredPark answer="carry on." />
      <AnsweredPark answer="用第二种：在区块 API 上限制，先应用到 dev。" />
    </div>
  ),
};

const QUESTION = 'published nothing: the declarations did not settle ask `#0fda18` (engine and game both accept a UI bug), so it asks you rather than guess — its question ends its transcript.';

/**
 * A parked INTAKE (INT4g): its answer is on the ask, so the door leads there. No finish, no
 * decline, no box to answer in; its stop is the page header's, which says the ask stays a proposal (SESSUX1d).
 */
export const IntakeAsking: StoryObj = {
  render: () => (
    <div className="max-w-3xl">
      <AwaitingIntake ask="0fda18" note={QUESTION} onAnswer={() => {}} />
    </div>
  ),
};

/** Where nothing can act — a mirrored record — it still says where the answer lives. */
export const IntakeAskingReadOnly: StoryObj = {
  render: () => (
    <div className="max-w-3xl">
      <AwaitingIntake ask="0fda18" note={QUESTION} />
    </div>
  ),
};

/**
 * A RUNNING intake (INT4h): one turn, so no message box — the line says why, and where an answer
 * goes if it asks. The door opens the ask to look at; its stop is the page header's (SESSUX1d).
 */
export const IntakeRunning: StoryObj = {
  render: () => (
    <div className="max-w-3xl">
      <RunningIntake ask="0fda18" onOpen={() => {}} />
    </div>
  ),
};

/**
 * The agent's trust question, asked by Daoris for a folder the driver holds (D73): the folder, what
 * trusting means, what it holds, and the one file written — granted only on the press.
 */
export const TrustAsking: StoryObj = {
  render: () => (
    <div className="max-w-xl">
      <TrustAsk hold={HOLD} onGrant={() => {}} onCancel={() => {}} />
    </div>
  ),
};

/** An intake's room held for trust, with the grant being written: the press is held. */
export const TrustGranting: StoryObj = {
  render: () => (
    <div className="max-w-xl">
      <TrustAsk
        hold={{ folder: 'C:/somewhere/data/intake/aurora', trustFile: HOLD.trustFile, ask: '0fda18' }}
        busy
        onGrant={() => {}}
        onCancel={() => {}}
      />
    </div>
  ),
};
