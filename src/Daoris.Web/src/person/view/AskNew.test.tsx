import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { type PressView, PressViewLine } from '../PressViewLine';

// A new ask is drawn on the Confirm card from its route's view (`ASK_NEW`): where it is made and its words, then what it carries,
// worded from the catalogue, above the command the press is.

const ASK: PressView = {
  kind: 'ask-new-to',
  facts: { workspace: 'games', sentence: 'fix the stall', to: 'engine' },
  command: 'daoris ask --workspace games --to engine --short Stall --review off "fix the stall"',
  also: [
    { kind: 'ask-new-short', facts: { title: 'Stall' } },
    { kind: 'ask-new-review-off', facts: {} },
  ],
};

describe('a new ask on the Confirm card', () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('says where it is made, its words, what it carries and the command it is', () => {
    render(<PressViewLine view={ASK} />);

    expect(screen.getByText('Makes an ask at workspace games: “fix the stall”, published at once as a quest for engine.')).toBeInTheDocument();
    expect(screen.getByText('A list names it “Stall”.')).toBeInTheDocument();
    expect(screen.getByText('Its work lands with no review first.')).toBeInTheDocument();
    expect(screen.getByText(ASK.command)).toBeInTheDocument();
  });

  it('says its intake turns one that names no receiver into quests', () => {
    render(<PressViewLine view={{ kind: 'ask-new', facts: { workspace: 'default', sentence: 'tidy the docs' }, command: 'daoris ask --workspace default "tidy the docs"' }} />);

    expect(screen.getByText(/^Makes an ask at workspace default: “tidy the docs”\. Its intake turns it into quests/)).toBeInTheDocument();
  });

  it('words them in 中文, the command as written', async () => {
    await i18n.changeLanguage('zh');
    render(<PressViewLine view={ASK} />);

    expect(screen.getByText('在工作区 games 提出需求：“fix the stall”，并立即作为委托发给 engine。')).toBeInTheDocument();
    expect(screen.getByText('列表中以“Stall”称呼它。')).toBeInTheDocument();
    expect(screen.getByText(ASK.command)).toBeInTheDocument();
  });
});
