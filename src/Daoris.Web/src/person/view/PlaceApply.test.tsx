import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { type PressView, PressViewLine } from '../PressViewLine';

// A place's set-up applied is drawn on the Confirm card from its route's view (`PLACE_APPLY`): what the press applies and where, then
// each change its plan makes in the plan's own words, what the ask carries and each acceptance, above the command the press is.

const APPLY: PressView = {
  kind: 'place-apply',
  facts: { repositories: 'engine, site', workspace: 'aurora', preset: 'merge-after-accept' },
  command: 'daoris place engine site --preset merge-after-accept --adoption --apply --accept engine',
  also: [
    { kind: 'place-change', facts: { said: 'Driving', before: 'off', after: 'on', repositories: 'engine, site' } },
    { kind: 'place-adoption', facts: {} },
    { kind: 'place-accept', facts: { repository: 'engine' } },
  ],
};

describe("a place's set-up on the Confirm card", () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('says what it applies and where, each change, what it carries and the command it is', () => {
    render(<PressViewLine view={APPLY} />);

    expect(screen.getByText(/^Applies the set-up merge-after-accept to engine, site in workspace aurora/)).toBeInTheDocument();
    expect(screen.getByText('Driving: off → on (engine, site).')).toBeInTheDocument();
    expect(screen.getByText("Asks each repository's own session, as a quest, to set it up for agents.")).toBeInTheDocument();
    expect(screen.getByText('Accepts the declaration of engine.')).toBeInTheDocument();
    expect(screen.getByText(APPLY.command)).toBeInTheDocument();
  });

  it('says so where the plan changes nothing', () => {
    render(<PressViewLine view={{ ...APPLY, also: [{ kind: 'place-nothing', facts: {} }] }} />);

    expect(screen.getByText('The plan changes nothing now.')).toBeInTheDocument();
  });

  it('words them in 中文, the command as written', async () => {
    await i18n.changeLanguage('zh');
    render(<PressViewLine view={APPLY} />);

    expect(screen.getByText(/^把配置 merge-after-accept 应用到工作区 aurora 的 engine, site/)).toBeInTheDocument();
    expect(screen.getByText('Driving：off → on（engine, site）。')).toBeInTheDocument();
    expect(screen.getByText(APPLY.command)).toBeInTheDocument();
  });
});
