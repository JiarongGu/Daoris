import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { type LanguageOption, LanguageList, type RepositoryLanguage } from './Languages';

// LANG1c (D142 point 7): the language each workspace's sessions write to the person in, named from the driver's own table,
// and set from here as `daoris driver language --workspace` sets it from a terminal (D50). The window's language is
// Appearance's, and nothing here touches it. Since UX6f (D150 §3.1) a repository's own is set once, on its page under
// Setup: the card names the repositories that set their own, each a door there.

const TABLE: LanguageOption[] = [
  { code: 'en', name: 'English' },
  { code: 'zh', name: 'Simplified Chinese (简体中文)' },
];

const LANGUAGES: RepositoryLanguage[] = [
  { repository: 'engine', workspace: 'aurora', language: 'en', name: 'English', source: 'repository' },
  { repository: 'game', workspace: 'aurora', language: 'zh', name: 'Simplified Chinese (简体中文)', source: 'workspace' },
  { repository: 'tools', workspace: 'forge' },
];

const draw = (onSet = vi.fn(), languages = LANGUAGES, onOpen = vi.fn()) => {
  render(
    <Tooltip.Provider>
      <LanguageList
        languages={languages}
        workspaceLanguages={[{ workspace: 'aurora', language: 'zh' }]}
        table={TABLE}
        onSet={onSet}
        onOpen={onOpen}
      />
    </Tooltip.Provider>,
  );
  return { onSet, onOpen };
};

const choose = async (field: string, option: string) => {
  const user = userEvent.setup();
  screen.getByRole('combobox', { name: field }).focus();
  await user.keyboard('{Enter}');
  await user.click(await screen.findByRole('option', { name: option }));
};

describe('the session language card', () => {
  it('shows what is SET in each workspace\'s field', () => {
    draw();

    expect(screen.getByRole('combobox', { name: 'The session language for aurora' })).toHaveTextContent('Simplified Chinese (简体中文)');
    expect(screen.getByRole('combobox', { name: 'The session language for forge' })).toHaveTextContent('Not set');
  });

  it('offers the driver\'s table by the names a session is told, sets a workspace\'s, and clears one', async () => {
    const { onSet } = draw();

    await choose('The session language for forge', 'English');
    expect(onSet).toHaveBeenLastCalledWith({ workspace: 'forge', language: 'en' });

    await choose('The session language for aurora', 'Not set');
    expect(onSet).toHaveBeenLastCalledWith({ workspace: 'aurora' });
    onSet.mockClear();

    await choose('The session language for forge', 'Not set');
    expect(onSet).not.toHaveBeenCalled();
  });

  // UX6f: a repository's session language was set in two places; it has one now, its Setup.
  it('keeps no row per repository, and names those that set their own, each a door to its Setup', async () => {
    const { onOpen } = draw();

    expect(screen.queryByRole('combobox', { name: 'The session language for engine' })).toBeNull();
    expect(screen.queryByRole('combobox', { name: 'The session language for game' })).toBeNull();
    const aurora = screen.getByRole('region', { name: 'aurora' });
    expect(within(aurora).getAllByRole('button', { name: /'s setup$/ }).map((door) => door.textContent)).toEqual(['engine']);
    await userEvent.click(within(aurora).getByRole('button', { name: "Open engine's setup" }));
    expect(onOpen).toHaveBeenLastCalledWith('engine');
    expect(within(screen.getByRole('region', { name: 'forge' })).getByText(/No repository here sets its own/)).toBeInTheDocument();
  });

  it('names a machine with no workspace to set one for', () => {
    render(<LanguageList languages={[]} workspaceLanguages={[]} table={TABLE} onSet={vi.fn()} />);

    expect(screen.getByText('No repository on this machine to set a session language for.')).toBeInTheDocument();
  });

  it('shows a workspace that sets its own even with no repository here', () => {
    draw(vi.fn(), []);

    expect(screen.getByRole('region', { name: 'aurora' })).toBeInTheDocument();
  });
});
