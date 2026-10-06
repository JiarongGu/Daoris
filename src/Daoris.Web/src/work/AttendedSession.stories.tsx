import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import type { Quest, Session } from '../api';
import i18n from '../i18n';
import { buildChain } from '../map/chain';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../storyLanguage';
import { AttendedSession } from './AttendedSession';
import { sessionFacts } from './headFacts';
import { questName } from './identity';
import { SessionPageHead } from './SessionPageHead';
import { answer, SESSION_CHAIN } from './traceFixtures';
import { ViewMain } from './ViewMain';

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

// The assembled region: the record and the observed layer over it. The stream is deliberately not
// part of this — it has one home, the frame's output panel (D55) — and neither is the composer,
// which sits beneath the region rather than inside it.

const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const SESSION: Session = {
  id: 's1a2b3c4',
  quest: '7a82cc',
  repository: 'engine',
  adapter: 'claude-code',
  state: 'working',
  kind: 'driven',
  harnessVersion: '2.1.4',
  profile: 'owner',
  tree: 'C:/somewhere/.daoris/trees/default/engine/streaming-budget',
  created: at(134),
  updated: at(4),
};

const QUEST: Quest = {
  id: '7a82cc',
  from: 'platform',
  to: 'engine',
  title: 'Expose a streaming budget on the chunk API',
  body: 'World streaming needs a per-frame cap.',
  status: 'Taken',
  filed: at(10_000),
  updated: at(130),
};

const meta: Meta<typeof AttendedSession> = {
  title: 'Work/AttendedSession',
  component: AttendedSession,
  args: { session: SESSION, quest: QUEST },
  decorators: [(Story) => <div className="max-w-3xl p-4"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof AttendedSession>;

export const Attending: Story = {};

/** Nothing chosen. The frame lands here on a first launch, so it is a designed state. */
export const NothingAttended: Story = { args: { session: null, quest: null } };

/** Parked — the whole region reorganised around the thing that is waiting on a person. */
export const Parked: Story = {
  args: {
    session: {
      ...SESSION,
      state: 'awaiting-person',
      created: at(52),
      updated: at(11),
      note: 'Two ways forward.\n\n1. Cap hydration in the scheduler — smaller change, but the budget then lives away from the API that spends it.\n2. Cap it on the chunk API itself — touches more call sites, and the budget ends up where the work is.\n\nI recommend the second: the quest asks for the budget on the chunk API, and option 1 would leave that promise half kept.',
    },
  },
};

/** A step of a chain (MAP1): the strip under the head says what came before and what is to come. */
export const InAChain: Story = {
  args: {
    quest: { ...QUEST, parent: '19c0de', then: [{ to: 'platform', title: 'Report the cap back', body: '' }] },
    chain: buildChain(
      '7a82cc',
      [
        { ...QUEST, parent: '19c0de', then: [{ to: 'platform', title: 'Report the cap back', body: '' }] },
        { ...QUEST, id: '19c0de', from: 'ask #c7c4de', title: 'Measure what a frame hydrates', status: 'Done' },
      ],
      [SESSION, { ...SESSION, id: 's0measure', quest: '19c0de', state: 'completed', created: at(400) }],
    ),
  },
};

// ——— Titles first, on the assembled page (UX7c, D152 §7; the UX7 design §5.2, §6.2): the pinned head, the record folded
// under it, and the conversation's first lines. The install's fourth attempt at a re-filed quest.

const REFILED_TITLE = '(Re-filed from ask #0181ea, whose quest was taken outside the driver with no session.)';
const ATTEMPT: Session = {
  ...SESSION, id: 'c2b8d293', quest: '38e9876e3f53', adapter: 'claude-code-acp', harnessVersion: '0.84.0', profile: 'account-1',
  created: at(2), updated: at(2),
};
const REFILED_QUEST: Quest = {
  ...QUEST, id: '38e9876e3f53', from: 'ask #4e3aaf', title: REFILED_TITLE, status: 'Taken',
  body: `${REFILED_TITLE}\ncontinue the prod half of the ticket`,
};
const FAILED_BEFORE = [1, 2, 3].map((n): Session => ({
  ...ATTEMPT, id: `f${n}000000`, state: 'failed', created: at(60 - n * 10), updated: at(59 - n * 10),
}));

/** The page as the install's session would read: the head, the record folded, then the agent's first words. */
function SessionPage({ quest }: { quest: Quest }) {
  const title = questName(quest);
  return (
    <div className="flex h-[47.5rem] w-[50rem] max-w-full flex-col border border-line bg-page">
      <ViewMain
        gutters="session"
        header={(
          <SessionPageHead
            session={ATTEMPT} title={title} shown="working" onAct={() => {}}
            facts={sessionFacts(i18n.t.bind(i18n), {
              session: ATTEMPT, shown: 'working',
              grouping: { session: ATTEMPT.id, group: 'working', shown: 'working', archived: false, teammate: false, strikes: 3 },
            })}
            acts={['stop', 'review', 'openFolder', 'terminal', 'detach', 'copy']}
          />
        )}
      >
        <AttendedSession
          session={ATTEMPT} quest={quest} headed
          chain={buildChain(quest.id, [quest], [...FAILED_BEFORE, ATTEMPT])}
          trace={{ open: false, onToggle: nothing }}
        />
        {Array.from({ length: 12 }, (_, line) => (
          <p key={line} className="m-0 py-1 text-body text-ink-soft">The agent’s words, line {line + 1}.</p>
        ))}
      </ViewMain>
    </div>
  );
}

/** With its quest's short title (SESSUX1j): the work in the head, the whole title once under it. */
export const PageWithAShortTitle: Story = {
  render: () => <SessionPage quest={{ ...REFILED_QUEST, short: 'Continue the production half' }} />,
};

/** No short title: the title as written in the head, one line, and said nowhere else. */
export const PageWithNoShortTitle: Story = {
  render: () => <SessionPage quest={REFILED_QUEST} />,
};

/** Finished, reviewable: the lifetime, the quest's close, and what came out of it. */
export const Finished: Story = {
  args: {
    session: {
      ...SESSION,
      state: 'completed',
      created: at(95),
      updated: at(50),
      note: 'gates green; the session closed the quest through its own door.',
      evidence: 'commits landed:\nc0ffee12 cap hydration work per frame\ndeadbee1 expose the budget on the chunk API',
    },
    quest: { ...QUEST, status: 'Done', updated: at(51) },
  },
};

// ——— How it came to be (TRACE1b, D143): folded at the foot of its record, then open on its chain.

const nothing = () => {};

/** How it came to be, folded: a line above the conversation, nothing read until it opens. */
export const HowItCameToBeFolded: Story = { args: { trace: { open: false, onToggle: nothing } } };

/** How it came to be, open: its ask and the person's words, its quest, and this session whole. */
export const HowItCameToBeOpen: Story = {
  args: {
    session: { ...SESSION, id: 's2', state: 'completed' },
    trace: { open: true, onToggle: nothing, answer: answer(SESSION_CHAIN), onSession: nothing, onQuest: nothing },
  },
};

/** Open, in 中文. */
export const HowItCameToBeChinese: Story = { ...HowItCameToBeOpen, decorators: [chinese] };

/** Open, in dark. */
export const HowItCameToBeDark: Story = { ...HowItCameToBeOpen, decorators: [dark] };

/** Open, in 中文 and dark. */
export const HowItCameToBeChineseDark: Story = { ...HowItCameToBeOpen, decorators: [chinese, dark] };
