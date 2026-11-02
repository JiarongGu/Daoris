import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import '../../locales/i18n';
import { GitHosts, GitRemotes, type GitRemoteShown } from './GitRemotes';
import { DEPLOY, PERSONAL, WORK } from './gitFixtures';

// A repository's *Git remote* and a workspace's *Git* on their Setup (GITAUTH1e, the git-auth design §7.3), drawn from props.

const ORIGIN: GitRemoteShown = {
  name: 'origin', address: 'github.com:org-a/engine', host: 'github.com', way: 'ssh', serves: 'identity', identity: WORK.id, label: 'Work',
  source: 'workspace', passed: [], chosen: null,
};
const UPSTREAM: GitRemoteShown = {
  name: 'upstream', address: 'https://github.com/upstream/engine.git', host: 'github.com', way: 'https', serves: 'identity', identity: PERSONAL.id,
  label: 'Personal', source: 'default', passed: [], chosen: null,
};
const AZURE: GitRemoteShown = {
  name: 'deploy', address: 'ssh.dev.azure.com:v3/org-a/engine', host: 'dev.azure.com', way: 'ssh', serves: 'none', source: null, passed: [], chosen: null,
};

const draw = (node: React.ReactNode) => render(<Tooltip.Provider>{node}</Tooltip.Provider>);

describe('a repository\'s git remotes', () => {
  it('says each remote\'s address, the identity it resolves to and where that came from', () => {
    draw(<GitRemotes remotes={[ORIGIN, UPSTREAM]} identities={[WORK, PERSONAL]} acts={{ use: vi.fn() }} />);

    const origin = document.querySelector('[data-git-remote="origin"]') as HTMLElement;
    expect(within(origin).getByText('github.com:org-a/engine')).toBeInTheDocument();
    expect(within(origin).getByText('Work · SSH · from this workspace')).toBeInTheDocument();
    const upstream = document.querySelector('[data-git-remote="upstream"]') as HTMLElement;
    expect(within(upstream).getByText("Personal · HTTPS · from github.com's default")).toBeInTheDocument();
  });

  it('says No git identity in the danger ink with the door to set one up, and a local path needs none', async () => {
    const onSetUp = vi.fn();
    draw(<GitRemotes remotes={[AZURE, { ...ORIGIN, name: 'backup', address: '/mnt/backup', host: null, way: null, serves: 'local', label: null }]} identities={[DEPLOY]} acts={{ use: vi.fn(), onSetUp }} />);

    expect(screen.getByText('No git identity')).toHaveClass('text-ink-danger');
    await userEvent.click(screen.getByRole('button', { name: 'Set one up…' }));
    expect(onSetUp).toHaveBeenCalledWith('dev.azure.com');
    expect(screen.getByText('A local path: no identity needed.')).toBeInTheDocument();
  });

  it('chooses an identity of the host and way for a remote, and clears the choice with As the workspace', async () => {
    const use = vi.fn(async () => ({}));
    draw(<GitRemotes remotes={[{ ...ORIGIN, chosen: WORK.id, source: 'repository' }]} identities={[WORK, PERSONAL]} acts={{ use }} />);

    await userEvent.click(screen.getByRole('button', { name: 'Change…' }));
    screen.getByRole('combobox', { name: 'Choose for origin' }).focus();
    await userEvent.keyboard('{Enter}');
    // Only the identities of the remote's host and way are offered, beside the workspace's and the person's own.
    expect((await screen.findAllByRole('option')).map((option) => option.textContent)).toEqual(['As the workspace', 'Work', 'Own sign-ins']);
    await userEvent.click(screen.getByRole('option', { name: 'As the workspace' }));
    await userEvent.click(screen.getByRole('button', { name: 'Use this' }));

    expect(use).toHaveBeenCalledWith(expect.objectContaining({ name: 'origin' }), null);
  });

  it('confirms the person\'s own sign-ins as a widening and writes nothing before the second press', async () => {
    const use = vi.fn(async () => ({}));
    draw(<GitRemotes remotes={[ORIGIN]} identities={[WORK]} acts={{ use }} />);

    await userEvent.click(screen.getByRole('button', { name: 'Change…' }));
    screen.getByRole('combobox', { name: 'Choose for origin' }).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('option', { name: 'Own sign-ins' }));

    expect(screen.getByText(/your own SSH keys, git configuration and stored sign-ins for origin/)).toBeInTheDocument();
    expect(use).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Use my own sign-ins' }));

    expect(use).toHaveBeenCalledWith(expect.objectContaining({ name: 'origin' }), 'own');
  });

  it('says a refusal under the press, keeping the choice', async () => {
    const use = vi.fn().mockRejectedValue(new Error('upstream is at the same address and points elsewhere'));
    draw(<GitRemotes remotes={[ORIGIN]} identities={[WORK, { ...WORK, id: 'gi-00000000000f', label: 'Side', default: false }]} acts={{ use }} />);

    await userEvent.click(screen.getByRole('button', { name: 'Change…' }));
    screen.getByRole('combobox', { name: 'Choose for origin' }).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('option', { name: 'Side' }));
    await userEvent.click(screen.getByRole('button', { name: 'Use this' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('upstream is at the same address and points elsewhere');
  });
});

describe('a workspace\'s hosts', () => {
  it('has a row per host and way with the remotes that reach it, and points the workspace at an identity', async () => {
    const use = vi.fn(async () => ({}));
    draw(<GitHosts hosts={[{ host: 'github.com', way: 'ssh', chosen: null, remotes: ['engine: origin', 'site: origin'] }]} identities={[WORK]} acts={{ use }} />);

    expect(screen.getByText('github.com · SSH')).toBeInTheDocument();
    expect(screen.getByText('engine: origin, site: origin')).toBeInTheDocument();
    screen.getByRole('combobox', { name: 'The identity for github.com' }).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('option', { name: 'Work' }));

    expect(use).toHaveBeenCalledWith('github.com', WORK.id);
  });

  it('confirms the person\'s own sign-ins for the host as a widening', async () => {
    const use = vi.fn(async () => ({}));
    draw(<GitHosts hosts={[{ host: 'github.com', way: 'ssh', chosen: null, remotes: ['engine: origin'] }]} identities={[WORK]} acts={{ use }} />);

    screen.getByRole('combobox', { name: 'The identity for github.com' }).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('option', { name: 'Own sign-ins' }));
    expect(use).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Use my own sign-ins' }));

    expect(use).toHaveBeenCalledWith('github.com', 'own');
  });
});
