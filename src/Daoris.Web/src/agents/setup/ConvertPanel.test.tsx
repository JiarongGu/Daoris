import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// WORKFLOWCLEAN1g-i (the cleanup design §4.3-§4.5): the conversion on the page reads one plan for the row's two choices, presses Apply as the
// person's own with the plan's digest and the choices, says `moved` in the driver's words with the plan read again, and reads the undo's plan
// only once the conversion is done or the census has ended.

const { invoke, notifyReady } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => {},
}));

import i18n from '../../locales/i18n';
import { show } from '../../test/shellHarness';
import { APPLIED, MOVED, NOTHING, planOf, UNDO_OFFERED, UNDO_REFUSED, UNDONE } from './convertFixtures';
import type { ConvertApplied, ConvertChoices, ConvertPlanned, ConvertUndoPlan } from './convertPlan';
import { ConvertPanel } from './ConvertPanel';

const sent = (type: string) => invoke.mock.calls.filter(([, route]) => route === type).map(([, , options]) => (options as { payload: Record<string, unknown> }).payload);

describe('Convert…, on the page', () => {
  let after: ConvertPlanned | null = null;
  let apply: ConvertApplied = APPLIED;
  let undo: ConvertUndoPlan = UNDO_OFFERED;
  let undone: ConvertApplied = UNDONE;
  const onClose = vi.fn();

  beforeEach(() => {
    after = null;
    apply = APPLIED;
    undo = UNDO_OFFERED;
    undone = UNDONE;
    i18n.changeLanguage('en');
    invoke.mockImplementation(async (_module: string, type: string, options?: { payload?: Record<string, unknown> }) => {
      const payload = options?.payload ?? {};
      if (type === 'CONVERT_PLAN') return payload.undo ? undo : after ?? planOf(payload as unknown as ConvertChoices);
      if (type === 'CONVERT_APPLY') {
        if (apply.done) after = NOTHING;
        return apply;
      }
      if (type === 'CONVERT_UNDO') {
        after = null;
        return undone;
      }
      return undefined;
    });
  });
  afterEach(() => {
    cleanup();
    invoke.mockReset();
  });

  it('reads the plan for the default choices and again for the one pressed', async () => {
    show(<ConvertPanel onClose={onClose} />);
    await screen.findByRole('list', { name: 'What converting changes' });
    expect(sent('CONVERT_PLAN')[0]).toEqual({ composed: 'none', landAutomatic: 'review' });
    await userEvent.click(screen.getByRole('radio', { name: 'With no review, as it was saved' }));
    await waitFor(() => expect(sent('CONVERT_PLAN')).toContainEqual({ composed: 'none', landAutomatic: 'none' }));
    await screen.findByText(/lands with no review, as it was saved/);
  });

  it('presses Apply with the plan\'s digest and the choices, and reads the undo\'s plan only once it is done', async () => {
    show(<ConvertPanel onClose={onClose} />);
    await screen.findByRole('list', { name: 'What converting changes' });
    expect(sent('CONVERT_PLAN').some((each) => each.undo)).toBe(false);
    const button = screen.getByRole('button', { name: 'Apply' });
    await waitFor(() => expect(button).toBeEnabled());
    await userEvent.click(button);
    const shown = planOf({ composed: 'none', landAutomatic: 'review' });
    await waitFor(() => expect(sent('CONVERT_APPLY')).toEqual([{ composed: 'none', landAutomatic: 'review', digest: shown.digest }]));
    await screen.findByRole('status');
    await screen.findByRole('button', { name: 'Undo the conversion' });
    expect(sent('CONVERT_PLAN').some((each) => each.undo)).toBe(true);
  });

  it('undoes by the undo plan\'s digest and says the versions it saved stay', async () => {
    show(<ConvertPanel onClose={onClose} />);
    await userEvent.click(await screen.findByRole('button', { name: 'Apply' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Undo the conversion' }));
    await waitFor(() => expect(sent('CONVERT_UNDO')).toEqual([{ digest: UNDO_OFFERED.digest }]));
    await screen.findByText(/the versions it saved stay saved/);
  });

  it('offers no Undo where the driver refuses it', async () => {
    undo = UNDO_REFUSED;
    show(<ConvertPanel onClose={onClose} />);
    await userEvent.click(await screen.findByRole('button', { name: 'Apply' }));
    await screen.findByRole('status');
    await waitFor(() => expect(sent('CONVERT_PLAN').some((each) => each.undo)).toBe(true));
    expect(screen.queryByRole('button', { name: 'Undo the conversion' })).toBeNull();
  });

  it('says a moved plan in the driver\'s words and reads the plan again', async () => {
    apply = MOVED;
    show(<ConvertPanel onClose={onClose} />);
    await userEvent.click(await screen.findByRole('button', { name: 'Apply' }));
    await screen.findByText(/the plan you read no longer holds/);
    await waitFor(() => expect(sent('CONVERT_PLAN').filter((each) => !each.undo).length).toBeGreaterThan(1));
    expect(screen.queryByRole('button', { name: 'Undo the conversion' })).toBeNull();
  });

  it('reads the undo\'s plan when the census has ended, with no apply pressed', async () => {
    after = NOTHING;
    show(<ConvertPanel onClose={onClose} />);
    await screen.findByText('Nothing is left to convert.');
    await screen.findByRole('button', { name: 'Undo the conversion' });
  });
});
