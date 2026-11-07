import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { RelatedSessions, type RelatedRow } from './RelatedSessions';

// The sessions beside one: two on one file, a declared overlap, and one git could not read.

const row = (over: Partial<RelatedRow> & { id: string }): RelatedRow => ({
  kind: 'driven', quest: null, read: 'read', readAt: null, declaredOverlap: [], changedOverlap: [], ...over,
});

const meta = {
  title: 'Work/RelatedSessions',
  component: RelatedSessions,
  args: {
    repository: 'Lyntai',
    onSession: () => {},
    related: [
      row({ id: 'a1b2c3d4', changedOverlap: ['TASKS.md', 'docs/task-archive.md'] }),
      row({ id: 'e5f6a7b8', kind: 'chat', changedOverlap: ['TASKS.md'] }),
      row({ id: 'c9d0e1f2', declaredOverlap: ['src/Daoris.Web/src/work/WorkFrame.tsx'] }),
      row({ id: '0a1b2c3d', read: 'not-read', readAt: '2026-10-31T10:00:00Z' }),
    ],
  },
} satisfies Meta<typeof RelatedSessions>;

export default meta;
type Story = StoryObj<typeof meta>;

export const UnreadFolded: Story = {
  args: {
    related: [
      row({ id: 'a1b2c3d4', changedOverlap: ['TASKS.md'] }),
      row({ id: '0a1b2c3d', read: 'not-read', readAt: '2026-10-31T10:00:00Z' }),
      row({ id: '1b2c3d4e', read: 'not-read', readAt: '2026-10-31T09:24:00Z' }),
      row({ id: '2c3d4e5f', read: 'not-read', readAt: '2026-10-31T09:24:00Z' }),
    ],
  },
};
export const OwnNotRead: Story = { args: { related: [], own: { readAt: '2026-10-31T09:24:00Z' } } };
export const Wide: Story = { decorators: [storyWidths.wide] };
export const Narrow: Story = { decorators: [storyWidths.narrow] };
export const NarrowChinese: Story = { decorators: [chinese, storyWidths.narrow] };
