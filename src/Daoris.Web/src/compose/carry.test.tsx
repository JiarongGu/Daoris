import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, renderHook, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '../i18n';
import { CarryFields, NO_CARRY, useCarry, type Carry } from './carry';

// What a composer carries beside its words (D65 §2), shared by the quest composer and the ask
// composer so the two cannot drift on how a file arrives: dropped anywhere on the form, pasted as a
// screenshot, or chosen.

const file = (name: string, bytes = 10) => new File([new Uint8Array(bytes)], name);

/** The fields as a composer holds them — the carry in the parent, the hook beside the form. */
function Composer({ onCarry }: { onCarry?: (carry: Carry) => void }) {
  const [carry, setCarry] = useState<Carry>(NO_CARRY);
  const change = (next: Carry) => { setCarry(next); onCarry?.(next); };
  const zone = useCarry(carry, change, true);
  return (
    <form aria-label="composer" {...zone.handlers}>
      <CarryFields
        carry={carry} filesLabel="files" leftOff={zone.leftOff} dragging={zone.dragging}
        onLinks={(links) => change({ ...carry, links })} onAttach={zone.attach} onRemove={zone.remove}
      />
    </form>
  );
}

describe('CarryFields', () => {
  it('takes a chosen file and lists it, and removes it again', async () => {
    render(<Composer />);

    await userEvent.upload(screen.getByLabelText('Choose files…'), file('before.png'));
    expect(screen.getByText('before.png')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Remove before.png' }));
    expect(screen.queryByText('before.png')).toBeNull();
  });

  it('takes a file dropped anywhere on the form, and ignores a drag of text', () => {
    const onCarry = vi.fn();
    render(<Composer onCarry={onCarry} />);
    const form = screen.getByRole('form', { name: 'composer' });

    fireEvent.drop(form, { dataTransfer: { types: ['text/plain'], files: [] } });
    expect(onCarry).not.toHaveBeenCalled();

    fireEvent.drop(form, { dataTransfer: { types: ['Files'], files: [file('dropped.log')] } });
    expect(screen.getByText('dropped.log')).toBeInTheDocument();
  });

  /** A screenshot on the clipboard is a file, not text: attached, and kept out of the field. */
  it('attaches a pasted screenshot', () => {
    render(<Composer />);

    fireEvent.paste(screen.getByRole('form', { name: 'composer' }), { clipboardData: { files: [file('shot.png')] } });

    expect(screen.getByText('shot.png')).toBeInTheDocument();
  });

  it('says what a drop left off, while the person is still choosing', async () => {
    render(<Composer />);

    await userEvent.upload(screen.getByLabelText('Choose files…'), Array.from({ length: 11 }, (_, i) => file(`f${i}.txt`)));

    expect(screen.getByRole('status')).toHaveTextContent('At most 10 files travel together.');
    expect(screen.getAllByRole('button', { name: /^Remove / })).toHaveLength(10);
  });

  /**
   * 🔴 A file dropped outside the form must not become the page: a webview answers an unhandled drop
   * by NAVIGATING to the file. While a composer is open, a stray drop is absorbed.
   */
  it('absorbs a stray drop while active, and stops when it is not', () => {
    const stray = () => {
      const event = new Event('drop', { cancelable: true });
      Object.defineProperty(event, 'dataTransfer', { value: { types: ['Files'] } });
      window.dispatchEvent(event);
      return event.defaultPrevented;
    };
    const { rerender } = renderHook(({ active }) => useCarry(NO_CARRY, () => {}, active), {
      initialProps: { active: true },
    });
    expect(stray()).toBe(true);

    act(() => rerender({ active: false }));
    expect(stray()).toBe(false);
  });
});
