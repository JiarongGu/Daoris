import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import type { NodeState } from '../../design/canvas/SuppliedCanvas';
import { CodeCard, cutId, FITS, lastSegment } from './CodeCards';

// Each tier of a module, a folder and a cycle says less about the same card.

const state = (extra: Partial<NodeState> = {}): NodeState => ({ tier: 'full', selected: false, focused: false, dim: false, scale: 1, ...extra });
const LONG = 'src/Daoris.Service/session-registry';

beforeEach(() => i18n.changeLanguage('en'));
afterEach(cleanup);

describe('a module by tier', () => {
  it('at the full tier says its id, cut at what a card holds', () => {
    render(<CodeCard id={LONG} state={state()} />);
    expect(screen.getByText(cutId(LONG))).toBeInTheDocument();
    expect(cutId(LONG).length).toBe(FITS);
  });

  it('at the chip tier says the id\'s last segment', () => {
    render(<CodeCard id={LONG} state={state({ tier: 'chip', scale: 0.5 })} />);
    expect(screen.getByText('session-registry')).toBeInTheDocument();
    expect(lastSegment('web')).toBe('web');
    expect(lastSegment('a.b.engine')).toBe('engine');
  });

  it('at the dot tier says nothing, and keeps the whole id beside it while chosen', () => {
    const { container, rerender } = render(<CodeCard id={LONG} state={state({ tier: 'dot', scale: 0.1 })} />);
    expect(container.textContent).toBe('');
    rerender(<CodeCard id={LONG} state={state({ tier: 'dot', scale: 0.1, selected: true })} />);
    expect(container.querySelector('[data-card-label]')!.textContent).toBe(LONG);
    rerender(<CodeCard id={LONG} state={state({ tier: 'dot', scale: 0.1, focused: true })} />);
    expect(container.querySelector('[data-card-label]')!.textContent).toBe(LONG);
  });
});

describe('a folder and a cycle by tier', () => {
  const folder = { kind: 'folder', name: 'session', count: 4 } as const;
  const cycle = { kind: 'cycle', title: 'cycle of 3 across 2 folders', count: 3 } as const;

  it('says a folder\'s name and ×n at the full and the chip tiers, and nothing at the dot tier', () => {
    const { container, rerender } = render(<CodeCard id="session" group={folder} state={state()} />);
    expect(screen.getByText('session')).toBeInTheDocument();
    expect(screen.getByText('×4')).toBeInTheDocument();
    rerender(<CodeCard id="session" group={folder} state={state({ tier: 'chip', scale: 0.5 })} />);
    expect(screen.getByText('×4')).toBeInTheDocument();
    rerender(<CodeCard id="session" group={folder} state={state({ tier: 'dot', scale: 0.1 })} />);
    expect(container.textContent).toBe('');
  });

  it('says a cycle\'s size and the folders it crosses at the full tier, and its count at the chip tier', () => {
    const { rerender } = render(<CodeCard id="c" group={cycle} state={state()} />);
    expect(screen.getByText('cycle of 3 across 2 folders')).toBeInTheDocument();
    rerender(<CodeCard id="c" group={cycle} state={state({ tier: 'chip', scale: 0.5 })} />);
    expect(screen.getByText('×3')).toBeInTheDocument();
  });

  it('keeps a chosen folder\'s name beside it at the dot tier', () => {
    const { container } = render(<CodeCard id="session" group={folder} state={state({ tier: 'dot', scale: 0.1, selected: true })} />);
    expect(container.querySelector('[data-card-label]')!.textContent).toBe('session');
  });
});
