import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import '../../locales/i18n';
import { PanelComposer } from './PanelComposer';

// The composer at the panel's foot: it names the model a default resolves to, says when the agent reads the words, and sends
// them with one press or Ctrl Enter, emptied after.

describe('PanelComposer', () => {
  it('sends the words with its press and empties itself', () => {
    const onSend = vi.fn();
    render(<PanelComposer reads="next" chip="Claude · opus-4.1" onSend={onSend} />);
    const box = screen.getByRole('textbox', { name: 'Message the agent' }) as HTMLTextAreaElement;
    expect((screen.getByRole('button', { name: /Send/ }) as HTMLButtonElement).disabled).toBe(true);
    fireEvent.change(box, { target: { value: 'Keep the burst at 20.' } });
    fireEvent.click(screen.getByRole('button', { name: /Send/ }));
    expect(onSend).toHaveBeenCalledWith('Keep the burst at 20.');
    expect(box.value).toBe('');
  });

  it('sends on Ctrl Enter and never on Enter alone', () => {
    const onSend = vi.fn();
    render(<PanelComposer reads="now" chip="Claude · opus-4.1" onSend={onSend} />);
    const box = screen.getByRole('textbox', { name: 'Message the agent' });
    fireEvent.change(box, { target: { value: 'Stop after the tests.' } });
    fireEvent.keyDown(box, { key: 'Enter' });
    expect(onSend).not.toHaveBeenCalled();
    fireEvent.keyDown(box, { key: 'Enter', ctrlKey: true });
    expect(onSend).toHaveBeenCalledWith('Stop after the tests.');
  });

  it('says when the agent reads the words, and offers to go on once the work is finished', () => {
    const { rerender } = render(<PanelComposer reads="next" chip="Claude · opus-4.1" onSend={() => {}} />);
    expect(screen.getByText('It reads your words after the current step.')).toBeTruthy();
    expect(screen.getByText('Claude · opus-4.1')).toBeTruthy();
    rerender(<PanelComposer reads="finished" chip="Claude · opus-4.1" onSend={() => {}} />);
    expect(screen.getByPlaceholderText('Go on with this work…')).toBeTruthy();
  });
});
