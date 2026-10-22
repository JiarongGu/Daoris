import type { Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { sessionWidths } from '../../design/storyWidths';
import type { PieceRow } from './pieceRows';
import { Pieces } from './Pieces';

// A driven session's Pieces section, under its head (COAGENT1b, D199; the co-agent design §6.1): each piece it asked another agent to
// write, as the person wrote it, where the driver's look left it, its frozen commit and its files; a working piece's door and stop;
// and the ask. The section draws whatever it is handed, so every state is here. In English, 中文 and dark, at the frame's widths.

const row = (over: Partial<PieceRow> & Pick<PieceRow, 'id' | 'piece' | 'state'>): PieceRow => ({
  paths: ['src'], agent: 'codex-acp', askedBy: 'person', at: '2026-10-22T10:00:00Z', code: null, end: null, partner: null,
  startedOn: 'codex-acp', commit: null, ref: null, files: null, beyond: null, ...over,
});

const NAMES: Record<string, string> = { 'codex-acp': 'Codex', 'claude-code': 'Claude Code', 'stub-b': 'Stub B' };

const ROSTER = { harnesses: ['claude-code', 'codex-acp', 'stub-b'], names: NAMES };

const COMMIT = 'c0ffee1234567890c0ffee1234567890c0ffee12';

const PIECES: PieceRow[] = [
  row({ id: 'p1', piece: 'Make the report’s window seven days, and its test with it.', state: 'started', partner: 'pt1' }),
  row({
    id: 'p2', piece: 'Write the loader’s first-chunk test.', state: 'handed', end: 'answered', partner: 'pt2', commit: COMMIT,
    ref: 'refs/daoris/pieces/p2', files: ['src/loader.test.ts', 'README.md'], beyond: ['README.md'],
  }),
  row({
    id: 'p3', piece: 'Rename the budget’s field.', state: 'handed', end: 'out-of-time', partner: 'pt3', commit: COMMIT,
    ref: 'refs/daoris/pieces/p3', files: ['src/budget.ts'], beyond: [],
  }),
  row({ id: 'p4', piece: 'Version the save format.', state: 'refused', code: 'not-measured', startedOn: null }),
  row({ id: 'p5', piece: 'Split the hydrator.', state: 'uncertain', code: 'job-unknown', partner: 'pt5' }),
  row({ id: 'p6', piece: 'Drop the old cache.', state: 'held', code: 'import-refused', partner: 'pt6' }),
  row({ id: 'p7', piece: 'Read the queue’s bound from the settings.', state: 'asked', startedOn: null }),
];

const meta: Meta<typeof Pieces> = {
  title: 'Work/Pieces',
  component: Pieces,
  args: {
    headingId: 'pieces-story',
    section: {
      pieces: PIECES, nameOf: (agent) => NAMES[agent] ?? agent, onOpen: () => {}, onStop: () => {},
      ask: { from: 'claude-code', roster: ROSTER, onAsk: () => {} },
    },
  },
  decorators: [sessionWidths.half],
};
export default meta;

type Story = StoryObj<typeof Pieces>;

/** A piece working, one answered with its commit and a file beyond it, one out of time with its work kept, refused, uncertain, held, asked. */
export const EachState: Story = {};

/** The same at a 1546 px window. */
export const EachStateWide: Story = { decorators: [sessionWidths.wide] };

/** The same at the main area's 400 px floor. */
export const EachStateNarrow: Story = { decorators: [sessionWidths.narrow] };

/** The same in 中文. */
export const EachStateChinese: Story = {
  args: {
    headingId: 'pieces-story',
    section: {
      pieces: [
        row({ id: 'p1', piece: '把报告的时间窗口改为七天，并同时改它的测试。', state: 'started', partner: 'pt1' }),
        row({
          id: 'p2', piece: '为加载器的第一个区块写测试。', state: 'handed', end: 'answered', partner: 'pt2', commit: COMMIT,
          ref: 'refs/daoris/pieces/p2', files: ['src/loader.test.ts', 'README.md'], beyond: ['README.md'],
        }),
        row({ id: 'p4', piece: '给存档格式加上版本。', state: 'refused', code: 'not-measured', startedOn: null }),
        row({ id: 'p5', piece: '拆分水合器。', state: 'uncertain', code: 'job-unknown', partner: 'pt5' }),
      ],
      nameOf: (agent) => NAMES[agent] ?? agent, onOpen: () => {}, onStop: () => {},
      ask: { from: 'claude-code', roster: ROSTER, onAsk: () => {} },
    },
  },
  decorators: [chinese],
};

/** The same in dark. */
export const EachStateDark: Story = {
  decorators: [(Story) => <InTheme theme="dark"><Story /></InTheme>],
};

/** None asked yet: the heading and the ask alone. */
export const NoneYet: Story = {
  args: { headingId: 'pieces-story', section: { pieces: [], nameOf: (agent) => NAMES[agent] ?? agent, ask: { from: 'claude-code', roster: ROSTER, onAsk: () => {} } } },
};

/** Read only, as a browser shows them: no ask, no door and no stop. */
export const NothingToPress: Story = {
  args: { headingId: 'pieces-story', section: { pieces: PIECES, nameOf: (agent) => NAMES[agent] ?? agent } },
};
