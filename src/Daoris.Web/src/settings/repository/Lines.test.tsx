import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { LineField } from './Lines';

// WSR2: a line's field, set as `daoris driver line` sets it from a terminal (D50). One field, on a workspace's Setup and a
// repository's (UX6f, UX6g; D150 §4.2, §4.3), so its behaviour is held here.

const draw = (set: string | undefined, onSave = vi.fn(), clearable = true) => {
  render(
    <Tooltip.Provider>
      <LineField label="The line for aurora" set={set} placeholder="each checkout's own" clearable={clearable} onSave={onSave} />
    </Tooltip.Provider>,
  );
  return onSave;
};

describe("a line's field", () => {
  it('holds only what is SET, with what would stand as its placeholder, and offers a clear only where something is', () => {
    draw(undefined);

    const field = screen.getByRole('textbox', { name: 'The line for aurora' });
    expect(field).toHaveValue('');
    expect(field).toHaveAttribute('placeholder', "each checkout's own");
    // The clear keeps its room, hidden, so every row's field sits in one column.
    expect(screen.queryByRole('button', { name: 'Clear' })).toBeNull();
  });

  it('sets a line, and clears one', async () => {
    const onSave = draw('develop');
    const user = userEvent.setup();

    const field = screen.getByRole('textbox', { name: 'The line for aurora' });
    expect(field).toHaveValue('develop');
    await user.clear(field);
    await user.type(field, 'trunk{Enter}');
    expect(onSave).toHaveBeenLastCalledWith('trunk');

    await user.click(screen.getByRole('button', { name: 'Clear' }));
    expect(onSave).toHaveBeenLastCalledWith(undefined);
  });

  it('does not send what is already set, or nothing at all', async () => {
    const onSave = draw('develop');

    await userEvent.setup().type(screen.getByRole('textbox', { name: 'The line for aurora' }), '{Enter}');
    expect(onSave).not.toHaveBeenCalled();
  });

  /** A Setup row gives each value its own *Clear* beside the field (UX6f), so there the field carries none. */
  it('carries no clear of its own where its row has one', () => {
    draw('develop', vi.fn(), false);
    expect(screen.queryByRole('button', { name: 'Clear', hidden: true })).toBeNull();
  });
});
