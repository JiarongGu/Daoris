import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { type PressView, PressViewLine } from '../PressViewLine';

// Bringing repositories up to date is drawn on the Confirm card from its route's view: what the press does, then each row its list
// moves, worded from the catalogue, above the command the press is.

const SYNC: PressView = {
  kind: 'sync-repository',
  facts: { repository: 'engine' },
  command: 'daoris tree sync --repository engine --yes',
  also: [
    { kind: 'sync-line', facts: { repository: 'engine', line: 'main', commits: '3' } },
    { kind: 'sync-replay', facts: { repository: 'engine', branch: 'daoris/q1', commits: '2' } },
    { kind: 'sync-delete', facts: { repository: 'engine', branch: 'daoris/q0' } },
  ],
};

describe('bringing repositories up to date on the Confirm card', () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('says what the press does, each row that moves, and the command it is', () => {
    render(<PressViewLine view={SYNC} />);

    expect(screen.getByText(/^Brings engine up to date from origin, as you/)).toBeInTheDocument();
    expect(screen.getByText('engine: main fast-forwards 3 commits from origin.')).toBeInTheDocument();
    expect(screen.getByText('engine: daoris/q1 replays its 2 commits onto its line.')).toBeInTheDocument();
    expect(screen.getByText('engine: the landed branch daoris/q0 is deleted, its work on the line.')).toBeInTheDocument();
    expect(screen.getByText(SYNC.command)).toBeInTheDocument();
  });

  it('says so where nothing moves, and for every repository', () => {
    render(<PressViewLine view={{ kind: 'sync-all', facts: {}, command: 'daoris tree sync --all --yes', also: [{ kind: 'sync-nothing', facts: {} }] }} />);

    expect(screen.getByText(/^Brings every repository with a checkout here up to date/)).toBeInTheDocument();
    expect(screen.getByText(/^Nothing moves now/)).toBeInTheDocument();
  });

  it('words them in 中文, the command as written', async () => {
    await i18n.changeLanguage('zh');
    render(<PressViewLine view={SYNC} />);

    expect(screen.getByText(/^以你的身份从 origin 把 engine 更新到最新/)).toBeInTheDocument();
    expect(screen.getByText('engine：main 从 origin 快进 3 个提交。')).toBeInTheDocument();
    expect(screen.getByText(SYNC.command)).toBeInTheDocument();
  });
});
