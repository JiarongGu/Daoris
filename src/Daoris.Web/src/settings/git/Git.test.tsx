import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import '../../locales/i18n';
import { GitDefaults, GitIdentities, GitIdentityRow, GitOwn, type GitActs } from './Git';
import { GitAdd } from './GitAdd';
import { GitReads } from './GitReads';
import { ACTS, DEPLOY, EMPTY_PAGE, PAGE, PERSONAL, READS, UNKNOWN_HOST, WORK } from './gitFixtures';

// Settings → Git: the organisms drawn from props. GitDomain holds the bridge.

const acts = (over: Partial<GitActs> = {}): GitActs => ({ ...ACTS, ...Object.fromEntries(Object.entries(ACTS).map(([name, act]) => [name, vi.fn(act)])), ...over });

const draw = (node: React.ReactNode) => render(<Tooltip.Provider>{node}</Tooltip.Provider>);

describe('an identity\'s row', () => {
  it('says the host, the way, the key, who it commits as, the test and what points at it', () => {
    draw(<ul><GitIdentityRow identity={WORK} acts={acts()} /></ul>);

    const row = screen.getByRole('listitem');
    expect(within(row).getByText('Work')).toBeInTheDocument();
    expect(within(row).getByText('Default for github.com')).toBeInTheDocument();
    expect(within(row).getByText(/github\.com · SSH ed25519 SHA256:q3Vf/)).toBeInTheDocument();
    expect(within(row).getByText('commits as Work Name <work@example.invalid>')).toBeInTheDocument();
    expect(within(row).getByText(/reached as octo-work/)).toBeInTheDocument();
    expect(within(row).getByText(/repositories: 12 · workspaces: 0/)).toBeInTheDocument();
  });

  it('offers the public key and the key page to an SSH identity and neither to an HTTPS one', () => {
    const { unmount } = draw(<ul><GitIdentityRow identity={WORK} acts={acts()} /></ul>);
    expect(screen.getByRole('button', { name: 'Copy public key' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: "Open github.com's key page" })).toHaveAttribute('href', 'https://github.com/settings/keys');
    unmount();

    draw(<ul><GitIdentityRow identity={PERSONAL} acts={acts()} /></ul>);
    expect(screen.queryByRole('button', { name: 'Copy public key' })).toBeNull();
    expect(screen.getByText(/signed in with a token/)).toBeInTheDocument();
  });

  it('says a refusal with the act that fixes it, and tests again', async () => {
    const test = vi.fn(async () => ({ result: 'refused', hostKeys: [] }));
    draw(<ul><GitIdentityRow identity={DEPLOY} acts={acts({ test })} /></ul>);

    expect(screen.getByText('The host does not take this identity.')).toBeInTheDocument();
    expect(screen.getByText('read only')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Test again' }));

    expect(test).toHaveBeenCalledWith(DEPLOY, {});
  });

  it('tests an HTTPS identity against a repository the person names, and not before', async () => {
    const test = vi.fn(async () => ({ result: 'reached', user: 'octo-me', hostKeys: [] }));
    draw(<ul><GitIdentityRow identity={PERSONAL} acts={acts({ test })} /></ul>);

    expect(screen.getByRole('button', { name: 'Test' })).toBeDisabled();
    await userEvent.type(screen.getByLabelText('A repository to test Personal against'), 'https://github.com/org-a/engine');
    await userEvent.click(screen.getByRole('button', { name: 'Test' }));

    expect(test).toHaveBeenCalledWith(PERSONAL, { repository: 'https://github.com/org-a/engine' });
  });

  it('shows a host\'s keys against the published ones, and trusts the one the person presses', async () => {
    const test = vi.fn()
      .mockResolvedValueOnce(UNKNOWN_HOST)
      .mockResolvedValueOnce({ result: 'reached', user: 'octo-work', hostKeys: [] });
    draw(<ul><GitIdentityRow identity={WORK} acts={acts({ test })} /></ul>);

    await userEvent.click(screen.getByRole('button', { name: 'Test' }));

    const group = await screen.findByRole('group', { name: 'Trust github.com' });
    expect(within(group).getByText('matches the key the host publishes')).toBeInTheDocument();
    expect(within(group).getByText('NOT a key the host publishes')).toBeInTheDocument();
    await userEvent.click(within(group).getAllByRole('button', { name: 'Trust this host' })[0]!);

    expect(test).toHaveBeenLastCalledWith(WORK, { trust: 'SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU' });
    await waitFor(() => expect(screen.queryByRole('group', { name: 'Trust github.com' })).toBeNull());
  });

  it('says a refusal under the press, keeping the form open', async () => {
    const rename = vi.fn().mockRejectedValue(new Error('an identity is already named Personal'));
    draw(<ul><GitIdentityRow identity={WORK} acts={acts({ rename })} /></ul>);

    await userEvent.click(screen.getByRole('button', { name: 'Rename…' }));
    const field = screen.getByLabelText('A new name for Work');
    await userEvent.clear(field);
    await userEvent.type(field, 'Personal');
    await userEvent.click(screen.getByRole('button', { name: 'Rename' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('an identity is already named Personal');
    expect(screen.getByLabelText('A new name for Work')).toHaveValue('Personal');
  });

  it('confirms allowing writes again as a widening, and sends nothing before the second press', async () => {
    const readOnly = vi.fn(async () => ({}));
    draw(<ul><GitIdentityRow identity={DEPLOY} acts={acts({ readOnly })} /></ul>);

    await userEvent.click(screen.getByRole('button', { name: 'Allow writes again' }));
    expect(readOnly).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Allow writes' }));

    expect(readOnly).toHaveBeenCalledWith(DEPLOY, false);
  });

  it('sets an API token from a password field that is cleared once sent, never showing it back, and clears it', async () => {
    const apiToken = vi.fn(async () => ({}));
    draw(<ul><GitIdentityRow identity={{ ...WORK, apiToken: true }} acts={acts({ apiToken })} /></ul>);

    await userEvent.click(screen.getByRole('button', { name: 'Change API token…' }));
    const field = screen.getByLabelText('The API token of Work');
    expect(field).toHaveAttribute('type', 'password');
    expect(field).toHaveValue('');
    await userEvent.type(field, 'throwaway-api-token');
    await userEvent.click(screen.getByRole('button', { name: 'Save token' }));
    expect(apiToken).toHaveBeenCalledWith(expect.objectContaining({ id: WORK.id }), 'throwaway-api-token');
    expect(document.body.textContent).not.toContain('throwaway-api-token');

    await userEvent.click(screen.getByRole('button', { name: 'Change API token…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Clear token' }));
    expect(apiToken).toHaveBeenLastCalledWith(expect.objectContaining({ id: WORK.id }), null);
  });

  it('lists every scope pointing at an identity before it is removed, and removes on the second press', async () => {
    const remove = vi.fn(async () => ({}));
    draw(<ul><GitIdentityRow identity={WORK} acts={acts({ remove })} /></ul>);

    await userEvent.click(screen.getByRole('button', { name: 'Remove…' }));
    expect(screen.getByText("engine's origin")).toBeInTheDocument();
    expect(screen.getByText("workspace games's github.com")).toBeInTheDocument();
    expect(remove).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Remove' }));

    expect(remove).toHaveBeenCalledWith(WORK);
  });
});

describe('the identities card', () => {
  it('says what an empty set means and offers the add press', async () => {
    const onAdd = vi.fn();
    draw(<GitIdentities identities={EMPTY_PAGE.identities} acts={acts()} adding={false} onAdd={onAdd} />);

    expect(screen.getByText('No git identities yet')).toBeInTheDocument();
    await userEvent.click(screen.getAllByRole('button', { name: 'Add a git identity…' })[0]!);
    expect(onAdd).toHaveBeenCalled();
  });
});

describe('the defaults card', () => {
  it('has a row per host and way, and changing one sets that identity\'s default', async () => {
    const makeDefault = vi.fn(async () => ({}));
    const other = { ...WORK, id: 'gi-00000000000e', label: 'Side', default: false };
    draw(<GitDefaults identities={[WORK, other, PERSONAL]} acts={acts({ makeDefault })} onError={vi.fn()} />);

    expect(screen.getByText('github.com · SSH')).toBeInTheDocument();
    expect(screen.getByText('github.com · HTTPS')).toBeInTheDocument();
    // A select opens from the keyboard in this document, as the Tools card's does.
    screen.getByRole('combobox', { name: 'The default SSH key identity for github.com' }).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('option', { name: 'Side' }));

    expect(makeDefault).toHaveBeenCalledWith(other);
  });
});

describe('your own git sign-ins', () => {
  it('names the hosts it is used for and stops one', async () => {
    const own = vi.fn(async () => ({}));
    draw(<GitOwn hosts={['gitlab.com']} knownHosts={PAGE.hosts} acts={acts({ own })} />);

    expect(screen.getByText('gitlab.com')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Stop using' }));

    expect(own).toHaveBeenCalledWith('gitlab.com', false);
  });

  it('is a widening: choosing a host names what it lets Daoris\'s git use, and writes nothing until confirmed', async () => {
    const own = vi.fn(async () => ({}));
    draw(<GitOwn hosts={[]} knownHosts={PAGE.hosts} acts={acts({ own })} />);

    await userEvent.click(screen.getByRole('button', { name: 'Use for…' }));
    await userEvent.type(screen.getByLabelText('Host'), 'github.com');
    expect(screen.getByText(/your own SSH keys, git configuration and stored sign-ins for github\.com/)).toBeInTheDocument();
    expect(own).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Use my own sign-ins' }));

    expect(own).toHaveBeenCalledWith('github.com', true);
  });
});

describe('the add form', () => {
  it('generates an SSH key and then gives the public key to copy, with the host\'s key page', async () => {
    const add = vi.fn(ACTS.add);
    const copy = vi.fn();
    draw(<GitAdd acts={acts({ add, copy })} knownHosts={PAGE.hosts} onClose={vi.fn()} />);

    await userEvent.type(screen.getByLabelText('Host'), 'github.com');
    await userEvent.type(screen.getByLabelText('A name for this identity'), 'Work');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));

    expect(add).toHaveBeenCalledWith({ way: 'ssh', host: 'github.com', label: 'Work' });
    expect(await screen.findByText('Work is added')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Copy public key' }));
    expect(copy).toHaveBeenCalledWith(expect.stringMatching(/^ssh-ed25519 /));
    expect(screen.getByRole('link', { name: "Open github.com's key page" })).toBeInTheDocument();
  });

  it('makes an RSA key where the host takes RSA alone, and says why', async () => {
    const add = vi.fn(ACTS.add);
    draw(<GitAdd acts={acts({ add })} knownHosts={PAGE.hosts} host="dev.azure.com" onClose={vi.fn()} />);

    expect(screen.getByText('This host takes RSA keys alone, so the new key is RSA 4096.')).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText('A name for this identity'), 'Org A deploy');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));

    expect(add).toHaveBeenCalledWith({ way: 'ssh', host: 'dev.azure.com', label: 'Org A deploy', rsa: true });
  });

  it('confirms an import as a widening before it reads the file', async () => {
    const add = vi.fn(ACTS.add);
    draw(<GitAdd acts={acts({ add })} knownHosts={PAGE.hosts} host="github.com" onClose={vi.fn()} />);

    await userEvent.type(screen.getByLabelText('A name for this identity'), 'Mine');
    await userEvent.click(screen.getByRole('radio', { name: 'Import a key…' }));
    await userEvent.type(screen.getByLabelText('The key file to import'), 'X:/keys/id');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));

    expect(add).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Import this key' }));
    expect(add).toHaveBeenCalledWith({ way: 'ssh', host: 'github.com', label: 'Mine', importFile: 'X:/keys/id' });
  });

  it('takes a token in a password field that is cleared once sent, and offers no browser sign-in', async () => {
    const add = vi.fn(ACTS.add);
    draw(<GitAdd acts={acts({ add })} knownHosts={PAGE.hosts} host="github.com" wayToday="https" onClose={vi.fn()} />);

    await userEvent.type(screen.getByLabelText('A name for this identity'), 'Personal');
    expect(screen.queryByRole('radio', { name: 'Browser sign-in' })).toBeNull();
    expect(screen.getByLabelText('The token')).toHaveAttribute('type', 'password');
    await userEvent.type(screen.getByLabelText('The account on the host'), 'octo-me');
    await userEvent.type(screen.getByLabelText('The token'), 'ghp_exampletoken');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));

    expect(add).toHaveBeenCalledWith({ way: 'https', host: 'github.com', label: 'Personal', user: 'octo-me', token: 'ghp_exampletoken' });
    expect(await screen.findByText('Personal is added')).toBeInTheDocument();
    expect(document.body.textContent).not.toContain('ghp_exampletoken');
  });

  it('says a refusal under the press, with what was typed kept', async () => {
    const add = vi.fn().mockRejectedValue(new Error('an identity is already named Work'));
    draw(<GitAdd acts={acts({ add })} knownHosts={PAGE.hosts} host="github.com" onClose={vi.fn()} />);

    await userEvent.type(screen.getByLabelText('A name for this identity'), 'Work');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('an identity is already named Work');
    expect(screen.getByLabelText('A name for this identity')).toHaveValue('Work');
  });
});

describe('the git reads', () => {
  it('lists each repository with what the library and git each read and why git was asked', () => {
    draw(<GitReads reads={READS} />);

    const engine = screen.getByText('engine').closest('li')!;
    expect(within(engine).getByText('library 412')).toBeInTheDocument();
    expect(within(engine).getByText('git 9')).toBeInTheDocument();
    expect(within(engine).getByText('a question the library leaves to git · 7')).toBeInTheDocument();
    expect(within(engine).getByText('replaced objects, which git reads · 2')).toBeInTheDocument();
    const archive = screen.getByText('archive').closest('li')!;
    expect(within(archive).getByText('reftable refs, which git reads · 131')).toBeInTheDocument();
    expect(within(screen.getByText('site').closest('li')!).queryByText(/·/, { selector: 'li' })).toBeNull();
  });

  it('says a reason it does not know as it was sent, and that nothing is read yet', () => {
    const { unmount } = draw(<GitReads reads={[{ repository: 'odd', library: 1, git: 1, reasons: [{ reason: 'new-reason', count: 1 }], last: new Date().toISOString() }]} />);
    expect(screen.getByText('new-reason · 1')).toBeInTheDocument();
    unmount();

    draw(<GitReads reads={[]} />);
    expect(screen.getByText('Nothing read yet')).toBeInTheDocument();
  });
});
