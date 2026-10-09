import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import type { ReactNode } from 'react';
import { clockOf, list, moment } from '../format';
import { InTheme } from '../plugins/storyIcons';
import { chinese, readerIn } from '../storyLanguage';
import { AnsweredPark } from './AnsweredPark';
import { type Attention, type AttentionActs, AttentionRow } from './AttentionRow';
import { AttentionList, type AttentionDoors, AttentionRegion, NothingNeedsYou } from './AttentionList';
import { AwaitingIntake } from './AwaitingIntake';
import { RunningIntake } from './RunningIntake';
import { AwaitingPerson } from './AwaitingPerson';
import { TrustAsk } from './TrustAsk';

// The surfaces of attention (design §4; UX6c, design §6): Overview's *What needs you* whole, as the page leads with it,
// the rows it is made of with the acts that settle them, and the parked session's own answer. What is NOT here is a
// "resume" button — the ledger allows that move and a person makes it by answering, which the surface says rather than
// offering twice.

const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

/** A reader of 中文, whatever the window's language: what the 中文 rows below are worded by. */
const zh = readerIn('zh');
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

/** Every act handed, as a shell's band hands them; a story reports nothing. */
const ACTS: Required<AttentionActs> = {
  publish: () => {}, retry: () => {}, answer: () => {}, trust: () => {}, acceptDeparture: () => {}, acceptRule: () => {},
  declineRule: () => {}, signIn: () => {}, read: () => {}, letRun: () => {}, reviewed: () => {}, notYet: () => {},
  showAgain: () => {},
};
/** Every door, as a shell's Overview hands them. */
const DOORS: AttentionDoors = {
  parked: () => {}, 'parked-quest': () => {}, 'go-ahead': () => {}, trust: () => {}, proposal: () => {}, intake: () => {},
  departure: () => {}, rule: () => {}, unanswerable: () => {}, review: () => {}, 'account-wait': () => {}, 'signed-out': () => {},
  'set-up': () => {},
};

/** A reset tomorrow early, and a reading at this morning's clock: what the account rows say their times from. */
const RESET = new Date(Date.now() + 18 * 3_600_000).toISOString();
const READ = at(8);
const resetSaid = moment(RESET, 'en');
const readSaid = clockOf(READ, new Date(), 'en');

/** Claude Code, whose accounts the rows below name: the agent's page is each one's door. */
const CLAUDE = { agent: 'claude-code', product: 'Claude Code', harness: 'claude-code', signsIn: true } as const;

/**
 * The owner's morning (D150 §6.3's drawing, UX6d): the intake for two asks held behind `home`'s cool-off, having passed two
 * accounts read signed out. Its why says each as last read, and each signed-out one is a press.
 */
const ACCOUNT_WAIT: Attention = {
  id: 'wait:claude-code/account-2', kind: 'account-wait', title: 'Intake for 2 asks', where: 'work', circle: true, since: at(25),
  detail: `home cools until ${resetSaid}; account-1 and account-3 read signed out at ${readSaid}.`,
  account: {
    ...CLAUDE, outside: null,
    named: [
      { id: 'account-2', label: 'home', state: 'cooling', read: READ, until: RESET },
      { id: 'account-1', label: 'account-1', state: 'out', read: READ, until: null },
      { id: 'account-3', label: 'account-3', state: 'out', read: READ, until: null },
    ],
  },
};

/**
 * A quest whose only account cools, with a ready account outside `work`'s list (D130 §3.3) and one no read answered:
 * *Let … run …*, which asks once, and *Read* for that one account.
 */
const LET_IN: Attention = {
  id: 'quest:claude-code/account-2', kind: 'account-wait', title: 'Read the media field names from config', where: 'engine',
  since: at(70), detail: `home cools until ${resetSaid}; spare unknown, never read.`,
  account: {
    ...CLAUDE,
    named: [
      { id: 'account-2', label: 'home', state: 'cooling', read: READ, until: RESET },
      { id: 'acct-3f9c1a2b', label: 'spare', state: 'unknown', read: null, until: null },
    ],
    outside: { id: 'account-4', label: 'account-4', list: 'work', workspace: 'work' },
  },
};

/**
 * The same quest where the one account outside `work`'s list was never read (UXFIX3): it may be signed out, so the row reads
 * it first, in *Let … run …*'s place, and offers to let it run once it reads ready.
 */
const READ_FIRST: Attention = {
  ...LET_IN, id: 'quest:claude-code/account-2:read-first', detail: `home cools until ${resetSaid}.`,
  account: { ...CLAUDE, named: [LET_IN.account!.named[0]!], outside: null, readFirst: { id: 'acct-5e6f7a8b', label: 'team seat' } },
};

/**
 * A signed-out account a list holds that no waiting start names: its own row, its sign-in, its agent's page. It says when
 * it was read, never how long it waited, since nobody knows when it began to hold work (UXFIX3).
 */
const SIGNED_OUT: Attention = {
  id: 'signed-out:claude-code/account-5', kind: 'signed-out', title: 'you@work.example', where: 'Claude Code', since: null,
  read: READ, detail: 'It runs work in this machine and aurora.',
  account: { ...CLAUDE, outside: null, named: [{ id: 'acct-7d01e3aa', label: 'you@work.example', state: 'out', read: READ, until: null }] },
};

/** Overview's main area: the container the row's layout follows, at the width a story gives it. */
function Main({ width, children }: { width?: number; children: ReactNode }) {
  return <div className="@container/main max-w-full bg-page p-4" style={width ? { width } : undefined}>{children}</div>;
}

/** A park's note by code (LANG1b): the lead-in worded in the window's language, the agent's question as written. */
const PARKED: Attention = {
  id: 's1a2b3c4',
  kind: 'parked',
  title: 'Expose a streaming budget on the chunk API',
  where: 'engine',
  since: at(38),
  note: {
    note: 'It stopped with its quest still taken, to ask you: …',
    parts: [
      { code: 'ended.parked-asked', values: {}, text: 'It stopped with its quest still taken, to ask you:' },
      { words: 'Two ways forward; I recommend capping on the chunk API, which is what the quest asks for.', by: 'agent' },
    ],
  },
};

/** A park from before parts: its note as kept, marked. */
const PARKED_BEFORE: Attention = {
  ...PARKED,
  id: 'b4f0r3',
  note: { note: 'Two ways forward; I recommend capping on the chunk API, which is what the quest asks for.' },
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

/** An ask waiting on a person (INT4d): its place is its circle, named as one; its declarations named two receivers. */
const PROPOSAL: Attention = {
  id: '7c1e9a04b2d5',
  kind: 'proposal',
  title: 'The chunk streamer stalls on a cold cache — cap its hydration per frame.',
  where: 'aurora',
  since: at(190),
  detail: 'The declarations propose engine, game. Nothing is published until you choose.',
  publishTo: ['engine', 'game'],
  choices: ['engine', 'game', 'tools'],
};

/** A go-ahead a session asked on its ask (KNOWUSE1a): the act, the ask, and why in the session's words. */
const GO_AHEAD: Attention = {
  id: '0fda18#1', kind: 'go-ahead', ask: '0fda18', number: 1, title: 'push on prod: “the report’s menu entries”',
  where: 'ask #0fda18', since: at(52), detail: 'The menu ships with tonight’s release, and only prod serves it.',
};

/** A done held for the person's yes (DRIFT1d2): the requirement it departed from, and its reason. */
const DEPARTURE: Attention = {
  id: '5e7a11', kind: 'departure', title: 'Read the media field names from config', where: 'engine', since: at(140),
  detail: 'Requirement 2 departed: the names stay in code until the config loader lands next week',
};

/**
 * A set-up shown and waiting for the person's look (REVIEWENV1g): what it showed, and *Reviewed*, *Not yet…* and *Show it
 * again*, its set-up named as the row drew it.
 */
const SET_UP_ROW: Attention = {
  id: 'q2', kind: 'set-up', title: 'Show #q1 in `local` for review', where: 'reports', since: at(25),
  detail: 'Shown in `local`: The report with the compare setting turned on, showing last month beside this one.',
  setUp: { machine: 'desk', sequence: 7 }, local: true, session: 's9',
};

/** An agent's proposal to widen the rules (PERM2, D74). */
const RULE: Attention = {
  id: 'p0000002', kind: 'rule', title: 'allow WebFetch for every session on this machine', where: 'session i9n8t7k6 (ask #0fda18)',
  since: at(30), detail: 'The docs it needs are on the web.',
};

/** Seven sessions ended with work to review (D126), an hour apart: five shown, two counted. */
const REVIEWS: Attention[] = [
  'Verify the drill-down against DEV', 'Tidy the release notes', 'Cap hydration per frame', 'Read the media field names',
  'Expose the chunk budget', 'Fix the pipeline’s knowledge check', 'Rename the scheduler’s queue',
].map((title, index) => ({
  id: `r3v13w0${index}`, kind: 'review', title, where: index % 2 ? 'game' : 'engine', since: at(60 * (24 - index)),
  detail: index === 3 ? 'uncommitted changes' : index === 0 ? '1 commit to review' : `${index + 1} commits to review`,
}));

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
  quest: HOLD.quest,
};

const meta: Meta = { title: 'Work/Attention' };
export default meta;

/**
 * Overview's lead as the install would have shown it the morning the asks sat (UX6c, design §6): what holds work (a folder
 * held for trust, a quest and the asks' intake waiting on their accounts, a quest parked on its failed sessions, a
 * go-ahead, a parked session, and last a signed-out account a list holds, which has no wait to sort by, UXFIX3), what
 * waits for the person's word (two proposed asks, a departure, a widening), and seven sessions to review, five shown and
 * two counted.
 */
const BAND: Attention[] = [
  TRUST_ROW, LET_IN, PARKED_QUEST, GO_AHEAD, PARKED, ACCOUNT_WAIT, SIGNED_OUT,
  PROPOSAL, { ...PROPOSAL, id: '1b2c3d4e5f6a', title: 'Fix the pipeline’s knowledge check', since: at(170), publishTo: ['engine'], detail: 'The declarations propose engine. Nothing is published until you choose.' },
  DEPARTURE, SET_UP_ROW, RULE,
  ...REVIEWS,
];

/** The band whole, at the install's width: acts on their rows, each row's door at its end. */
export const Band: StoryObj = {
  render: () => <Main width={1180}><AttentionRegion><AttentionList items={BAND} doors={DOORS} acts={ACTS} onSessions={() => {}} /></AttentionRegion></Main>,
};

/** The same in 中文: the groups, the kinds and the acts named in 中文, the person's and the sessions' words as written. */
export const BandChinese: StoryObj = { ...Band, decorators: [chinese] };

/** The same in dark. */
export const BandDark: StoryObj = { ...Band, decorators: [dark] };

/** At 680 px: each row's title under its kind, where and how long, as the design draws it narrow. */
export const BandNarrow: StoryObj = {
  render: () => <Main width={600}><AttentionRegion><AttentionList items={BAND} doors={DOORS} acts={ACTS} onSessions={() => {}} /></AttentionRegion></Main>,
};

/** Narrow, in 中文. */
export const BandNarrowChinese: StoryObj = { ...BandNarrow, decorators: [chinese] };

/**
 * An intake held on an account read signed out with none cooling (UX6d1, UX6d2): a wait for a sign-in, never a reset, so its
 * line names no time and never the tool's own sign-in. Its sign-in, and *Read* first for the account outside `work`'s list
 * no read answered (UXFIX3), whose readiness nobody knows.
 */
const SIGNED_OUT_WAIT: Attention = {
  id: 'wait:claude-code/:signed-out', kind: 'account-wait', title: 'Intake for ask #0fda18', where: 'work', circle: true,
  since: at(42), detail: `account-1 read signed out at ${readSaid}.`,
  account: {
    ...CLAUDE, outside: null, readFirst: { id: 'acct-5e6f7a8b', label: 'team seat' },
    named: [{ id: 'account-1', label: 'account-1', state: 'out', read: READ, until: null }],
  },
};

/** Beside it, a cool-off of the tool's own sign-in for the same agent: no account named either, and a row of its own. */
const OWN_COOLING: Attention = {
  id: 'wait:claude-code/', kind: 'account-wait', title: 'Expose a streaming budget on the chunk API', where: 'engine',
  since: at(33), detail: `Your own sign-in cools until ${resetSaid}.`,
  account: { ...CLAUDE, outside: null, named: [{ id: null, label: 'Your own sign-in', state: 'cooling', read: null, until: RESET }] },
};

/**
 * Overview's *What needs you* where one agent holds two waits (UX6d2): the intake held on a signed-out account with none
 * cooling, and a quest behind the tool's own sign-in's cool-off, each its own row under its own key; then an ask for the
 * person's word.
 */
export const BandSignedOutWait: StoryObj = {
  render: () => (
    <Main width={1180}>
      <AttentionRegion>
        <AttentionList items={[SIGNED_OUT_WAIT, OWN_COOLING, PROPOSAL]} doors={DOORS} acts={ACTS} onSessions={() => {}} />
      </AttentionRegion>
    </Main>
  ),
};

/** The same in dark. */
export const BandSignedOutWaitDark: StoryObj = { ...BandSignedOutWait, decorators: [dark] };

/**
 * A key its provider refused (ACCTUX1): it read *unchecked* and was no row at all. A list holds it, so it is a signed-out row
 * of its own, saying the key was refused and that its repair is a new key on its agent's page, with no sign-in, which a key
 * has none of; and a start waiting on its accounts names it refused beside one read signed out.
 */
const KEY_REFUSED: Attention = {
  id: 'signed-out:claude-code/acct-0a1b2c3d', kind: 'signed-out', title: 'API key …k3y9', where: 'Claude Code', since: null,
  read: READ, detail: "Key refused: add a new API key on Claude Code's page in its place. It runs work in this machine.",
  account: { ...CLAUDE, outside: null, named: [{ id: 'acct-0a1b2c3d', label: 'API key …k3y9', state: 'refused', read: READ, until: null }] },
};

const WAIT_ON_REFUSED: Attention = {
  id: 'wait:claude-code/:signed-out', kind: 'account-wait', title: 'Intake for ask #0fda18', where: 'work', circle: true,
  since: at(42), detail: `account-1 read signed out at ${readSaid}; API key …k3y9: key refused.`,
  account: {
    ...CLAUDE, outside: null,
    named: [
      { id: 'account-1', label: 'account-1', state: 'out', read: READ, until: null },
      { id: 'acct-0a1b2c3d', label: 'API key …k3y9', state: 'refused', read: READ, until: null },
    ],
  },
};

/** Overview's *What needs you* holding a refused key: its own row, and a waiting start naming it. */
export const BandKeyRefused: StoryObj = {
  render: () => (
    <Main width={1180}>
      <AttentionRegion>
        <AttentionList items={[WAIT_ON_REFUSED, KEY_REFUSED]} doors={DOORS} acts={ACTS} onSessions={() => {}} />
      </AttentionRegion>
    </Main>
  ),
};

/** The same in dark. */
export const BandKeyRefusedDark: StoryObj = { ...BandKeyRefused, decorators: [dark] };

/** In a browser: the asks and quests it can know, their acts on the service's own doors, and no driver's rows. */
export const BandInABrowser: StoryObj = {
  render: () => (
    <Main width={1180}>
      <AttentionRegion>
        <AttentionList
          items={[PROPOSAL, DEPARTURE, { id: '7a82cc', kind: 'unanswerable', title: 'Expose a streaming budget on the chunk API', where: 'retired', since: at(19_000), detail: '`retired` is not on this deployment\'s register, so no agent will ever pull this quest.' }]}
          doors={{ proposal: () => {}, departure: () => {}, unanswerable: () => {} }}
          acts={{ publish: () => {}, acceptDeparture: () => {} }}
        />
      </AttentionRegion>
    </Main>
  ),
};

/** Nothing needs the person: one line, and what is working, never a card saying all clear (§6.1). */
export const Nothing: StoryObj = {
  render: () => <Main width={1180}><AttentionRegion><NothingNeedsYou working={2} /></AttentionRegion></Main>,
};

/** Nothing needs the person, and nothing is working. */
export const NothingQuiet: StoryObj = {
  render: () => <Main width={1180}><AttentionRegion><NothingNeedsYou working={0} /></AttentionRegion></Main>,
};

/**
 * Each question a row asks once under itself before it acts (design §6.3, D41 §4): a go-ahead's yes with the person's
 * words, its no, a widening's accept naming what it widens, a receiver chosen, and the agent's own trust question.
 */
export const Asking: StoryObj = {
  render: () => (
    <Main width={1180}>
      <ul className="m-0 grid list-none gap-2 border border-line bg-raised p-0 py-2">
        <AttentionRow item={GO_AHEAD} acts={ACTS} onOpen={() => {}} opened="approve" />
        <AttentionRow item={{ ...GO_AHEAD, id: '0fda18#2', number: 2 }} acts={ACTS} onOpen={() => {}} opened="refuse" />
        <AttentionRow item={RULE} acts={ACTS} onOpen={() => {}} opened="accept-rule" />
        <AttentionRow item={{ ...PROPOSAL, publishTo: [] }} acts={ACTS} onOpen={() => {}} opened="choose" />
        <AttentionRow item={TRUST_ROW} acts={ACTS} onOpen={() => {}} opened="trust" />
        {/* REVIEWENV1g: a set-up's *not yet*, asking for the person's words, which go to its session as its next turn. */}
        <AttentionRow item={SET_UP_ROW} acts={ACTS} onOpen={() => {}} opened="not-yet" />
        {/* UX6d: an account let into the workspace's list, and into this machine's, each saying what it lets Daoris spend. */}
        <AttentionRow item={LET_IN} acts={ACTS} onOpen={() => {}} opened="let-run" />
        <AttentionRow
          item={{ ...LET_IN, id: 'machine', account: { ...LET_IN.account!, outside: { ...LET_IN.account!.outside!, list: null } } }}
          acts={ACTS}
          onOpen={() => {}}
          opened="let-run"
        />
      </ul>
    </Main>
  ),
};

/**
 * The accounts' rows (UX6d, design §6.2–§6.3): the intake waiting behind a cooling account with a sign-in for each account it
 * passed signed out; a quest whose one account cools, letting a ready one in and reading one never read; the same where the
 * one outside its list was never read, read first (UXFIX3); a signed-out account a list holds, saying when it was read; the
 * tool's own sign-in cooling, which only the person signs in; three signed out, two pressed and the third said; the tick's
 * word before the roster answers, nothing to press; a row held while a sign-in runs; a long 中文 name.
 */
export const AccountRows: StoryObj = {
  render: () => (
    <Main width={1180}>
      <ul className="m-0 grid list-none gap-1 border border-line bg-raised p-0 py-2">
        <AttentionRow item={ACCOUNT_WAIT} acts={ACTS} onOpen={() => {}} />
        <AttentionRow item={LET_IN} acts={ACTS} onOpen={() => {}} />
        <AttentionRow item={READ_FIRST} acts={ACTS} onOpen={() => {}} />
        <AttentionRow item={SIGNED_OUT} acts={ACTS} onOpen={() => {}} />
        <AttentionRow
          item={{
            ...ACCOUNT_WAIT, id: 'own', title: 'Expose a streaming budget on the chunk API', where: 'engine', circle: false,
            detail: `Your own sign-in cools until ${resetSaid}.`,
            account: { ...CLAUDE, outside: null, named: [{ id: null, label: 'Your own sign-in', state: 'cooling', read: null, until: RESET }] },
          }}
          acts={ACTS}
          onOpen={() => {}}
        />
        <AttentionRow
          item={{
            ...ACCOUNT_WAIT, id: 'three', title: '2 quests', where: 'game', circle: false,
            detail: `account-1, account-3, and account-6 read signed out at ${readSaid}.`,
            account: {
              ...CLAUDE, outside: null,
              named: ['account-1', 'account-3', 'account-6'].map((id) => ({ id, label: id, state: 'out' as const, read: READ, until: null })),
            },
          }}
          acts={ACTS}
          onOpen={() => {}}
        />
        <AttentionRow
          item={{
            ...ACCOUNT_WAIT, id: 'tick',
            detail: `home cools until ${resetSaid}; account-1 and account-3 signed out.`,
            account: { ...ACCOUNT_WAIT.account!, harness: null, signsIn: false, product: 'claude-code' },
          }}
          acts={ACTS}
          onOpen={() => {}}
        />
        <AttentionRow item={{ ...SIGNED_OUT, id: 'held' }} acts={ACTS} onOpen={() => {}} busy />
        <AttentionRow
          item={{
            ...SIGNED_OUT, id: 'cjk', title: '工作账户（团队席位，周额度）',
            detail: 'It runs work in 世界流式加载引擎.',
            account: { ...CLAUDE, outside: null, named: [{ id: 'acct-1a2b3c4d', label: '工作账户（团队席位，周额度）', state: 'out', read: READ, until: null }] },
          }}
          acts={ACTS}
          onOpen={() => {}}
        />
      </ul>
    </Main>
  ),
};

/**
 * The rows as 中文 derives them (`accountAttention` words each from the catalogue, in the reader's language): the titles and
 * the lines in 中文, the accounts' names and the reset's moment as the reader's language writes them. A signed-out account's
 * reading is the row's own words (*… 读取*), so its line says only where it runs (UXFIX3).
 */
function chineseRows(): Attention[] {
  const t = zh.t.bind(zh);
  const line = (...clauses: string[]) => t('work.attention.accounts.line', { clauses: clauses.join(t('work.attention.accounts.join')) });
  const cooling = t('work.attention.accounts.cooling', { account: 'home', when: moment(RESET, 'zh') });
  const read = clockOf(READ, new Date(), 'zh');
  return [
    {
      ...ACCOUNT_WAIT, title: t('work.attention.held.intakes', { count: 2 }),
      detail: line(cooling, t('work.attention.accounts.out', { accounts: list(['account-1', 'account-3'], 'zh'), when: read })),
    },
    {
      ...LET_IN, title: '从配置读取媒体字段名',
      detail: line(cooling, t('work.attention.accounts.never', { accounts: 'spare' })),
    },
    {
      ...SIGNED_OUT,
      detail: t('work.attention.signedOut.runs', { runs: list([t('agents.runs.machine'), 'aurora'], 'zh') }),
    },
    {
      ...READ_FIRST, title: '从配置读取媒体字段名', detail: line(cooling),
      account: { ...READ_FIRST.account!, readFirst: { id: 'acct-5e6f7a8b', label: '团队席位' } },
    },
    {
      ...ACCOUNT_WAIT, id: 'own', title: '在区块 API 上暴露流式预算', where: 'engine', circle: false,
      detail: line(t('work.attention.accounts.cooling', { account: t('agents.account.own'), when: moment(RESET, 'zh') })),
      account: { ...CLAUDE, outside: null, named: [{ id: null, label: t('agents.account.own'), state: 'cooling', read: null, until: RESET }] },
    },
    {
      ...SIGNED_OUT, id: 'own-out', title: t('agents.account.own'),
      detail: t('work.attention.signedOut.own'),
      account: { ...CLAUDE, outside: null, named: [{ id: null, label: t('agents.account.own'), state: 'out', read: READ, until: null }] },
    },
  ];
}

/**
 * The accounts' rows in 中文, each title and line as 中文 says it: a signed-out account saying when it was read, an account
 * outside the list read first, and the tool's own sign-in read signed out, with no press.
 */
export const AccountRowsChinese: StoryObj = {
  decorators: [chinese],
  render: () => (
    <Main width={1180}>
      <ul className="m-0 grid list-none gap-1 border border-line bg-raised p-0 py-2">
        {chineseRows().map((item) => <AttentionRow key={item.id} item={item} acts={ACTS} onOpen={() => {}} />)}
      </ul>
    </Main>
  ),
};

/** The accounts' rows in dark. */
export const AccountRowsDark: StoryObj = { ...AccountRows, decorators: [dark] };

/** The accounts' rows at 680 px: each act under its row, the door at the end. */
export const AccountRowsNarrow: StoryObj = {
  render: () => (
    <Main width={600}>
      <ul className="m-0 grid list-none gap-1 border border-line bg-raised p-0 py-2">
        <AttentionRow item={ACCOUNT_WAIT} acts={ACTS} onOpen={() => {}} />
        <AttentionRow item={LET_IN} acts={ACTS} onOpen={() => {}} />
        <AttentionRow item={SIGNED_OUT} acts={ACTS} onOpen={() => {}} />
      </ul>
    </Main>
  ),
};

/** Narrow, in 中文, in dark. */
export const AccountRowsNarrowChinese: StoryObj = {
  decorators: [chinese, dark],
  render: () => (
    <Main width={600}>
      <ul className="m-0 grid list-none gap-1 border border-line bg-raised p-0 py-2">
        {chineseRows().slice(0, 3).map((item) => <AttentionRow key={item.id} item={item} acts={ACTS} onOpen={() => {}} />)}
      </ul>
    </Main>
  ),
};

/** The questions in 中文. */
export const AskingChinese: StoryObj = { ...Asking, decorators: [chinese] };

/** An act on its way: the row's acts held, its door still a door. */
export const Acting: StoryObj = {
  render: () => (
    <Main width={1180}>
      <ul className="m-0 list-none border border-line bg-raised p-0">
        <AttentionRow item={PROPOSAL} acts={ACTS} onOpen={() => {}} busy />
      </ul>
    </Main>
  ),
};

/**
 * The band's rows one by one: parked, its note by code and then one from before parts (LANG1b), a quest parked on its
 * failed sessions, the two kinds of ask, a quest nobody can take, a long title, one that said nothing — and last, a
 * parked row with no door, as a browser shows it.
 */
export const Rows: StoryObj = {
  render: () => (
    <Main width={900}>
      <ul className="m-0 list-none border border-line bg-raised p-0">
        <AttentionRow item={PARKED} onOpen={() => {}} />
        <AttentionRow item={PARKED_BEFORE} onOpen={() => {}} />
        <AttentionRow item={PARKED_QUEST} acts={ACTS} onOpen={() => {}} />
        {/* A folder waiting on the person's trust (D73): the folder, what it holds, and why. */}
        <AttentionRow item={TRUST_ROW} acts={ACTS} onOpen={() => {}} />
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
        <AttentionRow item={PROPOSAL} acts={ACTS} onOpen={() => {}} />
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
        <AttentionRow item={GO_AHEAD} acts={ACTS} onOpen={() => {}} />
        <AttentionRow item={DEPARTURE} acts={ACTS} onOpen={() => {}} />
        <AttentionRow item={RULE} acts={ACTS} onOpen={() => {}} />
        <AttentionRow item={REVIEWS[0]!} onOpen={() => {}} />
        <AttentionRow item={{ ...PARKED, id: 'quiet', note: undefined, since: at(4) }} onOpen={() => {}} />
        <AttentionRow
          item={{
            ...PARKED,
            id: 'cjk',
            title: '让世界流式加载在每一帧内限制水合工作量，并把预算暴露在区块 API 上，供上层调度器读取',
            where: '世界流式加载引擎',
            note: {
              note: 'It stopped with its quest still taken, to ask you: …',
              parts: [
                { code: 'ended.parked-asked', values: {}, text: 'It stopped with its quest still taken, to ask you:' },
                { words: '有两条路可走；我建议在区块 API 上限流，这正是委托所要求的。', by: 'agent' },
              ],
            },
          }}
          onOpen={() => {}}
        />
        {/* No door: a parked session in a browser, which has no Sessions to open it in. */}
        <AttentionRow item={{ ...PARKED, id: 'browser' }} />
      </ul>
    </Main>
  ),
};

/**
 * Quests parked on their failed sessions (SESSUX1i): one as English says it, and one with a long 中文 title and the
 * sitting sentence as 中文 says it from the number the tick carries; the title truncates to its line.
 */
export const ParkedQuestRows: StoryObj = {
  render: () => (
    <Main width={900}>
      <ul className="m-0 list-none border border-line bg-raised p-0">
        <AttentionRow item={PARKED_QUEST} acts={ACTS} onOpen={() => {}} />
        <AttentionRow
          item={{
            ...PARKED_QUEST,
            id: 'cjk-quest',
            title: '让世界流式加载在每一帧内限制水合工作量，并把预算暴露在区块 API 上，供上层调度器读取',
            detail: '`#cjk-quest` 上已有 3 个会话失败，且没有落地任何工作——已挂起，因为继续尝试只会消耗账户，而不会有进展。弄清原因后，`daoris driver retry cjk-quest` 会让它重新开始。',
          }}
          acts={ACTS}
          onOpen={() => {}}
        />
      </ul>
    </Main>
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
