import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { type PressView, PressViewLine } from '../PressViewLine';

// A clear of finished history is drawn on the Confirm card from its route's view: what would go, counted from the first press's
// list, worded from the catalogue, above the command the press is.

const QUEST: PressView = {
  kind: 'history-clear-quest',
  facts: { quest: 'q1', quests: '1', sessions: '2', copies: '0', size: '2 KB' },
  command: 'daoris quest clear q1 --yes',
  also: [{ kind: 'history-clear-remote', facts: { workspace: 'work' } }, { kind: 'history-clear-pieces', facts: { count: '1' } }],
};

const WORKSPACE: PressView = {
  kind: 'history-clear-workspace',
  facts: { workspace: 'work', quests: '4', asks: '1', sessions: '6', copies: '0', size: '3 MB' },
  command: 'daoris history clear --workspace work --yes',
  also: [{ kind: 'history-clear-workspace-keeps', facts: { count: '2' } }],
};

describe('a clear of finished history on the Confirm card', () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it("says what a quest's clear takes, that nothing brings it back, and the command it is", () => {
    render(<PressViewLine view={QUEST} />);

    expect(screen.getByText(/^Clears quest q1 from this machine \(sessions: 2, teammates' copies: 0, 2 KB\)/)).toBeInTheDocument();
    expect(screen.getByText(/Nothing brings it back\.$/)).toBeInTheDocument();
    expect(screen.getByText(/^The remote for work keeps the team's copy/)).toBeInTheDocument();
    expect(screen.getByText(QUEST.command)).toBeInTheDocument();
  });

  it("counts a workspace's clear and what it keeps", () => {
    render(<PressViewLine view={WORKSPACE} />);

    expect(screen.getByText(/^Clears work's finished history from this machine \(quests: 4, asks: 1, sessions: 6/)).toBeInTheDocument();
    expect(screen.getByText(/^It keeps 2 of its quests and asks here/)).toBeInTheDocument();
  });

  it('words them in 中文, the command as written', async () => {
    await i18n.changeLanguage('zh');
    render(<PressViewLine view={QUEST} />);

    expect(screen.getByText(/^从本机清除委托 q1/)).toBeInTheDocument();
    expect(screen.getByText(QUEST.command)).toBeInTheDocument();
    cleanup();

    render(<PressViewLine view={WORKSPACE} />);
    expect(screen.getByText(/^从本机清除 work 已完成的历史/)).toBeInTheDocument();
  });
});
