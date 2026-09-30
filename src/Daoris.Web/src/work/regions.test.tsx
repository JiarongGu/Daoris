import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { LIST_ROW } from './listKeys';
import { focusRegion, nextRegion, REGION } from './regions';

// D118 §3e: F6 and Shift+F6 focus the next and the previous region, VS Code's *Focus Next Part*.

describe('the region F6 moves to', () => {
  it('steps forward and back, and goes round, as VS Code\'s parts do', () => {
    expect(nextRegion(4, 0, false)).toBe(1);
    expect(nextRegion(4, 3, false)).toBe(0);
    expect(nextRegion(4, 0, true)).toBe(3);
    expect(nextRegion(4, 2, true)).toBe(1);
  });

  it('starts at the first going forward and the last going back, from outside every region', () => {
    expect(nextRegion(4, null, false)).toBe(0);
    expect(nextRegion(4, -1, true)).toBe(3);
  });

  it('goes nowhere where there is no region', () => {
    expect(nextRegion(0, null, false)).toBeNull();
  });
});

/** The frame's regions as the window draws them, in its order. */
function Frame({ empty = false, chosen = true }: { empty?: boolean; chosen?: boolean }) {
  return (
    <div>
      <nav {...{ [REGION]: 'activity' }} aria-label="Views"><button type="button">Overview</button></nav>
      <aside {...{ [REGION]: 'list' }}>
        <button type="button">Start a session</button>
        <ul>
          <li {...{ [LIST_ROW]: '' }}><button type="button">first session</button></li>
          <li {...{ [LIST_ROW]: '' }}><button type="button" aria-current={chosen || undefined}>attended session</button></li>
        </ul>
      </aside>
      <main {...{ [REGION]: 'main' }} aria-label="main area">{empty ? <p>nothing to press</p> : <button type="button">head</button>}</main>
      <footer {...{ [REGION]: 'status' }}><button type="button">driver</button></footer>
    </div>
  );
}

describe('focusing a region', () => {
  it('focuses the next region\'s chosen item where it has one, else its first control', () => {
    render(<Frame />);
    screen.getByRole('button', { name: 'Overview' }).focus();

    focusRegion(document, false);
    expect(screen.getByRole('button', { name: 'attended session' })).toHaveFocus();
    focusRegion(document, false);
    expect(screen.getByRole('button', { name: 'head' })).toHaveFocus();
    focusRegion(document, true);
    expect(screen.getByRole('button', { name: 'attended session' })).toHaveFocus();
  });

  it('focuses a list\'s first row where nothing in it is chosen, not the controls in its header', () => {
    render(<Frame chosen={false} />);
    screen.getByRole('button', { name: 'Overview' }).focus();

    focusRegion(document, false);
    expect(screen.getByRole('button', { name: 'first session' })).toHaveFocus();
  });

  it('goes round from the last region to the first, and in at the first from outside every one', () => {
    render(<Frame />);
    focusRegion(document, false);
    expect(screen.getByRole('button', { name: 'Overview' })).toHaveFocus();

    screen.getByRole('button', { name: 'driver' }).focus();
    focusRegion(document, false);
    expect(screen.getByRole('button', { name: 'Overview' })).toHaveFocus();
  });

  it('focuses a region with nothing to press as itself, so the next F6 goes on from it', () => {
    render(<Frame empty />);
    screen.getByRole('button', { name: 'attended session' }).focus();

    focusRegion(document, false);
    const main = screen.getByRole('main', { name: 'main area' });
    expect(main).toHaveFocus();
    focusRegion(document, false);
    expect(screen.getByRole('button', { name: 'driver' })).toHaveFocus();
  });
});
