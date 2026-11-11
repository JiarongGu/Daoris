import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { type PressView, PressViewLine } from '../PressViewLine';

const GATEKEEPER: PressView = {
  kind: 'role-supervisor-set',
  facts: { role: 'gatekeeper', name: 'Gatekeeper · 守门人' },
  command: 'daoris role set gatekeeper --name Gatekeeper --name-in zh=守门人 --scope workspace:w1 --decides allow,landing --acts allow.once --holder terminal --window 10',
  supervisor: {
    id: 'gatekeeper', names: ['Gatekeeper', '守门人'], scope: { kind: 'workspace', id: 'w1' },
    hold: { kind: 'supervisor', acts: ['allow.once'], holder: 'terminal', window: 10, decides: ['allow', 'landing'] },
    before: null,
  },
};

const GENERIC: PressView = {
  ...GATEKEEPER,
  supervisor: {
    id: 'wide', names: ['Wide'], scope: { kind: 'machine', id: null },
    hold: { kind: 'supervisor', acts: [], holder: 'rules', window: 1, decides: [] },
    before: { kind: 'supervisor', acts: ['question.answer'], holder: 'terminal', window: 5, decides: [] },
  },
};

describe('a supervisor set on the Confirm card', () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('words its kinds, holder, acts and window from the catalogue in English, with the command', () => {
    render(<PressViewLine view={GATEKEEPER} />);

    expect(screen.getByText('Sets supervisor gatekeeper (Gatekeeper · 守门人): what it decides, who holds it, the acts it is granted and its window, as below. Running work goes on.')).toBeInTheDocument();
    expect(screen.getByText('Supervisor: Gatekeeper · 守门人, for Workspace w1')).toBeInTheDocument();
    expect(screen.getByText('Not set here yet')).toBeInTheDocument();
    const after = within(screen.getByRole('region', { name: 'After you confirm' }));
    expect(after.getByText(
      'Decides: Allows and Landings · Held by a terminal · allow a call once · a decision is held 10 minutes for your overrule',
    )).toBeInTheDocument();
    expect(screen.getByText(GATEKEEPER.command)).toBeInTheDocument();
  });

  it('says a generic supervisor as every decision, and what it was before', () => {
    render(<PressViewLine view={GENERIC} />);

    const now = within(screen.getByRole('region', { name: 'Now' }));
    expect(now.getByText(
      'Decides: Every decision no other supervisor here takes · Held by a terminal · answer a question · a decision is held 5 minutes for your overrule',
    )).toBeInTheDocument();
    const after = within(screen.getByRole('region', { name: 'After you confirm' }));
    expect(after.getByText(
      'Decides: Every decision no other supervisor here takes · Held by your rules, then you · no acts · a decision is held 1 minute for your overrule',
    )).toBeInTheDocument();
  });

  it('words it in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<PressViewLine view={GATEKEEPER} />);

    expect(screen.getByText('主管：Gatekeeper · 守门人，适用于工作区 w1')).toBeInTheDocument();
    const after = within(screen.getByRole('region', { name: '确认之后' }));
    expect(after.getByText('决定：允许和落地 · 担任者：终端 · 允许这一次调用 · 决定会保留 10 分钟，供你否决')).toBeInTheDocument();
  });
});
