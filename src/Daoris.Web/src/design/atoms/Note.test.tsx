import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Note } from './Note';

describe('the note (the window design §7.4: a note wears no edge, its tone is its ink and its mark)', () => {
  it('says a refusal in the failure ink with the mark ahead of the words', () => {
    render(<Note tone="fail" role="alert">It could not be written.</Note>);
    const note = screen.getByRole('alert');
    expect(note).toHaveClass('text-fail-ink');
    expect(note.querySelector('svg')).not.toBeNull();
    expect(note).toHaveTextContent('It could not be written.');
    expect(note.className).not.toMatch(/border/);
  });

  it("says the person's own wait in the waiting ink, a caution in the plain ink with its mark, and a remark with neither", () => {
    render(<><Note tone="you">waits</Note><Note tone="caution">careful</Note><Note tone="plain">just so</Note></>);
    expect(screen.getByText('waits').closest('p')).toHaveClass('text-you-ink');
    expect(screen.getByText('careful').closest('p')).toHaveClass('text-ink');
    expect(screen.getByText('careful').closest('p')!.querySelector('svg')).not.toBeNull();
    expect(screen.getByText('just so').closest('p')).toHaveClass('text-ink-soft');
    expect(screen.getByText('just so').closest('p')!.querySelector('svg')).toBeNull();
  });
});
