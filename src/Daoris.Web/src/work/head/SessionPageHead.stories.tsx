import type { ComponentProps, ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import i18n from '../../locales/i18n';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { sessionWidths } from '../../design/storyWidths';
import type { Session } from '../../data/api';
import { AskHelper } from '../start/AskHelper';
import { HandOverAsk, type HandOverRoster } from '../start/HandOverAsk';
import { HandOverOfferLine } from '../start/HandOverOfferLine';
import { type SessionFactsOf, sessionFacts, sessionFactsTip } from './headFacts';
import { DeleteAsk, SessionPageHead, StopAsk } from './SessionPageHead';
import { PauseAsk } from '../pausing/WorkAsks';

// A session's page header (SESSUX1d, D126 §3.2) pinned in Sessions' main area, in each state's acts, each stop's ask
// (§3.3), a teammate's record, below 560 px and with a 中文 title. A molecule, so each is reached by passing it; the
// conversation below is stand-in lines, scrolled under the header. Drawn in a session's page at the frame's widths (the
// UI/UX design R6): the helper gives the session's gutters, which the header's sticky box reaches over as it does there.

const SESSION: Session = {
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', adapter: 'claude-code', state: 'working', kind: 'driven',
  created: '2026-10-02T09:00:00Z', updated: '2026-10-02T09:30:00Z',
};

const TITLE = 'Expose a streaming budget on the chunk API so world streaming can cap hydration work per frame';

/** The header with something to scroll under it, as a long run's page has. */
function Page({ children }: { children: ReactNode }) {
  return (
    <>
      {children}
      {Array.from({ length: 40 }, (_, line) => (
        <p key={line} className="m-0 py-1 text-body text-ink-soft">The agent’s words, line {line + 1}.</p>
      ))}
    </>
  );
}

const meta: Meta<typeof SessionPageHead> = {
  title: 'Work/SessionPageHead',
  component: SessionPageHead,
  args: {
    session: SESSION, title: TITLE, shown: 'working',
    facts: ['engine', '2m'],
    acts: ['stop', 'review', 'openFolder', 'terminal', 'detach', 'copy'],
    onAct: () => {},
  },
  render: (args) => <Page><SessionPageHead {...args} /></Page>,
  // A session's page at half a 1546 px window, about the main area the session list leaves.
  decorators: [sessionWidths.half],
};
export default meta;

type Story = StoryObj<typeof SessionPageHead>;

/** A running driven session: *Stop…*, then its ⋯ with its review, its folder, a terminal there, its window and its id. */
export const Running: Story = {};

/** Waiting on you: *Stop…* and the ⋯; the answer is the card's under the header, never a second owner here. */
export const WaitingOnYou: Story = {
  args: { session: { ...SESSION, state: 'awaiting-person' }, shown: 'awaiting-person' },
};

/** A parked quest's last session: *Try again*, the loud act, then its ⋯. */
export const Parked: Story = {
  args: {
    session: { ...SESSION, state: 'failed' }, shown: 'parked',
    acts: ['retry', 'review', 'openFolder', 'terminal', 'detach', 'copy'], primary: 'retry',
  },
};

/** Stopped, its stop holding its quest (SESSUX1b): *Try again* releases it. */
export const StoppedHoldingItsQuest: Story = {
  args: {
    session: { ...SESSION, state: 'stopped' }, shown: 'stopped',
    acts: ['retry', 'review', 'openFolder', 'terminal', 'detach', 'archive', 'copy'], primary: 'retry',
  },
};

/** The head of a session whose provider failed, its facts read as the frame reads them while a transient wait runs. */
function TryingAgainHead(args: ComponentProps<typeof SessionPageHead>) {
  const { t } = useTranslation();
  const retrying = new Date(Date.now() + 4 * 60_000).toISOString();
  return <Page><SessionPageHead {...args} facts={sessionFacts(t, { session: SESSION, shown: 'working', retrying })} /></Page>;
}

/** AGENTERRORS1a2 (D210 §5.1, §8): its provider failed and Daoris waits to try again in the same conversation; the facts say when. */
export const TryingAgain: Story = { render: (args) => <TryingAgainHead {...args} /> };

/** The same in 中文: 将于 … 重试. */
export const TryingAgainInChinese: Story = { ...TryingAgain, decorators: [sessionWidths.half, chinese] };

/** Past its transient bound (AGENTERRORS1a2): *Try again* leads, releasing that end alone, in the same conversation. */
export const PastItsTransientBound: Story = {
  args: {
    session: { ...SESSION, state: 'failed', noteParts: [{ code: 'ended.transient', values: { count: 5, minutes: 31 } }] }, shown: 'failed',
    acts: ['retry', 'handOver', 'review', 'openFolder', 'terminal', 'detach', 'archive', 'copy'], primary: 'retry',
  },
};

/** To review: *Review* leads, since nothing but the person will verify the work (D37). */
export const ToReview: Story = {
  args: {
    session: { ...SESSION, state: 'completed' }, shown: 'completed',
    acts: ['review', 'openFolder', 'terminal', 'detach', 'copy'], primary: 'review',
  },
};

/** A teammate's record (SYNC4): read here, offered only what reaches no process. */
export const ATeammatesRecord: Story = {
  args: { session: { ...SESSION, id: 'person@machine-b/s1a2b3c4', state: 'completed' }, shown: 'completed', acts: ['archive', 'copy'] },
};

/** A driven session holding its quest, its stop asking once under the header. */
export const StopAskingHeld: Story = {
  args: {
    asking: <StopAsk sentence={i18n.t('work.stop.drivenHeld')} onStop={() => {}} onClose={() => {}} />,
  },
};

/** A driven session before it took its quest: another machine may still take it. */
export const StopAskingBeforeTheTake: Story = {
  args: {
    session: { ...SESSION, state: 'queued' }, shown: 'queued',
    asking: <StopAsk sentence={i18n.t('work.stop.drivenOpen', { quest: 'abc123' })} onStop={() => {}} onClose={() => {}} />,
  },
};

/** Waiting on you with no process left: it stops unanswered. */
export const StopAskingParked: Story = {
  args: {
    session: { ...SESSION, state: 'awaiting-person' }, shown: 'awaiting-person',
    asking: <StopAsk sentence={i18n.t('work.stop.parked')} onStop={() => {}} onClose={() => {}} />,
  },
};

/** A chat: it ends the conversation, and Finish is named as the gentler ending. */
export const StopAskingAChat: Story = {
  args: {
    session: { ...SESSION, quest: null, kind: 'chat' }, title: 'Cap the hydration per frame', shown: 'idle',
    asking: <StopAsk sentence={i18n.t('work.stop.chat')} onStop={() => {}} onClose={() => {}} />,
  },
};

/** An intake: the ask stays a proposal, as its card said before its stop moved here. */
export const StopAskingAnIntake: Story = {
  args: {
    session: { ...SESSION, quest: null, kind: 'chat', ask: '0fda18', repository: 'ask #0fda18' }, title: 'Intake for ask #0fda18',
    acts: ['stop', 'detach', 'copy'],
    asking: <StopAsk sentence={i18n.t('work.intake.stopMeans')} onStop={() => {}} onClose={() => {}} />,
  },
};

/** An ended conversation that served no quest (SESSUX1f): *Delete…* in its ⋯, after the archive marks. */
export const ADeletableConversation: Story = {
  args: {
    session: { ...SESSION, quest: null, kind: 'chat', state: 'completed' }, title: 'Cap the hydration per frame', shown: 'completed',
    acts: ['openFolder', 'terminal', 'detach', 'archive', 'delete', 'copy'],
  },
};

/** Its delete asking once under the header (§5.4): what goes, and that nothing brings it back. */
export const DeleteAsking: Story = {
  args: {
    ...ADeletableConversation.args,
    asking: <DeleteAsk onDelete={() => {}} onClose={() => {}} />,
  },
};

/** At the main area's 400 px floor (under 640 px): the acts take their own right-aligned row under the facts. */
export const Narrow: Story = {
  args: { acts: ['stop', 'retry', 'review', 'openFolder', 'detach', 'copy'], primary: 'retry' },
  decorators: [sessionWidths.narrow],
};

/** A 中文 title, one line, whole on its tip. */
export const ChineseTitle: Story = {
  args: { title: '为区块接口提供流式预算，让世界流式加载能按帧限制水合工作量，避免卡顿和掉帧' },
};

// ——— Titles first (UX7c, D152 §7; the UX7 design §5.2): its word, then its title on one line, then its facts by state.
// The install's fourth attempt at a re-filed quest, with and without a short title.

const REFILED_TITLE = '(Re-filed from ask #0181ea, whose quest was taken outside the driver with no session.)';

/** Its quest's short title (SESSUX1j): the head says the work, and the fourth attempt is a fact on its line. */
export const FourthAttemptWithAShortTitle: Story = {
  args: { title: 'Continue the production half', facts: ['engine', 'account-1', '2m', 'attempt 4: the 3 before it failed'] },
};

/** No short title: the title as written, one line, whole in its tip; the word stays where it was. */
export const FourthAttemptWithNoShortTitle: Story = {
  args: { title: REFILED_TITLE, facts: ['engine', 'account-1', '2m', 'attempt 4: the 3 before it failed'] },
};

/** WAITSHOWN1: working, with nothing yet answered since it was asked: the fact follows its age. */
export const WorkingWithNoAnswerYet: Story = {
  args: { title: 'Continue the production half', facts: ['engine', '25m', 'no answer yet, asked Oct 14, 09:12 GMT'] },
};
export const WorkingWithNoAnswerYetChinese: Story = {
  args: { title: '继续生产环境的部分', facts: ['engine', '25 分钟', '尚无回答，10月14日 09:12 GMT发出'] },
  decorators: [chinese],
};

/** Parked after its failed sessions: its line says so, in the row's words, beside *Try again*. */
export const ParkedFacts: Story = {
  args: {
    session: { ...SESSION, state: 'failed' }, shown: 'parked', title: 'Continue the production half',
    facts: ['engine', 'after 3 failed sessions', 'ran 14m'],
    acts: ['retry', 'review', 'openFolder', 'terminal', 'detach', 'copy'], primary: 'retry',
  },
};

/** The same at 680 px of window (the main area's half width): the acts take their own right-aligned row under the facts. */
export const FourthAttemptNarrow: Story = {
  args: { ...FourthAttemptWithNoShortTitle.args },
  decorators: [sessionWidths.half],
};

// ——— HEAD2c (D162; the entry-and-heads design §2): one line whatever the request's length, the acts where they were, the
// facts the repository, its age and one fact; the request as written is its record's *Details*, never a line under the head.
// At the main area of a 1546 px window and of a 680 px one, in dark and in 中文.

const LONG_REQUEST = 'Repair streaming hydration budget for the sync look so a long request never wraps the head into a second line';

// The main area at the frame's widths: `sessionWidths.wide` at a 1546 px window, `sessionWidths.half` at 680 px.

/** A long request at 1546 px: one line, cut with its tip, *Stop…* and ⋯ where they are, the facts one line. */
export const OneLineAt1546: Story = {
  args: { title: LONG_REQUEST, facts: ['engine', '12m', 'attempt 4: the 3 before it failed'] },
  decorators: [sessionWidths.wide],
};

/** The same at 680 px: the acts take their own row under the facts, *Stop…* still shown. */
export const OneLineAt680: Story = {
  args: { ...OneLineAt1546.args },
  decorators: [sessionWidths.half],
};

/** The same in dark. */
export const OneLineDark: Story = {
  args: { ...OneLineAt1546.args },
  decorators: [sessionWidths.wide, (Story) => <InTheme theme="dark"><Story /></InTheme>],
};

/** The same in 中文, at 680 px. */
export const OneLineChinese: Story = {
  args: {
    title: '修复同步外观中的流式水合预算，让很长的请求也不会把页头撑成第二行，并保持按钮不动',
    facts: ['engine', '12 分钟', '第 4 次尝试：之前 3 次失败'],
  },
  decorators: [sessionWidths.half, chinese],
};

// ——— Whom it continues and whom a helper helps (MIXAGENT3a-i, the mixed-agents design §4): the facts line says the agent a
// handed-over conversation continues and why, in the hand-over's words, and a helper whom it helps; in English, 中文 and dark.

const HANDED: Session = { ...SESSION, quest: null, kind: 'chat', from: 'a1b2c3d4', fromWhy: 'near', fromBy: 'daoris' };
const A_HELPER: Session = { ...SESSION, id: 'h3lp3r00', quest: null, kind: 'chat', serves: 's1a2b3c4' };

/** A conversation handed over from Codex because its account was near its limit. */
export const ContinuesAnother: Story = {
  args: {
    session: HANDED, title: 'Go on with the hydrator fix',
    facts: ['engine', 'continues from Codex', 'its account was near its limit', 'started 2m ago'],
    acts: ['stop', 'handOver', 'openFolder', 'terminal', 'detach', 'copy'],
  },
};
export const ContinuesAnotherChinese: Story = {
  args: { ...ContinuesAnother.args, facts: ['engine', '接续 Codex 的会话', '它的账户接近上限', '2 分钟前开始'] },
  decorators: [chinese],
};
export const ContinuesAnotherDark: Story = {
  args: { ...ContinuesAnother.args },
  decorators: [(Story) => <InTheme theme="dark"><Story /></InTheme>],
};

/** A helper, titled as one, says the session it answers by its title. */
export const Helping: Story = {
  args: {
    session: A_HELPER, title: 'Helper',
    facts: ['engine', 'helping Expose a streaming budget on the chunk API', 'started 1m ago'],
    acts: ['stop', 'openFolder', 'detach', 'copy'],
  },
};
export const HelpingChinese: Story = {
  args: { ...Helping.args, title: '助手', facts: ['engine', '正在协助「Expose a streaming budget on the chunk API」', '1 分钟前开始'] },
  decorators: [chinese],
};
export const HelpingDark: Story = {
  args: { ...Helping.args },
  decorators: [(Story) => <InTheme theme="dark"><Story /></InTheme>],
};

// ——— *Go on with another agent…* (MIXAGENT1b-i, D163): the hand-over's ask under the head of a conversation, idle and working,
// and a refusal said in it; each at 680 px of window, in English and 中文.

const ROSTER: HandOverRoster = {
  harnesses: ['claude-code', 'codex', 'gemini'],
  names: { 'claude-code': 'Claude Code', codex: 'Codex', gemini: 'Gemini' },
  accounts: {
    'claude-code': [{ name: 'personal', login: 'in' }],
    codex: [{ name: 'team', login: 'in', account: 'team@example.com' }, { name: 'spare', login: 'in' }],
    gemini: [],
  },
};

const A_CONVERSATION: Session = { ...SESSION, quest: null, kind: 'chat', state: 'completed', repository: 'engine' };

/** An idle conversation: the ask says what carries over, with the agent chip on the first other agent, *Hand over* and *Never mind*. */
export const HandOverAskingIdle: Story = {
  args: {
    session: A_CONVERSATION, title: 'Cap the hydration per frame', shown: 'completed',
    acts: ['handOver', 'openFolder', 'terminal', 'detach', 'archive', 'delete', 'copy'],
    asking: <HandOverAsk from="claude-code"roster={ROSTER} onHandOver={() => {}} onClose={() => {}} />,
  },
  decorators: [sessionWidths.half],
};

/** While it works: *Stop…* stays the head's danger button, *Stop and hand over…* is in the ⋯, and the ask says its turn is stopped. */
export const HandOverAskingWhileItWorks: Story = {
  args: {
    session: { ...A_CONVERSATION, state: 'working' }, title: 'Cap the hydration per frame', shown: 'working',
    acts: ['stop', 'handOver', 'openFolder', 'terminal', 'detach', 'copy'],
    asking: <HandOverAsk from="claude-code"working roster={ROSTER} onHandOver={() => {}} onClose={() => {}} />,
  },
  decorators: [sessionWidths.half],
};

/** A refusal said in the ask, whole, with the choice kept (UX5 U68). */
export const HandOverRefused: Story = {
  args: {
    ...HandOverAskingIdle.args,
    asking: (
      <HandOverAsk
        from="claude-code"roster={ROSTER} onHandOver={() => {}} onClose={() => {}}
        refused={i18n.t('work.handOver.refused.treeGone')}
      />
    ),
  },
  decorators: [sessionWidths.half],
};

/** The idle ask in 中文, at 680 px. */
export const HandOverAskingChinese: Story = {
  args: { ...HandOverAskingIdle.args, title: '限制每帧的水合工作量' },
  decorators: [sessionWidths.half, chinese],
};

/** The working ask in 中文, at 680 px. */
export const HandOverAskingWhileItWorksChinese: Story = {
  args: { ...HandOverAskingWhileItWorks.args, title: '限制每帧的水合工作量' },
  decorators: [sessionWidths.half, chinese],
};

/** The refusal in 中文, at 680 px. */
export const HandOverRefusedChinese: Story = {
  args: {
    ...HandOverRefused.args,
    title: '限制每帧的水合工作量',
    asking: (
      <HandOverAsk
        from="claude-code"roster={ROSTER} onHandOver={() => {}} onClose={() => {}}
        refused={i18n.t('work.handOver.refused.treeGone', { lng: 'zh' })}
      />
    ),
  },
  decorators: [sessionWidths.half, chinese],
};

// ——— *Ask another agent…* (MIXAGENT2b, D163 point 2): the helper's ask under the head, the question and the agent left to Daoris;
// at 680 px of window, in English, 中文 and dark.

/** A conversation's ask: what a helper does, the question, which agent, *Ask* waiting for a question, and *Never mind*. */
export const AskHelperAsking: Story = {
  args: {
    session: A_CONVERSATION, title: 'Cap the hydration per frame', shown: 'completed',
    acts: ['handOver', 'askHelper', 'openFolder', 'terminal', 'detach', 'archive', 'delete', 'copy'],
    asking: <AskHelper from="claude-code" roster={ROSTER} onAsk={() => {}} onClose={() => {}} />,
  },
  decorators: [sessionWidths.half],
};
export const AskHelperAskingChinese: Story = {
  args: { ...AskHelperAsking.args, title: '限制每帧的水合工作量' },
  decorators: [sessionWidths.half, chinese],
};
export const AskHelperAskingDark: Story = {
  args: { ...AskHelperAsking.args },
  decorators: [sessionWidths.half, (Story) => <InTheme theme="dark"><Story /></InTheme>],
};

// ——— Daoris's offer to hand over (MIXAGENT1b-ii, D163): the line under the head where nothing else is asked, near the limit and
// cut, a longer agent and account name, and each in 中文; every one at 680 px of window.

const OFFER_HEAD = {
  session: A_CONVERSATION, title: 'Cap the hydration per frame', shown: 'completed' as const,
  acts: ['handOver', 'openFolder', 'terminal', 'detach', 'archive', 'delete', 'copy'] as const,
};
const offerLine = (offer: Parameters<typeof HandOverOfferLine>[0]['offer'], agentName = 'Codex', accountName: string | null = 'team') => (
  <HandOverOfferLine
    offer={offer} agentName={agentName} accountName={accountName} onGoOn={() => {}} onChoose={() => {}} onDismiss={() => {}}
  />
);
const NEAR: Parameters<typeof HandOverOfferLine>[0]['offer'] = { why: 'near', until: null, agent: 'codex', account: 'team' };
const CUT: Parameters<typeof HandOverOfferLine>[0]['offer'] = { why: 'limit', until: '2026-10-12T21:30:00Z', agent: 'codex', account: 'team' };

/**
 * The account is near its limit by its scope's near (a limit, credits or its number; never the agent's pace warning alone,
 * USAGEREAD3b): why, *Go on with Codex (team)*, *Choose…* and *Not now*.
 */
export const HandOverOfferNear: Story = {
  args: { ...OFFER_HEAD, acts: [...OFFER_HEAD.acts], asking: offerLine(NEAR) },
  decorators: [sessionWidths.half],
};

/** The account is cut: it says until when it is offered again. */
export const HandOverOfferCut: Story = {
  args: { ...HandOverOfferNear.args, asking: offerLine(CUT) },
  decorators: [sessionWidths.half],
};

/** Long names wrap inside the line and keep every press. */
export const HandOverOfferLongNames: Story = {
  args: { ...HandOverOfferNear.args, asking: offerLine(NEAR, 'Gemini CLI for the engine team', 'team-engineering@example.com') },
  decorators: [sessionWidths.half],
};

/** Near, in 中文. */
export const HandOverOfferNearChinese: Story = {
  args: { ...HandOverOfferNear.args, title: '限制每帧的水合工作量' },
  decorators: [sessionWidths.half, chinese],
};

/** Cut, in 中文. */
export const HandOverOfferCutChinese: Story = {
  args: { ...HandOverOfferCut.args, title: '限制每帧的水合工作量' },
  decorators: [sessionWidths.half, chinese],
};

// ——— RAILLINE1: a running shell call's title is its whole command, three lines of the facts line on the install. The line keeps
// to one, the call said by its head in at most 40 characters, the whole line in its tip; at the main area's 400 px floor and at
// 680 px of window, in English and 中文. The facts are `sessionFacts`' own, in the story's language.

const LONG_CALL = 'cd src/Daoris.Web && npm ci --no-audit --no-fund && npx vitest run src/work/rail/SessionRow.test.tsx '
  + 'src/work/head/headFacts.test.ts --reporter=verbose; dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests '
  + '--filter "FullyQualifiedName~RunningCallsTests|FullyQualifiedName~SessionLookTests" --no-build; git status --short';

/** The head of a session running `LONG_CALL`, its facts and their tip worked out in the language the story reads in. */
function RunningALongCallHead(args: ComponentProps<typeof SessionPageHead>) {
  const { t } = useTranslation();
  const of: SessionFactsOf = {
    session: { ...SESSION, created: new Date(Date.now() - 62 * 60_000).toISOString() },
    shown: 'working',
    grouping: {
      session: SESSION.id, group: 'working', shown: 'working', archived: false, teammate: false,
      calling: { title: LONG_CALL, since: new Date(Date.now() - 6 * 60_000).toISOString() },
    },
  };
  return <Page><SessionPageHead {...args} facts={sessionFacts(t, of)} factsTip={sessionFactsTip(t, of)} /></Page>;
}

/** At the main area's 400 px floor: the facts on one line, the call cut, the acts beside the title. */
export const RunningALongCallAt400: Story = {
  args: { title: 'Continue the production half' },
  render: (args) => <RunningALongCallHead {...args} />,
  decorators: [sessionWidths.narrow],
};

/** The same in 中文. */
export const RunningALongCallAt400Chinese: Story = {
  ...RunningALongCallAt400,
  args: { title: '继续生产环境的部分' },
  decorators: [...RunningALongCallAt400.decorators as Decorator[], chinese],
};

/** At 680 px of window. */
export const RunningALongCallAt680: Story = {
  args: { title: 'Continue the production half' },
  render: (args) => <RunningALongCallHead {...args} />,
  decorators: [sessionWidths.half],
};

/** The same in 中文. */
export const RunningALongCallAt680Chinese: Story = {
  ...RunningALongCallAt680,
  args: { title: '继续生产环境的部分' },
  decorators: [sessionWidths.half, chinese],
};

/** A running driven session whose quest an ask asked (PAUSE1e): *Pause quest…* and *Pause ask…* in its ⋯. */
export const RunningWithItsPauses: Story = {
  args: { acts: ['stop', 'pauseQuest', 'pauseAsk', 'review', 'openFolder', 'terminal', 'detach', 'copy'] },
};

/** Its quest's pause asking once under the header, from the driver's plan of what it stops (D132 §2.6). */
export const PauseAsking: Story = {
  args: {
    acts: ['stop', 'pauseQuest', 'pauseAsk', 'review', 'openFolder', 'terminal', 'detach', 'copy'],
    asking: (
      <PauseAsk
        className="mt-2.5" target={{ scope: 'quest', id: 'abc123' }} meanIt="Pause quest"
        lines={[{ key: 'work.pause.ask.stops', values: { count: 1 } }]} onPause={() => {}} onClose={() => {}}
      />
    ),
  },
};

/** Stopped by its ask's pause: *Resume ask*, the loud act, in *Try again*'s place (D132 §6.1). */
export const PausedWithItsAsk: Story = {
  args: {
    session: { ...SESSION, state: 'stopped' }, shown: 'stopped',
    acts: ['resumeAsk', 'review', 'openFolder', 'terminal', 'detach', 'copy'], primary: 'resumeAsk',
  },
};

// ——— The choice its actor ran (MODELROLE1c3, D205 §4.6 item 2, §7.1): the head's quiet *choice n* chip where a later choice than the
// first ran, and *your pick* where the person picked the agent, on the facts line after the facts; at the frame's three widths, in
// English and 中文. A start on its first choice wears none, as `Running` shows.

const LATER: Session = { ...SESSION, choice: '2' };
const PICKED: Session = { ...SESSION, choice: 'pick' };
const CHOICE_FACTS = ['engine', '12m', 'attempt 2: the 1 before it failed'];
const CHOICE_FACTS_ZH = ['engine', '12 分钟', '第 2 次尝试：之前 1 次失败'];

/** Build's second choice ran, at 1546 px: *choice 2* after the facts, the line on one row. */
export const ChoiceAt1546: Story = { args: { session: LATER, title: LONG_REQUEST, facts: CHOICE_FACTS }, decorators: [sessionWidths.wide] };

/** The same at 680 px. */
export const ChoiceAt680: Story = { args: { ...ChoiceAt1546.args }, decorators: [sessionWidths.half] };

/** The same at the main area's 400 px floor: the facts are cut before the chip, never the chip. */
export const ChoiceNarrow: Story = { args: { ...ChoiceAt1546.args }, decorators: [sessionWidths.narrow] };

/** The person's pick, at 680 px: *your pick*. */
export const YourPickAt680: Story = { args: { session: PICKED, title: LONG_REQUEST, facts: CHOICE_FACTS }, decorators: [sessionWidths.half] };

/** *选择 2* in 中文, at 1546 px. */
export const ChoiceChineseAt1546: Story = {
  args: { session: LATER, title: '修复同步外观中的流式水合预算', facts: CHOICE_FACTS_ZH },
  decorators: [sessionWidths.wide, chinese],
};

/** The same in 中文, at 680 px. */
export const ChoiceChineseAt680: Story = { args: { ...ChoiceChineseAt1546.args }, decorators: [sessionWidths.half, chinese] };

/** The same in 中文, at 400 px. */
export const ChoiceChineseNarrow: Story = { args: { ...ChoiceChineseAt1546.args }, decorators: [sessionWidths.narrow, chinese] };

/** *你指定的* in 中文, at 400 px. */
export const YourPickChineseNarrow: Story = {
  args: { session: PICKED, title: '修复同步外观中的流式水合预算', facts: CHOICE_FACTS_ZH },
  decorators: [sessionWidths.narrow, chinese],
};
