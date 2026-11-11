import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { type PressView, PressViewLine } from '../PressViewLine';

// A press of a place's supervisor rules is drawn on the Confirm card from its view, worded from the catalogue.

const RULES: PressView = {
  kind: 'role-rules',
  facts: { place: 'repository:daoris' },
  command: 'daoris role rules --place repository:daoris --clear all --set answers-alone=3:hold@build',
  rules: {
    place: 'repository:daoris',
    before: [],
    after: [{ count: 'answers-alone', at: 3, do: 'hold', roles: ['build'] }, { count: 'limits', at: 1, do: 'wait', upTo: 30, roles: null }],
  },
};

describe("a place's supervisor rules on the Confirm card", () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('words the place and each rule before and after from the catalogue in English, with the command', () => {
    render(<PressViewLine view={RULES} />);

    expect(screen.getByText("Writes the supervisor's rules for repository:daoris whole, over the ones it holds now.")).toBeInTheDocument();
    expect(screen.getByText('Supervisor: its rules for Repository daoris')).toBeInTheDocument();
    expect(screen.getByText('No rules of its own here')).toBeInTheDocument();
    const after = within(screen.getByRole('region', { name: 'After you confirm' }));
    expect(after.getByText('Answers alone in a row, at 3: Wait for you, for build')).toBeInTheDocument();
    expect(after.getByText('It reaches its limit, at 1: Wait for it up to 30 minutes, for Every role')).toBeInTheDocument();
    expect(screen.getByText(RULES.command)).toBeInTheDocument();
  });

  it('words them in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<PressViewLine view={RULES} />);

    expect(screen.getByText('主管：仓库 daoris的规则')).toBeInTheDocument();
    expect(screen.getByRole('region', { name: '确认之后' })).toBeInTheDocument();
  });
});
