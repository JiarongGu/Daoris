import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { GIT_CHANGES_ENGINE, GIT_STASH_PLAN_ENGINE, GIT_SWITCH_PLAN_ENGINE } from '../fixtures';
import { type ChangesActs, ChangesTab } from './RepositoryGit';
import { StashBox, type StashPlanValue } from './StashBox';
import { SwitchBox, type SwitchPlanValue } from './SwitchBox';

// Switch and stash Save in the Changes tab (GITAPP2k, D204, the git-writes design §8): the plan's words (the hooks, why a press is refused,
// the stash shared with every linked tree), a press that sends back the HEAD and stash it was read with, and the last act said. Molecules: the
// plans arrive as props.

const switchPlan = (over: Partial<SwitchPlanValue> = {}): SwitchPlanValue => ({ ...GIT_SWITCH_PLAN_ENGINE, ...over });
const stashPlan = (over: Partial<StashPlanValue> = {}): StashPlanValue => ({ ...GIT_STASH_PLAN_ENGINE, ...over });

describe('the switch box', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  const box = (props: Partial<Parameters<typeof SwitchBox>[0]> = {}) => (
    <SwitchBox branches={['main', 'release']} chosen="release" onChoose={vi.fn()} read={{ state: 'read', value: switchPlan() }} busy={false} onPress={vi.fn()} {...props} />
  );

  it('presses Switch with the branch and the HEAD it was read with', async () => {
    const onPress = vi.fn();
    render(box({ onPress }));

    await userEvent.click(screen.getByRole('button', { name: 'Switch' }));

    expect(onPress).toHaveBeenCalledWith({ branch: 'release', from: 'main', head: GIT_SWITCH_PLAN_ENGINE.head });
    expect(screen.getByText(/Runs these hooks, never skipped/)).toHaveTextContent('post-checkout');
  });

  it('holds Switch with no branch chosen and says what the plan found against it', () => {
    const { unmount } = render(box({ chosen: '', read: null }));
    expect(screen.getByRole('button', { name: 'Switch' })).toBeDisabled();
    unmount();

    render(box({ read: { state: 'read', value: switchPlan({ dirty: true, why: 'tracked files are changed here; commit or stash them first, since a switch never carries them' }) } }));
    expect(screen.getByRole('button', { name: 'Switch' })).toBeDisabled();
    expect(screen.getByText(/tracked files are changed here/)).toBeInTheDocument();
  });

  it('says a branch checked out elsewhere in the driver\'s words and holds every control while an act runs', () => {
    const { unmount } = render(box({ read: { state: 'read', value: switchPlan({ elsewhere: 'physics', why: '`release` is checked out in another tree, `physics`' }) } }));
    expect(screen.getByRole('region', { name: 'Switch branch' })).toHaveTextContent('release is checked out in another tree, physics');
    expect(screen.getByRole('button', { name: 'Switch' })).toBeDisabled();
    unmount();

    render(box({ busy: true }));
    expect(screen.getByRole('button', { name: 'Switch' })).toBeDisabled();
    expect(screen.getByRole('combobox', { name: 'Branch to switch to' })).toBeDisabled();
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(box());

    expect(screen.getByRole('region', { name: '切换分支' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '切换' })).toBeInTheDocument();
    expect(screen.getByText(/将运行这些钩子，绝不跳过/)).toHaveTextContent('post-checkout');
  });
});

describe('the stash box', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  const box = (value: StashPlanValue, onPress = vi.fn(), busy = false) => render(<StashBox read={{ state: 'read', value }} busy={busy} onPress={onPress} />);

  it('says the stash is shared with every linked tree and counts what a Save would take', () => {
    box(stashPlan());

    expect(screen.getByText(/shared by every linked tree, sessions' included/)).toHaveTextContent('Other trees sharing it: 1');
    expect(screen.getByText('Changed tracked files: 2. Untracked files: 1.')).toBeInTheDocument();
  });

  it('saves tracked files only by default, and untracked ones when asked, with the tip and stash it was read with', async () => {
    const onPress = vi.fn();
    box(stashPlan({ stash: 'ab12cd' }), onPress);

    await userEvent.click(screen.getByRole('button', { name: 'Save to the stash' }));
    await userEvent.click(screen.getByRole('checkbox', { name: 'Include untracked files' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'Message (optional)' }), 'before the switch');
    await userEvent.click(screen.getByRole('button', { name: 'Save to the stash' }));

    expect(onPress).toHaveBeenNthCalledWith(1, { untracked: false, message: '', head: GIT_STASH_PLAN_ENGINE.head, stash: 'ab12cd' }, expect.any(Function));
    expect(onPress).toHaveBeenNthCalledWith(2, { untracked: true, message: 'before the switch', head: GIT_STASH_PLAN_ENGINE.head, stash: 'ab12cd' }, expect.any(Function));
  });

  it('holds the press where only untracked files stand until they are included, and where there is nothing', async () => {
    const { unmount } = box(stashPlan({ tracked: 0 }));
    expect(screen.getByRole('button', { name: 'Save to the stash' })).toBeDisabled();
    expect(screen.getByText('No tracked file is changed: include the untracked files to save them.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('checkbox', { name: 'Include untracked files' }));
    expect(screen.getByRole('button', { name: 'Save to the stash' })).toBeEnabled();
    unmount();

    box(stashPlan({ tracked: 0, untracked: 0, why: 'there is nothing to save' }));
    expect(screen.getByRole('button', { name: 'Save to the stash' })).toBeDisabled();
    expect(screen.getByText('there is nothing to save')).toBeInTheDocument();
  });

  it('clears the message once the act is done, and holds every control while one runs', async () => {
    const onPress = vi.fn((_press: unknown, done: () => void) => done());
    const { unmount } = box(stashPlan(), onPress);
    await userEvent.type(screen.getByRole('textbox', { name: 'Message (optional)' }), 'x');

    await userEvent.click(screen.getByRole('button', { name: 'Save to the stash' }));

    expect(screen.getByRole('textbox', { name: 'Message (optional)' })).toHaveValue('');
    unmount();

    box(stashPlan(), vi.fn(), true);
    expect(screen.getByRole('button', { name: 'Save to the stash' })).toBeDisabled();
    expect(screen.getByRole('textbox', { name: 'Message (optional)' })).toBeDisabled();
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    box(stashPlan());

    expect(screen.getByRole('region', { name: '贮藏' })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: '包含未跟踪文件' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '保存到贮藏' })).toBeInTheDocument();
  });
});

describe('the last switch and stash in the Changes tab', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  const acts = (last: ChangesActs['last']): ChangesActs => ({
    busy: false, last, press: vi.fn(), undo: vi.fn(), Hunks: () => null,
    Switch: ({ busy }) => <SwitchBox branches={['main', 'release']} chosen="" onChoose={vi.fn()} read={null} busy={busy} onPress={vi.fn()} />,
    Stash: ({ busy }) => <StashBox read={{ state: 'read', value: stashPlan() }} busy={busy} onPress={vi.fn()} />,
  });
  const tab = (given: ChangesActs) => render(<ChangesTab repository="engine" read={{ state: 'read', value: GIT_CHANGES_ENGINE }} acts={given} />);

  it('draws both boxes and says a done switch with the way back, offering no Undo of its own', () => {
    tab(acts({ verb: 'switch', path: 'release', act: 'act-1', done: true, said: 'x', notes: 'Switch back to `main` to undo it.' }));

    expect(screen.getByRole('region', { name: 'Switch branch' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Stash' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Switched to release. Switch back to main to undo it.');
    expect(screen.queryByRole('button', { name: 'Undo' })).toBeNull();
  });

  it('says a done stash with the commit saved, and a refusal in the driver\'s words, in 中文 too', async () => {
    tab(acts({ verb: 'stash', path: null, act: 'act-2', done: true, said: 'x', notes: 'Saved as stash `ab12cd` in `refs/stash`.' }));
    expect(screen.getByRole('status')).toHaveTextContent('Your changes were taken out of the working tree. Saved as stash ab12cd');
  });

  it('says a switch done in 中文', async () => {
    await i18n.changeLanguage('zh');
    tab(acts({ verb: 'switch', path: 'release', act: 'act-3', done: true, said: 'x' }));

    expect(screen.getByRole('status')).toHaveTextContent('已切换到 release');
  });

  it('draws no box where the acts bring none', () => {
    tab({ ...acts(null), Switch: undefined, Stash: undefined });

    expect(screen.queryByRole('region', { name: 'Switch branch' })).toBeNull();
    expect(screen.queryByRole('region', { name: 'Stash' })).toBeNull();
  });
});
