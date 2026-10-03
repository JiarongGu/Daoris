import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import { SessionLanguage } from './SessionLanguage';

// LANG1c (D142 point 7): a repository's session language on its page, beside its standing answer — the language its sessions
// write to the person in, as the driver resolved it, with where it was set; chosen from the driver's own table. The window's
// language is Appearance's, and this never touches it.

const TABLE = [
  { code: 'en', name: 'English' },
  { code: 'zh', name: 'Simplified Chinese (简体中文)' },
];

const choose = async (option: string) => {
  const user = userEvent.setup();
  screen.getByRole('combobox', { name: 'The session language for engine' }).focus();
  await user.keyboard('{Enter}');
  await user.click(await screen.findByRole('option', { name: option }));
};

describe("a repository's session language", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says what its sessions write in when it sets its own, and offers the table by the names a session is told', async () => {
    const onSet = vi.fn();
    render(<SessionLanguage repository="engine" language={{ repository: 'engine', workspace: 'aurora', language: 'zh', name: 'Simplified Chinese (简体中文)', source: 'repository' }} table={TABLE} onSet={onSet} />);

    expect(screen.getByText('Its sessions write to you in Simplified Chinese (简体中文), set for this repository.')).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'The session language for engine' })).toHaveTextContent('Simplified Chinese (简体中文)');
    await choose('English');
    expect(onSet).toHaveBeenLastCalledWith('en');
    await choose('Not set');
    expect(onSet).toHaveBeenLastCalledWith(null);
  });

  it("says when it takes its workspace's, which its unset choice names", () => {
    render(<SessionLanguage repository="engine" language={{ repository: 'engine', workspace: 'aurora', language: 'en', name: 'English', source: 'workspace' }} table={TABLE} onSet={vi.fn()} />);

    expect(screen.getByText('Its sessions write to you in English, from the workspace aurora.')).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'The session language for engine' })).toHaveTextContent("Its workspace's: English");
  });

  it('says when none is set, here or for its workspace, and that the window keeps its own', () => {
    render(<SessionLanguage repository="engine" language={null} table={TABLE} onSet={vi.fn()} />);

    expect(screen.getByText('None is set here or for its workspace: its sessions are asked for no language.')).toBeInTheDocument();
    expect(screen.getByText(/The window's own language is in Settings → Appearance/)).toBeInTheDocument();
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<SessionLanguage repository="engine" language={null} table={TABLE} onSet={vi.fn()} />);

    expect(screen.getByText('会话语言')).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'engine 的会话语言' })).toHaveTextContent('未设定');
  });
});
