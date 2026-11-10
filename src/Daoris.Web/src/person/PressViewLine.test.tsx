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
});
