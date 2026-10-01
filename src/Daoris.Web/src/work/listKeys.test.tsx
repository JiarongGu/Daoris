import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { LIST_ROW, nextRow, useListKeys } from './listKeys';

// D118 §3e (audit A7): no list in the application moved by arrow keys, so a rail was walked by Tab.

describe('the row a key moves to', () => {
  it('steps down and up, and stops at either end rather than wrapping', () => {
    expect(nextRow(3, 0, 'ArrowDown')).toBe(1);
    expect(nextRow(3, 2, 'ArrowDown')).toBe(2);
    expect(nextRow(3, 1, 'ArrowUp')).toBe(0);
    expect(nextRow(3, 0, 'ArrowUp')).toBe(0);
  });

  it('goes to either end on Home and End', () => {
    expect(nextRow(5, 2, 'Home')).toBe(0);
    expect(nextRow(5, 2, 'End')).toBe(4);
  });

  it('enters the list at its first row going down and its last going up, from outside it', () => {
    expect(nextRow(4, null, 'ArrowDown')).toBe(0);
    expect(nextRow(4, -1, 'ArrowUp')).toBe(3);
    expect(nextRow(4, 9, 'ArrowDown')).toBe(0);
  });

  it('moves nowhere in an empty list, and on a key that is not a move', () => {
    expect(nextRow(0, null, 'ArrowDown')).toBeNull();
    expect(nextRow(3, 1, 'Enter')).toBeNull();
    expect(nextRow(3, 1, 'a')).toBeNull();
  });
});

/** A list the way a rail draws one: a search box, then rows, each a button and a menu of its own. */
function List() {
  const keys = useListKeys();
  return (
    // A key handler on a group: the rows and the box inside it are what take focus.
    <div onKeyDown={keys}>
      <input aria-label="search" />
      <ul>
        {['one', 'two', 'three'].map((name) => (
          <li key={name} {...{ [LIST_ROW]: '' }}>
            <button type="button">{name}</button>
            <button type="button">{`more for ${name}`}</button>
          </li>
        ))}
      </ul>
    </div>
  );
}

describe('a list bound to its keys', () => {
  it('moves the focus between rows with the arrows, and to either end with Home and End', async () => {
    render(<List />);
    screen.getByRole('button', { name: 'one' }).focus();

    await userEvent.keyboard('{ArrowDown}');
    expect(screen.getByRole('button', { name: 'two' })).toHaveFocus();
    await userEvent.keyboard('{End}');
    expect(screen.getByRole('button', { name: 'three' })).toHaveFocus();
    await userEvent.keyboard('{ArrowUp}');
    expect(screen.getByRole('button', { name: 'two' })).toHaveFocus();
    await userEvent.keyboard('{Home}');
    expect(screen.getByRole('button', { name: 'one' })).toHaveFocus();
  });

  it('keeps a row\'s own menu a Tab away, and moves on from it by the arrows', async () => {
    render(<List />);
    screen.getByRole('button', { name: 'one' }).focus();

    await userEvent.tab();
    expect(screen.getByRole('button', { name: 'more for one' })).toHaveFocus();
    await userEvent.keyboard('{ArrowDown}');
    expect(screen.getByRole('button', { name: 'two' })).toHaveFocus();
  });

  it('goes down from the search box into the list, and leaves the box its own Home and End', async () => {
    render(<List />);
    const box = screen.getByRole('textbox', { name: 'search' });
    await userEvent.type(box, 'on');

    await userEvent.keyboard('{Home}');
    expect(box).toHaveFocus();
    await userEvent.keyboard('{ArrowDown}');
    expect(screen.getByRole('button', { name: 'one' })).toHaveFocus();
  });

  it('leaves a key with a modifier to whoever owns it', async () => {
    render(<List />);
    screen.getByRole('button', { name: 'one' }).focus();

    await userEvent.keyboard('{Control>}{ArrowDown}{/Control}');
    expect(screen.getByRole('button', { name: 'one' })).toHaveFocus();
  });
});
