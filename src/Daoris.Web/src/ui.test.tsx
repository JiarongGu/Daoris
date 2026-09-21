import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { SessionState } from './api';
import './i18n';
import {
  Button, Dot, EmptyState, MetaLine, MonoWell, Pill, SESSION_TONE, Tile,
} from './ui';

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

describe('the liveness dot', () => {
  it('carries its word, and hides the mark from the reader who already has the word', () => {
    const { container } = render(<Dot tone="live" label="working" />);
    expect(screen.getByText('working')).toBeInTheDocument();
    // One mark, and it is decorative: the label is the accessible name, said once.
    const marks = container.querySelectorAll('[aria-hidden="true"]');
    expect(marks).toHaveLength(1);
    expect(marks[0].className).toContain('bg-accent');
  });

  it('spends a different hue per meaning, so the four are told apart with the label AND without', () => {
    const hue = (tone: 'live' | 'parked' | 'ended' | 'idle') => {
      const { container } = render(<Dot tone={tone} label={tone} />);
      return container.querySelector('[aria-hidden="true"]')!.className;
    };
    const hues = [hue('live'), hue('parked'), hue('ended'), hue('idle')];
    expect(new Set(hues).size).toBe(4);
  });
});

describe('the mono well', () => {
  it('renders its text verbatim — never translated, because output is data', () => {
    render(<MonoWell text={'line one\n  indented 道衍'} />);
    expect(screen.getByText(/indented 道衍/)).toBeInTheDocument();
  });

  it('states what fell out of the window rather than quietly skipping it', () => {
    render(<MonoWell text="tail" dropped={12_043} />);
    expect(screen.getByText(/12,?043/)).toBeInTheDocument();
  });

  it('says nothing about drops when nothing dropped', () => {
    render(<MonoWell text="tail" dropped={0} />);
    expect(screen.queryByText(/fell outside/)).not.toBeInTheDocument();
  });

  it('shows the live mark only while live', () => {
    const { rerender } = render(<MonoWell text="tail" label="console" live />);
    expect(screen.getByText('live')).toBeInTheDocument();
    rerender(<MonoWell text="tail" label="console" />);
    expect(screen.queryByText('live')).not.toBeInTheDocument();
  });
});

describe('the meta line', () => {
  it('omits a pair whose value is absent — an absence means something, a blank looks like a bug', () => {
    render(
      <MetaLine items={[
        { label: 'repository', value: 'engine' },
        { label: 'profile', value: null },
        { label: 'tree', value: '' },
      ]}
      />,
    );
    expect(screen.getByText('repository')).toBeInTheDocument();
    expect(screen.queryByText('profile')).not.toBeInTheDocument();
    expect(screen.queryByText('tree')).not.toBeInTheDocument();
  });

  it('lets a long path break rather than pushing the line off the surface', () => {
    render(<MetaLine items={[{ label: 'tree', value: 'a/very/long/checkout/path', mono: true }]} />);
    expect(screen.getByText('a/very/long/checkout/path').className).toContain('break-all');
  });
});

describe('the session tone map', () => {
  // The twin of QUEST_TONE, and the reason that one exists: the compiler holds the completeness, so
  // a tenth state cannot ship half-toned. The runtime half is that every state resolves to a tone.
  const STATES: SessionState[] = [
    'queued', 'starting', 'working', 'awaiting-person',
    'completed', 'declined', 'stood-down', 'failed', 'stopped',
  ];

  it('tones every session state, and awaiting-person wears the one only a person can clear', () => {
    for (const state of STATES) expect(SESSION_TONE[state]).toBeTruthy();
    expect(Object.keys(SESSION_TONE).sort()).toEqual([...STATES].sort());
    expect(SESSION_TONE['awaiting-person']).toBe('declined');
  });
});
