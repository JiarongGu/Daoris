import type { Meta, StoryObj } from '@storybook/react-vite';
import i18n from '../i18n';
import type { MenuAct } from '../ui';
import { actMenu } from '../work/acts';
import { ContextMenu } from './ContextMenu';

// The right-click menu (CTX1, D138): MENU1's menu at a point, its groups between rules, as each press is on — a session's
// row, a quest's page, selected words, a link on a shell and in a browser, a code span, a link on a page, and in 中文. A
// molecule, so each is reached by passing it; what a press is on is `press.ts`'s rule and held by its tests.

const t = (key: string, lng = 'en') => i18n.t(key, { lng });
const nothing = () => {};

const selection = (lng = 'en'): MenuAct[] => [
  { id: 'copy', label: t('contextMenu.act.copy', lng), icon: 'copy', copy: 'a per-frame budget' },
  { id: 'search', label: t('contextMenu.act.search', lng), icon: 'search', onSelect: nothing },
  { id: 'ask', label: t('contextMenu.act.ask', lng), icon: 'help', onSelect: nothing },
];

const link = (shell = true, lng = 'en'): MenuAct[] => [
  { id: 'open', label: t('contextMenu.act.open', lng), icon: 'external', onSelect: nothing },
  ...(shell ? [{ id: 'openInBrowser', label: t('contextMenu.act.openInBrowser', lng), icon: 'browser' as const, onSelect: nothing }] : []),
  { id: 'copyLink', label: t('contextMenu.act.copyLink', lng), icon: 'link', copy: 'https://tickets.example/T-1' },
];

const quest = (lng = 'en'): MenuAct[] => [
  { id: 'take', label: t('quests.detail.take', lng), onSelect: nothing },
  { id: 'done', label: t('quests.detail.done', lng), icon: 'check', onSelect: nothing },
  { id: 'pause', label: t('quests.detail.pause', lng), icon: 'pause', onSelect: nothing },
  { id: 'decline', label: t('quests.detail.decline', lng), onSelect: nothing },
  { id: 'abandon', label: t('quests.detail.abandon', lng), onSelect: nothing },
  { id: 'copy', label: t('contextMenu.act.copyQuest', lng), icon: 'copy', copy: 'abc123' },
];

const meta: Meta<typeof ContextMenu> = {
  title: 'Menus/ContextMenu',
  component: ContextMenu,
  args: { at: { x: 160, y: 72 }, label: t('contextMenu.label'), onClose: nothing, onCopy: nothing },
  // A page behind it, the size of a main area, so the menu sits where a press on it would put it.
  decorators: [(Story) => (
    <div className="h-[26rem] w-[44rem] max-w-full border border-line bg-page p-6 text-body text-ink-soft">
      <p className="m-0">The page a right-click was made on.</p>
      <Story />
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof ContextMenu>;

/** A session's row: its ⋯'s acts, the one list (`actMenu`), named for the session. */
export const ASessionsRow: Story = {
  args: {
    label: i18n.t('contextMenu.for', { name: 'Cap the hydration per frame' }),
    sections: [actMenu(['stop', 'pauseQuest', 'review', 'openFolder', 'terminal', 'detach', 'copy'], (key) => t(key), nothing)],
  },
};

/** A quest's page: its header's acts, then its id. */
export const AQuestsPage: Story = {
  args: { label: i18n.t('contextMenu.for', { name: 'Expose a streaming budget' }), sections: [quest()] },
};

/** Selected words, nowhere a surface offers: Copy, Search Daoris for it, Ask Daoris about it. */
export const SelectedWords: Story = { args: { sections: [selection()] } };

/** A link on a shell: Open, Open in Daoris's browser, Copy link. */
export const ALink: Story = { args: { sections: [link()] } };

/** A link in a browser, or a sign-in's: no Daoris's browser to offer. */
export const ALinkInABrowser: Story = { args: { sections: [link(false)] } };

/** A code span or a path: Copy. */
export const ACodeSpan: Story = {
  args: { sections: [[{ id: 'copyCode', label: t('contextMenu.act.copy'), icon: 'copy', copy: 'daoris sync' }]] },
};

/** A link on a quest's page: the link's group, a rule, then the quest's acts. */
export const ALinkOnAPage: Story = {
  args: { label: i18n.t('contextMenu.for', { name: 'Expose a streaming budget' }), sections: [link(), quest()] },
};

/** Opened by its key: the first act has the focus. */
export const OpenedByItsKey: Story = { args: { sections: [quest()], keyed: true } };

/** 中文: the same groups in the reader's language. */
export const Chinese: Story = {
  args: {
    label: i18n.t('contextMenu.for', { lng: 'zh', name: '暴露流式预算' }),
    sections: [selection('zh'), link(true, 'zh'), quest('zh')],
  },
};
