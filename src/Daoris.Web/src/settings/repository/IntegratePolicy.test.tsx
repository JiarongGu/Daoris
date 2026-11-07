import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { LinePolicyField, policySays } from './IntegratePolicy';

// The Line row shows and edits the line's policy, the same choices the Landing tab's integrate group offers.

afterEach(async () => {
  await act(async () => { await i18n.changeLanguage('en'); });
});

describe("a line's policy", () => {
  it('sets only the choices made, as SET_LINE takes them, and offers none of the unit\'s own (request, conflicts)', async () => {
    const onSave = vi.fn();
    render(<LinePolicyField name="engine" onSave={onSave} />);

    expect(screen.queryByRole('combobox', { name: /Who asks for the integration/ })).toBeNull();
    expect(screen.queryByRole('combobox', { name: /conflict of meaning/ })).toBeNull();
    const user = userEvent.setup();
    screen.getByRole('combobox', { name: "The order engine's work is integrated in" }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('option', { name: 'Oldest first' }));
    await user.type(screen.getByRole('textbox', { name: 'Members per candidate in engine' }), '4');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(onSave).toHaveBeenCalledWith({ batch: 4, order: 'arrival' });
  });

  it('withdraws the policy that stands, and says it in a line', async () => {
    const onSave = vi.fn();
    const set = { batch: 4, order: 'arrival', gates: 'declared' };
    render(<LinePolicyField name="engine" set={set} onSave={onSave} />);

    await userEvent.click(screen.getByRole('button', { name: 'Clear' }));

    expect(onSave).toHaveBeenCalledWith(null);
    expect(policySays(i18n.t.bind(i18n), set)).toBe('Integrated into the line: batch 4, oldest first, gates declared.');
  });
});
