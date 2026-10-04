import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import '../i18n';
import { code } from '../test/code';
import { StandingAnswer } from './StandingAnswer';

// A repository's standing answer on this machine (KNOWUSE1b, D135 §3): the person's words, handed to every session there,
// on the repository's page beside the driver's other choices. Props only: the view above holds the state and its mutation.

const hoursAgo = (count: number) => new Date(Date.now() - count * 3_600_000).toISOString();

const show = (props: Partial<Parameters<typeof StandingAnswer>[0]> = {}) => {
  const onSave = vi.fn();
  render(<Tooltip.Provider><StandingAnswer repository="engine" says={null} onSave={onSave} {...props} /></Tooltip.Provider>);
  return onSave;
};

describe('a standing answer', () => {
  it('says there is none and what one is for, and keeps one in the person\'s words', async () => {
    const onSave = show();

    expect(screen.getByText(/None yet/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'Standing answer' }), '  dev writes allowed; prod only on a yes  ');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onSave).toHaveBeenCalledWith('dev writes allowed; prod only on a yes');
  });

  it('shows the words verbatim with when they were set, and edits them in place', async () => {
    const onSave = show({ says: 'dev writes allowed; test locally against dev; prod only on a yes', at: hoursAgo(2) });

    expect(screen.getByText('dev writes allowed; test locally against dev; prod only on a yes')).toBeInTheDocument();
    expect(screen.getByText('set 2h ago')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Edit' }));
    const field = screen.getByRole('textbox', { name: 'Standing answer' });
    expect(field).toHaveValue('dev writes allowed; test locally against dev; prod only on a yes');
    await userEvent.clear(field);
    await userEvent.type(field, 'dev only');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onSave).toHaveBeenCalledWith('dev only');
  });

  it('clears it, and offers no save for blank words', async () => {
    const onSave = show({ says: 'dev only' });

    await userEvent.click(screen.getByRole('button', { name: 'Edit' }));
    await userEvent.clear(screen.getByRole('textbox', { name: 'Standing answer' }));
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
    await userEvent.click(screen.getByRole('button', { name: 'Never mind' }));
    await userEvent.click(screen.getByRole('button', { name: 'Clear' }));

    expect(onSave).toHaveBeenCalledTimes(1);
    expect(onSave).toHaveBeenCalledWith(null);
  });

  // UX6f: a row of the repository's Setup, its terminal twin as its hint and why it is safe on its info glyph.
  it('says its terminal twin, and on its info glyph that it is handed to every session here and never written into the repository', () => {
    show();
    expect(screen.getByText(code('daoris driver standing engine "…"|--clear'))).toBeInTheDocument();
    const why = screen.getByRole('note', { name: /handed to every session in this repository/ });
    expect(why).toHaveAccessibleName(/never written into the repository/);
  });

  it('says an answer past the bound is too long, and offers no save', async () => {
    show();

    await userEvent.click(screen.getByRole('button', { name: 'Add' }));
    const field = screen.getByRole('textbox', { name: 'Standing answer' });
    await userEvent.click(field);
    await userEvent.paste('x'.repeat(2_001));

    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
    expect(screen.getByText(/2,001 of 2,000 characters/)).toBeInTheDocument();
  });
});
