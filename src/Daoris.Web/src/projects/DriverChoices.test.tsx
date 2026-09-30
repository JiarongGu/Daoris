import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { DriverChoices } from './DriverChoices';

// A repository's standing choices for THIS machine's driver (D46 §6) — the same row on an adopter's
// card and, since INT3c, on an unadopted repository with a root here (D70). Props only: the view
// above holds the driver's state and its mutations.

const show = (props: Partial<Parameters<typeof DriverChoices>[0]> = {}) => {
  const handlers = { onDrive: vi.fn(), onHold: vi.fn(), onTrees: vi.fn() };
  render(
    <Tooltip.Provider>
      <DriverChoices drivable={false} held={false} ownTree={false} {...handlers} {...props} />
    </Tooltip.Provider>,
  );
  return handlers;
};

describe('the driver choices', () => {
  it('offers driving and its own tree, and says what was chosen', async () => {
    const { onDrive, onTrees } = show();

    await userEvent.click(screen.getByLabelText('Drive on this machine'));
    await userEvent.click(screen.getByLabelText('A tree per session'));

    expect(onDrive).toHaveBeenCalledWith(true);
    expect(onTrees).toHaveBeenCalledWith(true);
  });

  it('offers a hold only once the repository is driven — a hold on nothing is noise', () => {
    show();
    expect(screen.queryByLabelText('Hold')).toBeNull();
  });

  it('holds a driven repository', async () => {
    const { onHold } = show({ drivable: true });

    await userEvent.click(screen.getByLabelText('Hold'));

    expect(onHold).toHaveBeenCalledWith(true);
  });

  /** INT3c: a note beside the choices, when the view has a fact to add about what driving here does. */
  it('carries a note under the choices when the view has one', () => {
    show({ note: 'This machine drives on a direct agent.' });
    expect(screen.getByText('This machine drives on a direct agent.')).toBeInTheDocument();
  });

  it('carries the view\'s own action at the end of the row', () => {
    show({ action: <button type="button">Manage</button> });
    expect(screen.getByRole('button', { name: 'Manage' })).toBeInTheDocument();
  });
});
