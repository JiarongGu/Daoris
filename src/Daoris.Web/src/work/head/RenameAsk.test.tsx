import { describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { RenameAsk } from './RenameAsk';
import { DeleteAsk } from './SessionPageHead';

// The two asks under the header that speak of a conversation's whole line: its name, and what a delete takes with it.

describe('the rename ask', () => {
  it('starts from the title shown and saves the new name, told how it ended', async () => {
    const onSave = vi.fn();
    const onClose = vi.fn();
    render(<RenameAsk title="how do I drive engine?" named={false} onSave={onSave} onClose={onClose} />);

    const field = screen.getByRole('textbox', { name: 'Name' });
    expect(field).toHaveValue('how do I drive engine?');
    // Unchanged, there is nothing to save; a named conversation could be given its first words back, an unnamed one cannot.
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Use its first words' })).toBeNull();

    await userEvent.clear(field);
    await userEvent.type(field, 'Workspaces');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onSave).toHaveBeenCalledWith('Workspaces', expect.objectContaining({ done: expect.any(Function), refused: expect.any(Function) }));
    onSave.mock.calls[0][1].done();
    expect(onClose).toHaveBeenCalled();
  });

  it('gives a named conversation its first words back, saying a refusal where it is pressed', async () => {
    const onSave = vi.fn();
    render(<RenameAsk title="Workspaces" named onSave={onSave} onClose={() => {}} />);

    await userEvent.click(screen.getByRole('button', { name: 'Use its first words' }));

    expect(onSave).toHaveBeenCalledWith(null, expect.anything());
    act(() => onSave.mock.calls[0][1].refused('no conversation here is s1 any more.'));
    expect(await screen.findByRole('alert')).toHaveTextContent('no conversation here is s1 any more.');
  });

  it('is said in Chinese too', async () => {
    await i18n.changeLanguage('zh');
    render(<RenameAsk title="Workspaces" named onSave={() => {}} onClose={() => {}} />);
    expect(screen.getByRole('button', { name: '改回用开头的话' })).toBeInTheDocument();
    await i18n.changeLanguage('en');
  });
});

describe('the delete ask', () => {
  it('says how many parts a delete takes where its conversation has more than one, and says the one conversation alone otherwise', () => {
    const { rerender } = render(<DeleteAsk onDelete={() => {}} onClose={() => {}} />);
    expect(screen.getByText(/Deletes this conversation’s record/)).toBeInTheDocument();

    rerender(<DeleteAsk parts={3} onDelete={() => {}} onClose={() => {}} />);
    expect(screen.getByText(/Deletes all 3 parts of this conversation/)).toBeInTheDocument();
  });
});
