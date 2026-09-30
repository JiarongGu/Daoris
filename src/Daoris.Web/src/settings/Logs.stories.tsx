import type { Meta, StoryObj } from '@storybook/react-vite';
import { LOG_FILTERS, LogList, type LogReading } from './Logs';

// Settings → Logs (LOG1c, D94), in the shape the shell's DAORIS.LOG · LINES answer takes: a morning's
// lines across the sources, a period narrowed to errors, a reading that holds more than it shows, and
// nothing at all.

const READING: LogReading = {
  folder: 'C:/somewhere/data/logs',
  lines: [
    { time: '2026-09-30T11:30:00.000Z', source: 'desktop', level: 'error', event: 'page.error', data: { where: 'window', message: "Cannot read properties of undefined (reading 'id')" } },
    { time: '2026-09-30T11:02:10.000Z', source: 'host', level: 'warn', event: 'request.failed', data: { method: 'GET', route: '/api/search', status: 200, ms: 2310 } },
    { time: '2026-09-30T10:41:00.000Z', source: 'desktop', level: 'info', event: 'turn.ended', data: { session: '76cdd5db', stopReason: 'end_turn', turnMs: 41250 } },
    { time: '2026-09-30T10:40:05.000Z', source: 'desktop', level: 'info', event: 'session.opened', data: { session: '76cdd5db', adapter: 'claude-code-acp', openMs: 5840 } },
    { time: '2026-09-30T10:39:59.000Z', source: 'desktop', level: 'info', event: 'refused', data: { code: 'DRIVER_REFUSED', request: 'DAORIS.DRIVER.START_CHAT' } },
    { time: '2026-09-30T09:55:31.204Z', source: 'browser', level: 'error', event: 'error', data: { where: 'an unobserved task', type: 'System.AggregateException', message: 'The remote party closed the WebSocket connection without completing the close handshake.' } },
    { time: '2026-09-30T09:00:00.000Z', source: 'desktop', level: 'info', event: 'app.started', data: { version: '0.0.1', installed: true } },
  ],
  total: 7,
  counts: { info: 4, warn: 1, error: 2 },
  events: ['app.started', 'error', 'page.error', 'refused', 'request.failed', 'session.opened', 'turn.ended'],
  skipped: 0,
};

const meta = {
  title: 'Settings/Logs',
  component: LogList,
  args: { filters: LOG_FILTERS, onFilters: () => {}, onOpenFolder: () => {}, onRefresh: () => {} },
} satisfies Meta<typeof LogList>;

export default meta;
type Story = StoryObj<typeof meta>;

export const AMorning: Story = { args: { reading: READING } };

export const ErrorsOnly: Story = {
  args: {
    filters: { ...LOG_FILTERS, level: 'error' },
    reading: { ...READING, lines: READING.lines.filter((line) => line.level === 'error'), total: 2 },
  },
};

export const MoreThanShown: Story = { args: { reading: { ...READING, total: 1234, skipped: 2 } } };

export const Nothing: Story = {
  args: { reading: { ...READING, lines: [], total: 0, counts: { info: 0, warn: 0, error: 0 }, events: [] } },
};

export const Asking: Story = { args: { reading: undefined } };
