import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { FilesLook } from './filesLook';
import { FilesTab } from './FilesTab';

// The Files tab draws each ignored top's effective rule, its layer and what the layers below said, the acts and the person's
// answers, and the person's own rules and permissions, in both languages.

const LOOK: FilesLook = {
  repository: 'engine',
  commit: 'def1234567',
  foundCommit: 'abc1234567',
  files: [
    {
      path: 'node_modules/', kind: 'scratch', layer: 'person', files: 120, bytes: 4096, evidence: ['beside package-lock.json'],
      below: [{ layer: 'found', kind: 'dependency' }, { layer: 'repository', kind: 'output' }],
    },
    { path: 'local/', kind: 'local', layer: 'repository', copy: true },
    { path: 'src/', kind: null, layer: 'floor', floor: 'tracked' },
  ],
  acts: [
    { act: 'run(npm ci)', path: 'node_modules/', verdict: 'held' },
    { act: 'copy(local/)', path: 'local/', verdict: 'denied' },
  ],
  permissions: [
    { scope: 'repository', deny: ['copy(*)'], ask: ['run(npm *)'], allow: [{ rule: 'share(*)', by: 'person' }] },
    { scope: 'machine', deny: [], ask: [], allow: [] },
  ],
  rules: [{ scope: 'repository', kind: 'scratch', paths: ['node_modules/'], copy: null }],
  problems: [],
};

describe("a repository's Files tab", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it("shows a person's rule over the repository's and the found one's, beside both, with its size and evidence", () => {
    render(<FilesTab repository="engine" look={LOOK} />);

    expect(screen.getByRole('heading', { name: 'Files git does not manage' })).toBeInTheDocument();
    const row = within(screen.getByRole('list', { name: 'Paths' })).getAllByRole('listitem')[0]!;
    expect(row).toHaveTextContent('node_modules/');
    expect(row).toHaveTextContent('Scratch');
    expect(row).toHaveTextContent('by your rules');
    expect(row).toHaveTextContent('120 files · 4 KB');
    expect(row).toHaveTextContent('beside package-lock.json');
    expect(row).toHaveTextContent('Below it: the analysis said Dependencies; the repository said Runnable outputs');
  });

  it('shows a copy rule with who can read the copy and the answer to it', () => {
    render(<FilesTab repository="engine" look={LOOK} />);

    const row = within(screen.getByRole('list', { name: 'Paths' })).getAllByRole('listitem')[1]!;
    expect(row).toHaveTextContent('every session working in a tree can read the copy');
    expect(row).toHaveTextContent('Your permissions deny it, so it is not made.');
    expect(screen.getByText("Git tracks it, so it is git's.", { exact: false })).toBeInTheDocument();
  });

  it('presses an allow for an ask in one press, and takes an allow back with its maker shown', async () => {
    const onPress = vi.fn();
    render(<FilesTab repository="engine" look={LOOK} onPress={onPress} />);

    await userEvent.click(screen.getByRole('button', { name: 'Allow run(npm ci)' }));
    expect(onPress).toHaveBeenLastCalledWith({ act: 'permit', change: 'add', list: 'allow', rule: 'run(npm ci)' });

    const permissions = within(screen.getByRole('list', { name: 'Your permissions' }));
    expect(permissions.getByText('share(*)').closest('li')).toHaveTextContent('given by you');
    await userEvent.click(screen.getByRole('button', { name: 'Take back the allow share(*)' }));
    expect(onPress).toHaveBeenLastCalledWith({ act: 'permit', change: 'remove', list: 'allow', rule: 'share(*)', machine: false });
  });

  it("says a supervisor gave an allow, by its role, in both languages, and takes it back like the person's own", async () => {
    const onPress = vi.fn();
    const given: FilesLook = { ...LOOK, permissions: [{ scope: 'repository', deny: [], ask: ['run(npm *)'], allow: [{ rule: 'run(npm ci)', by: 'supervisor-files' }] }] };
    render(<FilesTab repository="engine" look={given} onPress={onPress} />);

    const row = within(screen.getByRole('list', { name: 'Your permissions' })).getByText('run(npm ci)').closest('li')!;
    expect(row).toHaveTextContent('given by the supervisor supervisor-files');
    await userEvent.click(within(row).getByRole('button', { name: 'Take back the allow run(npm ci)' }));
    expect(onPress).toHaveBeenLastCalledWith({ act: 'permit', change: 'remove', list: 'allow', rule: 'run(npm ci)', machine: false });

    await i18n.changeLanguage('zh');
    expect(row).toHaveTextContent('由主管 supervisor-files给出');
  });

  it("adds and removes the person's own rules, and reads again", async () => {
    const onPress = vi.fn();
    render(<FilesTab repository="engine" look={LOOK} onPress={onPress} />);

    await userEvent.click(screen.getByRole('button', { name: 'Remove your rule for node_modules/' }));
    expect(onPress).toHaveBeenLastCalledWith({ act: 'remove', paths: ['node_modules/'], machine: false });

    await userEvent.click(screen.getByRole('button', { name: 'Add a rule' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'Paths' }), 'tmp/');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));
    expect(onPress).toHaveBeenLastCalledWith({ act: 'set', kind: 'scratch', paths: ['tmp/'], copy: undefined, machine: false });

    await userEvent.click(screen.getByRole('button', { name: 'Read again' }));
    expect(onPress).toHaveBeenLastCalledWith({ act: 'read' });
  });

  it('says a file that does not read, and a press it refused', () => {
    render(<FilesTab repository="engine" look={{ ...LOOK, unread: 'files.json is not valid JSON' }} said="Nothing was written." />);

    expect(screen.getByRole('alert')).toHaveTextContent('Your file rules do not read (files.json is not valid JSON)');
    expect(screen.getByRole('status')).toHaveTextContent('Nothing was written.');
  });

  it('says the same in Chinese, with the glossary names', async () => {
    await i18n.changeLanguage('zh');
    render(<FilesTab repository="engine" look={{ ...LOOK, unread: 'x' }} onPress={vi.fn()} />);

    expect(screen.getByRole('heading', { name: 'git 不管理的文件' })).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('你的文件规则无法读取');
    const row = within(screen.getByRole('list', { name: '路径' })).getAllByRole('listitem')[0]!;
    expect(row).toHaveTextContent('临时文件');
    expect(row).toHaveTextContent('由你的规则判定');
    expect(row).toHaveTextContent('它之下：分析认为是依赖；仓库规则认为是可运行产物');
    expect(screen.getByRole('button', { name: '允许 run(npm ci)' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '重新读取' })).toBeInTheDocument();
  });

  it('says it is on its way or why there is none', () => {
    const { rerender } = render(<FilesTab repository="engine" reading />);
    expect(screen.getByText('Reading the files git does not manage.')).toBeInTheDocument();
    rerender(<FilesTab repository="engine" refusal="This repository has no checkout on this machine." />);
    expect(screen.getByText('This repository has no checkout on this machine.')).toBeInTheDocument();
  });
});
