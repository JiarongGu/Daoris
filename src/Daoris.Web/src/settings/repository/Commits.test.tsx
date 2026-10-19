import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../locales/i18n';
import { CommitField, type CommitRule, commitRowSays, commitSays, commitSummary, commitToast } from './Commits';

// DOORS1b (D164's amendment points 1 and 3): how a repository's work is committed, set on its Setup's *Line and landing* and on
// a workspace's Defaults as `daoris driver commits` sets it from a terminal (D50). Each mode's sentence is the terminal's
// (`driver commits`' words in the CLI's `driverconfig.ts`), the person's convention is quoted as written, and the field sends
// the twins' edit, which the driver judges.

const t = i18n.t.bind(i18n);

const OWN: CommitRule = { mode: 'ask', convention: 'Imperative subject line; the ticket first.' };

const draw = (set: CommitRule | undefined, inherited: CommitRule = { mode: 'as-you-go' }, onChange = vi.fn()) => {
  render(
    <Tooltip.Provider>
      <CommitField name="web-app" set={set} inherited={inherited} onChange={onChange} />
    </Tooltip.Provider>,
  );
  return onChange;
};

describe('what a commit rule says', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it("says each mode in the terminal's words, and the convention quoted as written", () => {
    expect(commitSays(t, { mode: 'as-you-go' })).toEqual(['Each piece that passes its checks is committed as the work goes.']);
    expect(commitSays(t, { mode: 'ask' })).toEqual(['Each commit waits for your yes before it is made.']);
    expect(commitSays(t, { mode: 'at-end', convention: null })).toEqual(['One commit is made when the work is done.']);
    expect(commitSays(t, OWN)).toEqual([
      'Each commit waits for your yes before it is made.',
      'Each session is told how a commit message is written: “Imperative subject line; the ticket first.”',
    ]);
  });

  it('says where it was set, the default included, and folds to its mode', () => {
    expect(commitRowSays(t, { mode: 'as-you-go' }, { source: 'default' }))
      .toBe("Each piece that passes its checks is committed as the work goes. Daoris's default: nothing is set.");
    expect(commitRowSays(t, OWN, { source: 'workspace', workspace: 'work' })).toBe(
      'Each commit waits for your yes before it is made. Each session is told how a commit message is written: '
      + '“Imperative subject line; the ticket first.” From the workspace work.');
    expect(commitRowSays(t, { mode: 'at-end' }, { source: 'repository' })).toBe('One commit is made when the work is done. Set for this repository.');
    expect(commitSummary(t, { mode: 'as-you-go' })).toBe('commits as it goes');
    expect(commitSummary(t, OWN)).toBe('commits after your yes');
    expect(commitSummary(t, { mode: 'at-end' })).toBe('one commit at the end');
  });

  it('toasts what a change did', () => {
    expect(commitToast(t, 'web-app', { set: { mode: 'as-you-go', convention: '' } })).toBe('In web-app, work is now committed as it goes.');
    expect(commitToast(t, 'web-app', { set: { mode: 'ask' } })).toBe('Each commit in web-app now waits for your yes.');
    expect(commitToast(t, 'web-app', { set: { mode: 'at-end' } })).toBe('In web-app, work is now committed once, when it is done.');
    expect(commitToast(t, 'web-app', { clear: true })).toBe('web-app takes its commit rule from what stands above it again.');
  });

  it('says it in Chinese, the convention kept as written', async () => {
    await i18n.changeLanguage('zh');

    expect(commitSays(t, OWN)).toEqual([
      '每次提交前都等你同意。',
      '每个会话都会被告知提交信息怎么写：“Imperative subject line; the ticket first.”',
    ]);
    expect(commitRowSays(t, { mode: 'as-you-go' }, { source: 'default' })).toBe('通过检查的每一部分随做随提交。Daoris 的默认：哪里都没有设定。');
    expect(commitSummary(t, { mode: 'at-end' })).toBe('完成时提交一次');
    expect(commitToast(t, 'web-app', { set: { mode: 'ask' } })).toBe('web-app 中的每次提交现在都先等你同意。');
  });

  /** Its terminal twin is one command with its alternatives joined by `|`, never translated, as its neighbours' (OPINIONTWIN1). */
  it('says its terminal twin as its neighbours do, one command with its alternatives joined by |, in both languages', () => {
    for (const key of ['settings.commits.twin.repository', 'settings.commits.twin.workspace']) {
      const english = i18n.getFixedT('en')(key, { repository: 'engine', workspace: 'aurora' });
      const chinese = i18n.getFixedT('zh')(key, { repository: 'engine', workspace: 'aurora' });

      expect(chinese).toBe(english);
      expect(english.match(/`/g)).toHaveLength(2);
      expect(english).toMatch(/--mode as-you-go\|ask\|at-end --convention "…"\|--clear`$/);
    }
    expect(i18n.getFixedT('en')('settings.commits.twin.repository', { repository: 'engine' }))
      .toBe('`daoris driver commits engine --mode as-you-go|ask|at-end --convention "…"|--clear`');
    expect(i18n.getFixedT('en')('settings.commits.twin.workspace', { workspace: 'aurora' }))
      .toBe('`daoris driver commits --workspace aurora --mode as-you-go|ask|at-end --convention "…"|--clear`');
  });
});

describe("a commit rule's field", () => {
  it('starts from what stands above where nothing of its own is set, and sends the mode and the convention as typed', async () => {
    const onChange = draw(undefined, { mode: 'at-end', convention: 'Say why.' });
    const user = userEvent.setup();

    expect(screen.getByRole('radio', { name: 'At the end' })).toBeChecked();
    const convention = screen.getByRole('textbox', { name: 'Your commit convention for web-app' });
    expect(convention).toHaveValue('Say why.');
    // Setting it here as it stands is a rule of its own, so Save is pressable at once.
    expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled();
    await user.click(screen.getByRole('radio', { name: 'Ask first' }));
    await user.clear(convention);
    await user.type(convention, '  Imperative subject.  ');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(onChange).toHaveBeenLastCalledWith({ set: { mode: 'ask', convention: 'Imperative subject.' } });
  });

  it('opens on the rule set here, holds Save until something changes, and an emptied convention takes it away', async () => {
    const onChange = draw(OWN);
    const user = userEvent.setup();

    expect(screen.getByRole('radio', { name: 'Ask first' })).toBeChecked();
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
    expect(screen.getByText('42 of 400 characters, one paragraph')).toBeInTheDocument();
    await user.clear(screen.getByRole('textbox', { name: 'Your commit convention for web-app' }));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(onChange).toHaveBeenLastCalledWith({ set: { mode: 'ask', convention: '' } });
  });
});
