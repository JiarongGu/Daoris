import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { MACHINE, type Place } from '../focus';
import type { PlaceRepository } from './focusPlaces';
import { FocusPicker } from './FocusPicker';

/** `docs/2026-11-11-ask-scope-ux-design.md` §1's machine: Games holds engine and tools, Work holds api and site, site with no checkout. */
const REPOSITORIES: PlaceRepository[] = [
  { name: 'tools', workspace: 'Games', checkout: true },
  { name: 'engine', workspace: 'Games', checkout: true },
  { name: 'site', workspace: 'Work', checkout: false },
  { name: 'api', workspace: 'Work', checkout: true },
];

const games: Place = { kind: 'workspace', workspace: 'Games' };
const engine: Place = { kind: 'repository', workspace: 'Games', repository: 'engine' };
const api: Place = { kind: 'repository', workspace: 'Work', repository: 'api' };

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

const draw = (props: Partial<Parameters<typeof FocusPicker>[0]> = {}) => {
  const onChoose = vi.fn();
  render(
    <FocusPicker
      focus={games}
      next={null}
      window="Games"
      repositories={REPOSITORIES}
      recent={[engine]}
      onChoose={onChoose}
      {...props}
    />,
  );
  return onChoose;
};

const chip = () => screen.getByRole('button', { name: /focus|聚焦/ });
const text = (row: Element) => (row.textContent ?? '').replace(/\s+/g, ' ').trim();
const rows = () => within(screen.getByRole('listbox')).getAllByRole('option').map(text);
const lit = () => text(screen.getAllByRole('option').find((row) => row.getAttribute('aria-selected') === 'true')!);

describe('the focus chip and its picker', () => {
  it('says the window’s workspace in words and in its label, and opens upward on the window first, Recent, this machine, the others', async () => {
    draw();
    expect(chip().textContent).toContain('Games · this window');
    expect(chip()).toHaveAccessibleName('Ask Daoris’s focus: workspace Games, this window’s');

    await userEvent.click(chip());
    const field = screen.getByRole('combobox', { name: 'Find a workspace or repository' });
    expect(field).toHaveFocus();
    expect(field.closest('div')!.className).toContain('bottom-full');
    expect(rows()).toEqual(['Games this window', 'engine Games', 'This machine every workspace', 'Work 2 repositories']);
    expect(screen.getByRole('group', { name: 'Recent' })).toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'Other workspaces' })).toBeInTheDocument();
    expect(screen.getByText('Focus only: Ask Daoris still looks in every workspace.')).toBeInTheDocument();
  });

  it('opens on Enter with the current focus active, unfolds at the caret’s end, folds back at its start, and chooses with Enter', async () => {
    const onChoose = draw({ focus: engine });
    chip().focus();
    await userEvent.keyboard('{Enter}');
    // A repository focus opens its workspace, with the repository active.
    expect(rows()).toContain('engine');
    expect(lit()).toBe('engine');

    await userEvent.keyboard('{ArrowLeft}');
    expect(lit()).toBe('Games this window');
    await userEvent.keyboard('{ArrowLeft}');
    expect(rows()).not.toContain('engine');

    await userEvent.keyboard('{ArrowUp}');
    expect(lit()).toBe('Work 2 repositories');
    await userEvent.keyboard('{ArrowRight}');
    expect(rows()).toEqual(expect.arrayContaining(['api', 'site no checkout on this machine']));
    await userEvent.keyboard('{ArrowDown}{Enter}');
    expect(onChoose).toHaveBeenCalledWith(api);
    expect(screen.queryByRole('listbox')).toBeNull();
    expect(chip()).toHaveFocus();
  });

  it('flattens a search into one list with no place twice, and says when nothing matches', async () => {
    draw({ recent: [engine, api] });
    await userEvent.click(chip());
    await userEvent.keyboard('eng');
    expect(rows()).toEqual(['engine Games']);
    await userEvent.clear(screen.getByRole('combobox'));
    await userEvent.keyboard('work');
    expect(rows()).toEqual(['api Work', 'Work', 'site Work no checkout on this machine']);
    await userEvent.keyboard('zzz');
    expect(screen.getByText('Nothing here matches “workzzz”.')).toBeInTheDocument();
  });

  it('tells two repositories of one name apart by the workspace beside each', async () => {
    draw({ repositories: [...REPOSITORIES, { name: 'api', workspace: 'Games', checkout: true }], recent: [] });
    await userEvent.click(chip());
    await userEvent.keyboard('api');
    expect(rows()).toEqual(['api Games', 'api Work']);
  });

  it('offers no workspace level on a machine whose only workspace is default', async () => {
    draw({ focus: MACHINE, window: 'default', repositories: [{ name: 'engine', workspace: 'default', checkout: true }], recent: [] });
    expect(chip()).toHaveAccessibleName('Ask Daoris’s focus: this machine');
    await userEvent.click(chip());
    expect(rows()).toEqual(['This machine this window', 'engine']);
  });

  it('keeps the focus as it is from a change waiting, and says the change in the chip', async () => {
    const onChoose = draw({ next: engine });
    expect(chip().textContent).toContain('Games · engine next message');
    await userEvent.click(chip());
    expect(screen.getByText('engine applies with your next message.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('option', { name: /Keep Games/ }));
    expect(onChoose).toHaveBeenCalledWith(games);
  });

  it('is disabled while this machine’s workspaces are read', () => {
    draw({ repositories: undefined });
    expect(screen.getByRole('button', { name: /focus/ })).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent('Reading this machine’s workspaces…');
  });

  it('speaks Chinese', async () => {
    await i18n.changeLanguage('zh');
    draw({ next: engine });
    expect(chip().textContent).toContain('Games · 下一条消息起 engine');
    await userEvent.click(chip());
    expect(screen.getByRole('combobox', { name: '查找工作区或仓库' })).toBeInTheDocument();
    expect(screen.getByText('engine 从你的下一条消息起生效。')).toBeInTheDocument();
    expect(screen.getByText('只是聚焦：问道衍仍会查看每个工作区。')).toBeInTheDocument();
    expect(rows()).toContain('Work 2 个仓库');
  });
});
