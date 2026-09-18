import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Button, EmptyState, Pill, Tile } from './ui';

describe('the primitives', () => {
  it('a pill always carries its text label — status never rides on hue alone', () => {
    render(<Pill tone="declined" title="turned down">Declined</Pill>);
    const pill = screen.getByText('Declined');
    expect(pill).toHaveAttribute('title', 'turned down');
    expect(pill.className).toContain('st-declined');
  });

  it('a warned tile wears the warning on its note, never on its value', () => {
    render(<Tile label="Open quests" value={3} note="oldest has sat 12d" warn />);
    expect(screen.getByText('oldest has sat 12d').className).toContain('st-open');
    expect(screen.getByText('3').className).not.toContain('st-open');
  });

  it('an empty state offers the action that would change the fact', () => {
    render(
      <EmptyState
        icon="inbox"
        headline="No open quests anywhere"
        body="The family owes itself nothing right now."
        action={<Button>ask for something</Button>}
      />,
    );
    expect(screen.getByRole('button', { name: 'ask for something' })).toBeInTheDocument();
  });

  it('a disabled primary stays announced as disabled', () => {
    render(<Button variant="primary" disabled>publish quest</Button>);
    expect(screen.getByRole('button', { name: 'publish quest' })).toBeDisabled();
  });
});
