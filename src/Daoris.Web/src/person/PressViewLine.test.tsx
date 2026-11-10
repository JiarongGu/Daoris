import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { PressViewLine } from './PressViewLine';

describe('PressViewLine', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('says what a press means in the window language, with the command it is', async () => {
    await i18n.changeLanguage('en');
    render(
      <PressViewLine
        view={{ kind: 'repository-move', facts: { repository: 'engine', from: 'default', to: 'work' }, command: 'daoris repository wire engine --workspace work' }}
      />,
    );

    expect(screen.getByText(/Moves engine from workspace default to workspace work/)).toBeTruthy();
    expect(screen.getByText('daoris repository wire engine --workspace work').tagName).toBe('CODE');
  });

  it('says a move starts a workspace no repository is in yet, in Chinese too', async () => {
    await i18n.changeLanguage('zh');
    render(
      <PressViewLine
        view={{ kind: 'repository-move-starts', facts: { repository: 'engine', from: 'default', to: 'lab' }, command: 'daoris repository wire engine --workspace lab' }}
      />,
    );

    const said = screen.getByText(/工作区 lab/);
    expect(said.tagName).toBe('P');
    expect(said.textContent).toContain('lab 里还没有仓库');
  });

  it('says a rule with its mode sentence and each further sentence, in both languages', async () => {
    const view = {
      kind: 'commits-set',
      facts: { repository: 'engine', mode: 'at-end' },
      command: 'daoris place commits engine --mode at-end',
      also: [{ kind: 'opinion-bound', facts: { minutes: '20' } }],
    };
    await i18n.changeLanguage('en');
    const { unmount } = render(<PressViewLine view={view} />);
    expect(screen.getByText(/Sets the commit rule for engine: at-end\. One commit is made when the work is done\./)).toBeTruthy();
    expect(screen.getByText('One pass takes at most 20 minutes.')).toBeTruthy();
    unmount();

    await i18n.changeLanguage('zh');
    render(<PressViewLine view={view} />);
    expect(screen.getByText(/工作完成时进行一次提交/)).toBeTruthy();
    expect(screen.getByText('一轮最多 20 分钟。')).toBeTruthy();
  });
});
