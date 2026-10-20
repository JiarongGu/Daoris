import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../locales/i18n';
import { HandOverField, type HandOverRule, handOverRowSays, handOverSays, handOverSummary, handOverToast } from './HandOver';

// MIXAGENT1c (D163 point 3): what an agent's ask to hand its session over does, set on a repository's Setup and on a workspace's
// Defaults as `daoris driver handover` sets it from a terminal (D50). Each mode's sentence is the terminal's (`handOverModeSays` in
// the CLI's `handover.ts`, the driver's `HandOverRules.Says`), and the field sends the twins' edit, which the driver judges.

const t = i18n.t.bind(i18n);

const draw = (set: HandOverRule | undefined, inherited: HandOverRule = { mode: 'ask' }, onChange = vi.fn()) => {
  render(
    <Tooltip.Provider>
      <HandOverField name="web-app" set={set} inherited={inherited} onChange={onChange} />
    </Tooltip.Provider>,
  );
  return onChange;
};

describe('what a hand-over rule says', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it("says each mode in the terminal's words, where it was set, and folds to its mode", () => {
    expect(handOverSays(t, { mode: 'ask' })).toBe('An agent’s ask to hand its session over waits for your yes or no on the session’s page.');
    expect(handOverSays(t, { mode: 'on' }))
      .toBe('An agent’s ask to hand its session over is taken when its turn ends: it goes on with the next agent in your order.');
    expect(handOverSays(t, { mode: 'off' })).toBe('An agent’s ask to hand its session over is refused: only your own press hands one over.');
    // A mode this page does not know is said as the default's, never as a guess.
    expect(handOverSays(t, { mode: 'sometimes' })).toBe(handOverSays(t, { mode: 'ask' }));
    expect(handOverRowSays(t, { mode: 'off' }, { source: 'workspace', workspace: 'work' }))
      .toBe('An agent’s ask to hand its session over is refused: only your own press hands one over. From the workspace work.');
    expect(handOverRowSays(t, { mode: 'ask' }, { source: 'default' })).toContain('Daoris’s default: nothing is set.');
    expect([handOverSummary(t, { mode: 'ask' }), handOverSummary(t, { mode: 'on' }), handOverSummary(t, { mode: 'off' })])
      .toEqual(['hand-overs wait for your yes', 'hand-overs taken at the turn’s end', 'hand-overs refused']);
  });

  it('toasts what a change did', () => {
    expect(handOverToast(t, 'web-app', { set: { mode: 'on' } }))
      .toBe('An agent in web-app asking to hand its session over now goes on with the next agent when its turn ends.');
    expect(handOverToast(t, 'web-app', { clear: true })).toBe('web-app takes its hand-over rule from what stands above it again.');
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    expect(handOverSays(t, { mode: 'off' })).toBe('智能体要求转交会话会被拒绝：只有你自己按下才会转交。');
  });
});

describe('the hand-over field', () => {
  it("sends the twins' edit for the mode pressed, starting from what stands", async () => {
    const onChange = draw(undefined, { mode: 'off' });
    expect(screen.getByRole('radio', { name: 'Refuse' })).toBeChecked();

    await userEvent.click(screen.getByRole('radio', { name: 'At turn’s end' }));

    expect(onChange).toHaveBeenCalledWith({ set: { mode: 'on' } });
  });

  it('sends nothing for the mode set here already', async () => {
    const onChange = draw({ mode: 'on' });

    await userEvent.click(screen.getByRole('radio', { name: 'At turn’s end' }));

    expect(onChange).not.toHaveBeenCalled();
  });
});
