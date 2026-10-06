import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import '../i18n';
import i18n from '../i18n';
import { type CommandState, commandTable, shortcutGroups } from '../commands';
import { KeyboardShortcuts } from './KeyboardShortcuts';

// Help › Keyboard shortcuts (UX7a, D152 §3.2): the table's keys, by menu, in the reader's language.

const state = (over: Partial<CommandState> = {}): CommandState => ({
  attached: true, view: 'quests', list: { shown: true }, panelShown: true, sideShown: false, moved: false, workspaces: [],
  scope: null, circle: null, wired: false, theme: 'system', language: 'en', agents: [], domains: [], knowledge: 'search',
  session: null, quest: null, record: false, find: false, field: false, refreshing: false, ...over,
});
const doors = new Proxy({}, { get: () => vi.fn() }) as never;
const groups = (over: Partial<CommandState> = {}) => {
  const t = i18n.t.bind(i18n);
  return shortcutGroups(commandTable(state(over), doors, t), t);
};

describe('Keyboard shortcuts', () => {
  it('lists every key the menus print, by menu, and how the bar is reached', () => {
    render(<KeyboardShortcuts groups={groups()} onClose={() => {}} />);

    const workspace = within(screen.getByRole('region', { name: 'Workspace' }));
    expect(workspace.getByText('New ask…')).toBeInTheDocument();
    expect(workspace.getByText('Ctrl+N')).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'Go' })).getByText('Ctrl+8')).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'Help' })).getByText('Ctrl+Alt+I')).toBeInTheDocument();
    expect(screen.getAllByRole('region')).toHaveLength(7);
    expect(screen.getByText(/Alt, pressed and let go, or F10/)).toBeInTheDocument();
  });

  it('lists in a browser only the keys a browser leaves the page, and says so', () => {
    render(<KeyboardShortcuts groups={groups({ attached: false })} browser onClose={() => {}} />);

    expect(screen.queryByText('Ctrl+N')).toBeNull();
    expect(screen.queryByText('Ctrl+3')).toBeNull();
    expect(screen.queryByRole('region', { name: 'Terminal' })).toBeNull();
    expect(screen.getByText('Ctrl+,')).toBeInTheDocument();
    expect(screen.queryByText(/F10/)).toBeNull();
    expect(screen.getByText(/In a browser/)).toBeInTheDocument();
  });

  it('speaks the reader\'s language', async () => {
    await i18n.changeLanguage('zh');
    try {
      render(<KeyboardShortcuts groups={groups()} onClose={() => {}} />);
      expect(screen.getByRole('dialog', { name: '键盘快捷键' })).toBeInTheDocument();
      expect(within(screen.getByRole('region', { name: '转到' })).getByText('委托')).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
