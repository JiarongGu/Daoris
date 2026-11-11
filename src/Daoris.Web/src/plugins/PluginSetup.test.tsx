import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import { interfacesOf, type InterfacesShown, switchedOn, type SwitchWord } from './interfaces';
import { PluginSetup } from './PluginSetup';

// A plugin's Set up tab: its commands with Run, an entry refused alone drawn disabled with why, its storage and its sign-out at
// removal, its views, the parts this build does not know, and Where it is on with a box per part and place.

const INTERFACES: InterfacesShown = interfacesOf({
  commands: [
    { name: 'login', title: { en: 'Sign in', zh: '登录' }, about: { en: 'Signs in by a device code.', zh: '用设备代码登录。' }, interactive: true },
    { name: 'status', title: 'Whether it is signed in', about: 'Says who it is signed in as.', tool: true },
    { name: 'logout', about: 'Signs out and forgets the token.', writes: true },
  ],
  storage: { for: 'The token its sign-in keeps.', forget: 'logout' },
  views: [{ id: 'pull-requests', title: 'Pull requests', where: ['side', 'page'], places: ['repository'], context: ['session'] }],
  refused: [{ interface: 'commands', entry: 'deploy', problem: '`deploy`: a tool command never asks the person.' }],
  unknown: ['status-items'],
});

function setup(over: Partial<Parameters<typeof PluginSetup>[0]> = {}) {
  const onRun = vi.fn();
  const onSwitch = vi.fn();
  render(
    <PluginSetup
      id="acme-boards" enabled refused={false} interfaces={INTERFACES} points={['quest/consider']} switches={[]} place="machine"
      places={[{ key: 'workspace:home' }, { key: 'repository:engine', workspace: 'home' }]} onRun={onRun} onSwitch={onSwitch} {...over}
    />,
  );
  return { onRun, onSwitch };
}

const section = (name: string) => screen.getByRole('heading', { name }).closest('section')!;

describe("a plugin's Set up tab", () => {
  it('lists each command by its title, its name, its marks and its words, and Run hands the command on', async () => {
    const { onRun } = setup();
    const commands = within(section('Commands'));
    expect(commands.getByText('Sign in')).toBeInTheDocument();
    expect(commands.getByText('login')).toBeInTheDocument();
    expect(commands.getByText('asks you')).toBeInTheDocument();
    expect(commands.getByText('for agents')).toBeInTheDocument();
    expect(commands.getByText('changes things')).toBeInTheDocument();
    // A command that writes no title is shown by its name.
    expect(commands.getAllByText('logout')).toHaveLength(2);
    await userEvent.click(commands.getByRole('button', { name: 'Run Sign in' }));
    expect(onRun).toHaveBeenCalledWith(expect.objectContaining({ name: 'login', interactive: true }));
  });

  it('draws an entry refused alone with its name, not read and why, and nothing to press', () => {
    setup();
    const row = within(section('Commands')).getByText('not read').closest('li')!;
    expect(within(row).getAllByText('deploy').length).toBeGreaterThan(0);
    expect(row).toHaveAttribute('aria-disabled', 'true');
    expect(within(row).getByText('not read')).toBeInTheDocument();
    expect(within(row).getByText(/never asks the person/)).toBeInTheDocument();
    expect(within(row).queryByRole('button')).toBeNull();
    expect(screen.getByText(/status-items/)).toBeInTheDocument();
  });

  it('disables Run with the reason where its commands are switched off at the place in force, or the plugin is off', () => {
    setup({ switches: [{ what: 'commands', place: 'workspace:home', on: false }], place: 'workspace:home' });
    expect(screen.getByText('Its commands are switched off at Workspace home.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Run Sign in' })).toBeDisabled();
  });

  it('says what its storage keeps and that removing it signs it out first, or why it does not', () => {
    setup();
    const storage = within(section('Storage'));
    expect(storage.getByText('The token its sign-in keeps.')).toBeInTheDocument();
    expect(storage.getByText(/asks it to sign out first, by/)).toBeInTheDocument();
  });

  it('switches a part at a place: the machine column first, a place with a word beside it, and storage at the machine alone', async () => {
    const switches: SwitchWord[] = [{ what: 'tools', place: 'workspace:home', on: false }];
    const { onSwitch } = setup({ switches });
    const table = within(section('Where it is on')).getByRole('table');
    expect(within(table).getAllByRole('columnheader').map((cell) => cell.textContent)).toEqual(['Where it is on', 'This machine', 'Workspace home']);
    expect(within(table).getByRole('checkbox', { name: 'Commands for agents at This machine' })).toBeChecked();
    expect(within(table).getByRole('checkbox', { name: 'Commands for agents at Workspace home' })).not.toBeChecked();
    expect(within(table).queryByRole('checkbox', { name: 'Storage at Workspace home' })).toBeNull();
    expect(within(table).getByRole('checkbox', { name: 'quest/consider at This machine' })).toBeChecked();
    await userEvent.click(within(table).getByRole('checkbox', { name: 'Commands at This machine' }));
    expect(onSwitch).toHaveBeenCalledWith('commands', 'machine', false);
  });

  it('reads the narrowest word, as the driver does, and storage at the machine alone', () => {
    const words: SwitchWord[] = [
      { what: 'commands', place: 'machine', on: false },
      { what: 'commands', place: 'workspace:home', on: true },
      { what: 'storage', place: 'workspace:home', on: false },
    ];
    expect(switchedOn(words, 'commands', 'machine')).toBe(false);
    expect(switchedOn(words, 'commands', 'repository:engine', 'home')).toBe(true);
    expect(switchedOn(words, 'commands', 'repository:other', 'away')).toBe(false);
    expect(switchedOn(words, 'storage', 'workspace:home')).toBe(true);
    expect(switchedOn([], 'views', 'machine')).toBe(true);
  });

  it('shows the authored words in the viewer\'s language', async () => {
    await i18n.changeLanguage('zh');
    try {
      setup();
      expect(screen.getByText('登录')).toBeInTheDocument();
      expect(screen.getByText('用设备代码登录。')).toBeInTheDocument();
      expect(screen.getByRole('heading', { name: '在哪里启用' })).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
