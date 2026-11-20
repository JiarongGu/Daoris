import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as draw, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactElement } from 'react';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { StepHoldPresses, stepHoldAsk } from '../../workflow/wait/StepHold';
import { PlaceCastSetting, type PlaceCastView } from './PlaceCast';

const render = (ui: ReactElement) => draw(<Tooltip.Provider>{ui}</Tooltip.Provider>);

const view: PlaceCastView = {
  place: 'workspace/work', risk: 'medium', riskSource: 'own', marks: ['release.production'],
  roles: [
    { role: 'acceptor', holder: 'agent:codex', why: 'cast', source: 'own', words: 'low-risk docs' },
    { role: 'approver', holder: 'person', why: 'marked', source: 'default' },
    { role: 'owner', holder: 'person', why: 'owner', source: 'default' },
    { role: 'tester', holder: 'agent', why: 'default', source: 'default' },
  ],
  roster: ['person', 'agent', 'agent:codex'],
  line: [{ level: 'workspace-supervisor', role: 'supervisor', holder: 'agent', why: '' }, { level: 'owner', holder: 'person', why: '' }],
};

afterEach(() => i18n.changeLanguage('en'));

describe('a place’s cast in its Settings', () => {
  it('says each role’s holder and why, the risk and marks, the roster and the line, and a choice casts with the reason', async () => {
    const onCast = vi.fn();
    render(<PlaceCastSetting name="work" twin="twin" view={view} onCast={onCast} />);
    expect(screen.getByText(/Chosen\. Set for this place\. low-risk docs/)).toBeInTheDocument();
    expect(screen.getByText(/Your mark keeps production releases yours\./)).toBeInTheDocument();
    expect(screen.getByText(/Nobody is chosen, so the role’s agent fills it until a choice names another\./)).toBeInTheDocument();
    expect(screen.queryByText(/Nobody else is chosen/)).toBeNull();
    expect(screen.getByText(/Who can fill a role: you, the role’s agent/)).toBeInTheDocument();
    expect(screen.getByText(/reports to the workspace’s supervisor/)).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Who fills owner' })).toBeNull();

    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'I sign these' } });
    await userEvent.click(screen.getByRole('combobox', { name: 'Who fills approver' }));
    await userEvent.click(await screen.findByRole('option', { name: 'you' }));
    expect(onCast).toHaveBeenCalledWith({ role: 'approver', holder: 'person', why: 'I sign these' });
  });

  it('says the saved casts do not read, keeps the last good reading and offers no choice', () => {
    render(<PlaceCastSetting name="work" twin="twin" view={{ ...view, unreadable: 'the cast of `acceptor` does not read' }} onCast={vi.fn()} />);
    expect(screen.getByRole('alert')).toHaveTextContent(/do not read.*last good reading.*the cast of acceptor does not read/);
    expect(screen.getByRole('combobox', { name: /acceptor/ })).toBeDisabled();
  });

  it('says it in Chinese with the glossary’s words', async () => {
    await i18n.changeLanguage('zh');
    render(<PlaceCastSetting name="work" twin="twin" view={view} onCast={vi.fn()} />);
    expect(screen.getByText('角色及其担任者')).toBeInTheDocument();
    expect(screen.getByText(/可以担任角色的：你、该角色的智能体/)).toBeInTheDocument();
    expect(screen.getByText(/未选定担任者，所以由该角色的智能体担任，直到选定其他担任者。/)).toBeInTheDocument();
  });
});

describe('a step’s holder presses', () => {
  it('takes, hands to one of the roster and gives back', () => {
    const onHold = vi.fn();
    render(<StepHoldPresses holds={['take', 'hand', 'give-back']} heldBy="agent:codex" roster={['person', 'agent:codex', 'agent:beta']} onHold={onHold} />);
    expect(screen.getByText('Filled by `codex`')).toBeInTheDocument();
    screen.getByRole('button', { name: 'Take' }).click();
    expect(onHold).toHaveBeenLastCalledWith('take');
    fireEvent.click(screen.getByRole('button', { name: 'Pass to…' }));
    fireEvent.click(screen.getByRole('button', { name: '`beta`' }));
    expect(onHold).toHaveBeenLastCalledWith('hand', 'agent:beta');
    screen.getByRole('button', { name: 'Give back' }).click();
    expect(onHold).toHaveBeenLastCalledWith('give-back');
  });

  it('sends the epoch its row read, so a step passed on since moves nothing', () => {
    expect(stepHoldAsk('q1', 'release', 'hand', 'agent:beta', 2)).toEqual({ run: 'q1', node: 'release', act: 'hand', holder: 'agent:beta', epoch: 2 });
    expect(stepHoldAsk('q1', 'release', 'take', null, null)).not.toHaveProperty('epoch');
  });

  it('says them in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<StepHoldPresses holds={['take', 'hand']} roster={[]} onHold={vi.fn()} />);
    expect(screen.getByRole('button', { name: '接下' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '转给…' })).toBeInTheDocument();
  });
});
