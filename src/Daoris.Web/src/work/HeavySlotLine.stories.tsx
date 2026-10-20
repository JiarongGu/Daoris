import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Session } from '../data/api';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import { InTheme } from '../plugins/storyIcons';
import { heavyPartOf, type HeavySlot } from './heavySlot';
import { HeavySlotLine, HeavySlotRow, heavyRow } from './HeavySlotLine';
import { DetailsFold } from './head/DetailsFold';
import { SessionRow } from './rail/SessionRow';
import { useTranslation } from 'react-i18next';

// HEAVYSLOT1 (the UI/UX design §3.5 and §4's `HeavySlotLine`, D185): a heavy run seen running and waiting. Each state as the rail
// draws its two rows, as the session's line with its door and its *Details*' gates, and as the repository's *Heavy run* row:
// running, waiting, a failed gate, held by a run from a terminal, and none; at the frame's widths, in English, 中文 and dark.

const AT = Date.now();
const ago = (minutes: number) => new Date(AT - minutes * 60_000).toISOString();

const HOLDER: Session = {
  id: 's-fts', quest: 'fts1', repository: 'engine', adapter: 'claude-code', state: 'working', kind: 'driven',
  created: ago(40), updated: ago(1),
};
const WAITER: Session = { ...HOLDER, id: 's-chan', quest: 'chan1', created: ago(20) };
const TITLES: Record<string, string> = { 's-fts': "Fix the FTS index's ranking", 's-chan': "Add the ask's channel" };
const titleOf = (id: string) => TITLES[id] ?? id;

const RUNNING: HeavySlot = {
  repository: 'engine',
  holder: { session: 's-fts', gates: ['build', 'test:web', 'e2e'], since: ago(9), now: { gate: 'test:web', at: 1, since: ago(6) } },
  waiters: [{ session: 's-chan', since: ago(3) }],
};
const FAILED: HeavySlot = {
  ...RUNNING,
  holder: { ...RUNNING.holder!, failed: { gate: 'test:web', exit: 1 } },
  waiters: [],
};
const TERMINAL: HeavySlot = {
  repository: 'engine',
  holder: { gates: ['build', 'test:web'], since: ago(4), now: { gate: 'build', at: 0, since: ago(4) } },
  waiters: [{ session: 's-chan', since: ago(2) }, { since: ago(1) }],
};
const NONE: HeavySlot = { repository: 'engine', waiters: [] };

/** The rail's rows, the session's line and its *Details*, and the repository's row, for one slot. */
function Slot({ slot, session }: { slot: HeavySlot; session: Session }) {
  const { t } = useTranslation();
  const part = heavyPartOf(slot, session.id);
  const row = heavyRow(t, slot, session.id, titleOf, () => {});
  return (
    <div className="grid gap-5">
      <ul className="m-0 w-72 list-none rounded-card border border-line bg-raised p-1">
        {[HOLDER, WAITER].map((one) => (
          <SessionRow
            key={one.id}
            session={one}
            name={titleOf(one.id)}
            heavy={heavyPartOf(slot, one.id)}
            heavyBehind={slot.holder?.session ? titleOf(slot.holder.session) : null}
          />
        ))}
      </ul>
      {part && <HeavySlotLine part={part} titleOf={titleOf} onOpen={() => {}} />}
      {row && (
        <DetailsFold summary="" rows={[row]} />
      )}
      <dl className="m-0 grid grid-cols-[max-content_1fr] items-baseline gap-x-4">
        <dt className="text-meta text-ink-faint">{t('projects.heavy.slot')}</dt>
        <dd className="m-0 min-w-0"><HeavySlotRow slot={slot} titleOf={titleOf} onOpen={() => {}} /></dd>
      </dl>
    </div>
  );
}

const meta: Meta<typeof Slot> = {
  title: 'Work/HeavySlotLine',
  component: Slot,
  args: { slot: RUNNING, session: HOLDER },
  decorators: [storyWidths.wide],
  // *Details* opened, so each story shows the gates as phases or the wait.
  play: async ({ canvasElement }) => {
    [...canvasElement.querySelectorAll<HTMLButtonElement>('button')]
      .find((button) => /^(Details|详情)/.test(button.textContent ?? ''))?.click();
  },
};
export default meta;

type Story = StoryObj<typeof Slot>;

/** Running, gate 2 of 3: the live mark, its gate and its time; the waiter neutral; the repository's row with one waiting. */
export const Running: Story = {};
export const RunningHalf: Story = { decorators: [storyWidths.half] };
/** At the main area's 400 px floor the line wraps after its separators. */
export const RunningNarrow: Story = { decorators: [storyWidths.narrow] };
export const RunningChinese: Story = { decorators: [storyWidths.half, chinese] };
export const RunningDark: Story = { decorators: [storyWidths.narrow, (Story) => <InTheme theme="dark"><Story /></InTheme>] };

/** Waiting behind another session: neutral, never the person's hue, its holder a door. */
export const Waiting: Story = { args: { session: WAITER } };
export const WaitingNarrow: Story = { args: { session: WAITER }, decorators: [storyWidths.narrow] };
export const WaitingChinese: Story = { args: { session: WAITER }, decorators: [storyWidths.narrow, chinese] };

/** A gate failed: the line says where, and *Details*' phase says its exit. */
export const FailedGate: Story = { args: { slot: FAILED } };
export const FailedGateChinese: Story = { args: { slot: FAILED }, decorators: [storyWidths.half, chinese] };

/** Held by a run from a terminal: no door for it, and the waiter is behind *a run from a terminal*. */
export const HeldFromATerminal: Story = { args: { slot: TERMINAL, session: WAITER } };
export const HeldFromATerminalNarrow: Story = { args: { slot: TERMINAL, session: WAITER }, decorators: [storyWidths.narrow] };

/** No heavy run now: the rows say nothing of it, and the repository's row says so. */
export const None: Story = { args: { slot: NONE } };
export const NoneChinese: Story = { args: { slot: NONE }, decorators: [storyWidths.narrow, chinese] };
