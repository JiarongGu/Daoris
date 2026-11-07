import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Acceptances, type AcceptanceRow } from './Acceptances';

// The one *Acceptances* molecule Gates and the set-up's review draw: each declaration's text shown
// once, each acceptance its own tick, unticked until the person ticks it, with its standing and consequence in words.

const TEXT = 'gates: [lint, tests]\n';

const rows = (patch: Partial<AcceptanceRow> = {}): AcceptanceRow[] => [
  { id: 'landed', label: 'Accept for landed verification', said: 'Changed: verification stops until you accept it again.', offered: true, text: TEXT, textLabel: 'main tip aa11bb2', ...patch },
  { id: 'integration', label: 'Accept for integration', said: 'Integration goes on under its last accepted blob.', offered: true, text: TEXT, textLabel: 'main tip aa11bb2' },
];

const draw = (props: Partial<Parameters<typeof Acceptances>[0]> = {}) => {
  const onTick = vi.fn();
  render(<Acceptances rows={rows()} ticked={[]} onTick={onTick} {...props} />);
  return onTick;
};

describe('the Acceptances molecule', () => {
  afterEach(cleanup);

  it('draws a declaration two acceptances share once, and each acceptance with its standing', () => {
    draw();
    expect(document.querySelectorAll('pre')).toHaveLength(1);
    const list = screen.getByRole('list');
    expect(within(list).getAllByRole('listitem')).toHaveLength(2);
    expect(within(list).getByText('Changed: verification stops until you accept it again.')).toBeTruthy();
    expect(within(list).getByText('Integration goes on under its last accepted blob.')).toBeTruthy();
  });

  it('starts every tick empty and sends the id ticked or unticked', async () => {
    const onTick = draw();
    const landed = screen.getByRole('checkbox', { name: 'Accept for landed verification' });
    expect(landed).not.toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'Accept for integration' })).not.toBeChecked();
    await userEvent.click(landed);
    expect(onTick).toHaveBeenLastCalledWith('landed', true);
    cleanup();
    const again = draw({ ticked: ['landed'] });
    await userEvent.click(screen.getByRole('checkbox', { name: 'Accept for landed verification' }));
    expect(again).toHaveBeenLastCalledWith('landed', false);
  });

  it('offers no tick for what is not offered, and none while disabled', () => {
    draw({ rows: rows({ offered: false }), disabled: true });
    expect(screen.queryByRole('checkbox', { name: 'Accept for landed verification' })).toBeNull();
    expect(screen.getByText('Accept for landed verification')).toBeTruthy();
    expect(screen.getByRole('checkbox', { name: 'Accept for integration' })).toBeDisabled();
  });

  it('folds the declaration under its own words when it is asked to, and says each tick cleared with its reason', () => {
    draw({ title: 'engine · main aa11bb2', fold: 'Read the declaration', cleared: ['The declaration changed since you ticked Accept for integration, so the tick was cleared.'] });
    const fold = screen.getByText('Read the declaration').closest('details')!;
    expect(fold).toHaveTextContent('gates: [lint, tests]');
    expect(screen.getByText('engine · main aa11bb2')).toBeTruthy();
    expect(screen.getByRole('status')).toHaveTextContent('so the tick was cleared');
  });
});
