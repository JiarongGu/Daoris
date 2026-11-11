import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { decidersAt, deciderText, offeredKinds } from '../../agents/setup/whoDecides';
import type { DeciderLine, DecidersRead, RoleCatalogue } from '../../data/wiring';
import i18n from '../../locales/i18n';
import { type PressView, PressViewLine } from '../PressViewLine';

/** Turning the second check on: the role door's card names its readers' providers and what they spend, and the roles say who reads Ask Daoris's acts. */
describe('the second check', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  const view: PressView = {
    kind: 'role-supervisor-set',
    facts: { role: 'second-check', name: 'Second check · 复核' },
    command: 'daoris role set second-check --holder agent "codex - -" "claude-code opus high"',
    also: [
      { kind: 'second-check-on', facts: { readers: 'Codex (OpenAI), Claude Code (Anthropic)' } },
      { kind: 'second-check-spend', facts: { starts: '40' } },
    ],
  };

  it('names on its card whom the acts are sent to and the starts a day each reader may take, in English', async () => {
    await i18n.changeLanguage('en');
    render(<PressViewLine view={view} />);

    expect(screen.getByText(/sent to be read by Codex \(OpenAI\), Claude Code \(Anthropic\), in that order/)).toBeTruthy();
    expect(screen.getByText(/at most 40 starts a day for each choice/)).toBeTruthy();
  });

  it('names them in Chinese too', async () => {
    await i18n.changeLanguage('zh');
    render(<PressViewLine view={view} />);

    expect(screen.getByText(/依次发送给 Codex \(OpenAI\), Claude Code \(Anthropic\) 复核/)).toBeTruthy();
    expect(screen.getByText(/每个选择每天最多 40 次启动/)).toBeTruthy();
  });

  const line = (holder: string, granted: string[]): DeciderLine =>
    ({ kind: 'ask-daoris', role: 'second-check', scope: 'machine', holder, granted, held: null }) as DeciderLine;

  it('says Ask Daoris\'s acts are read, or not read while its reader is off, in both languages', async () => {
    const name = () => 'Second check';
    await i18n.changeLanguage('en');
    expect(deciderText(i18n.t, line('agent', ['ask-daoris.report']), name)).toBe("Ask Daoris's acts: read by Second check");
    expect(deciderText(i18n.t, line('rules', ['ask-daoris.report']), name)).toBe("Ask Daoris's acts: not read (Second check off)");
    await i18n.changeLanguage('zh');
    expect(deciderText(i18n.t, line('agent', ['ask-daoris.report']), () => '复核')).toBe('问道衍的操作：由 复核 复核');
  });

  it('is never offered or listed below the machine', () => {
    const catalogue = { roles: [], works: [], kept: true } as unknown as RoleCatalogue;
    const deciders = { places: [{ place: 'machine', kinds: [line('rules', [])] }], elsewhere: [] } as unknown as DecidersRead;

    expect(offeredKinds(catalogue, { kind: 'machine' })).toContain('ask-daoris');
    expect(offeredKinds(catalogue, { kind: 'workspace', id: 'games' })).not.toContain('ask-daoris');
    expect(decidersAt(deciders, { workspace: 'games' } as never).map((each) => each.kind)).toEqual([]);
    expect(decidersAt(deciders, {} as never).map((each) => each.kind)).toEqual(['ask-daoris']);
  });
});
