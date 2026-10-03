import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { type LanguageOption, LanguageList, type RepositoryLanguage } from './Languages';

// LANG1c (D142 point 7): the language each workspace's and repository's sessions write to the person in — shown as the driver
// resolved it, with where it was set, named from the driver's own table, and set from here as `daoris driver language` sets it
// from a terminal (D50). The window's language is Appearance's, and nothing here touches it.

const TABLE: LanguageOption[] = [
  { code: 'en', name: 'English' },
  { code: 'zh', name: 'Simplified Chinese (简体中文)' },
];

const LANGUAGES: RepositoryLanguage[] = [
  { repository: 'engine', workspace: 'aurora', language: 'en', name: 'English', source: 'repository' },
  { repository: 'game', workspace: 'aurora', language: 'zh', name: 'Simplified Chinese (简体中文)', source: 'workspace' },
  { repository: 'tools', workspace: 'forge' },
];

const draw = (onSet = vi.fn(), languages = LANGUAGES) => {
  render(
    <LanguageList
      languages={languages}
      workspaceLanguages={[{ workspace: 'aurora', language: 'zh' }]}
      table={TABLE}
      onSet={onSet}
    />,
  );
  return onSet;
};

const choose = async (field: string, option: string) => {
  const user = userEvent.setup();
  screen.getByRole('combobox', { name: field }).focus();
  await user.keyboard('{Enter}');
  await user.click(await screen.findByRole('option', { name: option }));
};

describe('the session language card', () => {
  it('says what each repository\'s sessions write in and where that was set, grouped by its workspace', () => {
    draw();

    const aurora = screen.getByRole('region', { name: 'aurora' });
    expect(within(aurora).getByText('English, set for this repository.')).toBeInTheDocument();
    expect(within(aurora).getByText('Simplified Chinese (简体中文), from this workspace.')).toBeInTheDocument();
    const forge = screen.getByRole('region', { name: 'forge' });
    expect(within(forge).getByText('None: its sessions are asked for no language.')).toBeInTheDocument();
  });

  it('shows what is SET in each field, and a repository that sets none as its workspace\'s', () => {
    draw();

    expect(screen.getByRole('combobox', { name: 'The session language for aurora' })).toHaveTextContent('Simplified Chinese (简体中文)');
    expect(screen.getByRole('combobox', { name: 'The session language for engine' })).toHaveTextContent('English');
    expect(screen.getByRole('combobox', { name: 'The session language for game' })).toHaveTextContent('Its workspace\'s: Simplified Chinese (简体中文)');
    expect(screen.getByRole('combobox', { name: 'The session language for forge' })).toHaveTextContent('Not set');
    expect(screen.getByRole('combobox', { name: 'The session language for tools' })).toHaveTextContent('Not set');
  });

  it('offers the driver\'s table by the names a session is told, and sets a workspace\'s and a repository\'s', async () => {
    const onSet = draw();

    await choose('The session language for forge', 'English');
    expect(onSet).toHaveBeenLastCalledWith({ workspace: 'forge', language: 'en' });

    await choose('The session language for game', 'English');
    expect(onSet).toHaveBeenLastCalledWith({ repository: 'game', language: 'en' });
  });

  it('clears one by choosing its unset option, and sends nothing for what is already chosen', async () => {
    const onSet = draw();

    await choose('The session language for engine', 'Its workspace\'s: Simplified Chinese (简体中文)');
    expect(onSet).toHaveBeenLastCalledWith({ repository: 'engine' });

    await choose('The session language for aurora', 'Not set');
    expect(onSet).toHaveBeenLastCalledWith({ workspace: 'aurora' });
    onSet.mockClear();

    await choose('The session language for game', 'Its workspace\'s: Simplified Chinese (简体中文)');
    expect(onSet).not.toHaveBeenCalled();
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
