import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Session } from '../../data/api';
import { type SessionHit } from '../../bridge/shell';
import { SearchResults } from './SessionRail';

// What the rail's search found in what was said (RAIL1), read a page of sessions at a time (RAILPAGE1b, UX §4): the foot's count
// line and *Show more*, the page on its way, a page that could not be read, the last page, and a shell before pages. The
// results are reached by passing them: no driver, no service.

const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const SESSIONS: Session[] = ['a1b2c3d4', 'b2c3d4e5', 'c3d4e5f6'].map((id, index) => ({
  id, repository: 'engine', adapter: 'claude-code', kind: 'chat', state: 'completed', created: at(300), updated: at(10 + index),
}));

const hits: SessionHit[] = SESSIONS.map((session, index) => ({
  session: session.id, seq: index + 1, kind: 'message', snippet: `…the streamer of ${session.id} reads its budget.`,
}));

const meta: Meta<typeof SearchResults> = {
  title: 'Work/SessionRail/Search',
  component: SearchResults,
  args: {
    query: 'streamer',
    settled: 'streamer',
    named: [],
    hits,
    cut: false,
    total: 3,
    covered: 3,
    asking: false,
    sessionOf: (id) => SESSIONS.find((session) => session.id === id),
    titleOf: (session) => `Session ${session.id}`,
    row: () => null,
    selected: null,
  },
  decorators: [(Story) => <div className="w-[280px]"><Story /></div>],
};
export default meta;
type Story = StoryObj<typeof SearchResults>;

/** More sessions matched than one page holds: the count and *Show more*. */
export const Paged: Story = { args: { total: 40, covered: 3, onMore: () => {} } };
export const LoadingMore: Story = { args: { total: 40, covered: 3, onMore: () => {}, loadingMore: true } };
export const MoreFailed: Story = { args: { total: 40, covered: 3, onMore: () => {}, moreError: 'the driver stopped answering.' } };
/** The last page: no foot, and so no count. */
export const LastPage: Story = { args: { total: 3, covered: 3 } };
/** A shell before pages answers no total: it says it left some out, and offers nothing more. */
export const OlderShell: Story = { args: { total: null, cut: true } };
