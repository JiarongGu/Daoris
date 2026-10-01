import { createRef } from 'react';
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ViewMain } from './ViewMain';

// The main area (D118 §3b): props only, since a molecule imports no hook. jsdom lays nothing out, so what
// it holds is the property each state rests on — the container a view's split is measured by, the region
// the keys focus, and what each state draws in place of the page.

const NONE = { headline: 'Nothing chosen', body: 'Choose one in the list and it opens here.', action: <button type="button">New quest</button> };
const GONE = { headline: 'No record of that quest', body: 'It is not in this workspace any more.' };

describe('the main area', () => {
  it('is the container its page lays out by, and the region the keys focus', () => {
    render(<ViewMain><p>the page</p></ViewMain>);
    const main = screen.getByRole('main');
    // Named, so a split inside a card's own container still measures the main area (`@4xl/main:`).
    expect(main).toHaveClass('@container/main');
    expect(main).toHaveAttribute('data-region', 'main');
    // It scrolls by itself, and is the block what is positioned inside it is placed in (PERM1).
    expect(main).toHaveClass('overflow-y-auto', 'relative', 'min-w-0');
    expect(screen.getByText('the page')).toBeInTheDocument();
  });

  it('draws the chosen item under its page header', () => {
    render(<ViewMain header={<h1>Expose a streaming budget</h1>} none={NONE}><p>the record</p></ViewMain>);
    expect(screen.getByRole('heading', { level: 1, name: 'Expose a streaming budget' })).toBeInTheDocument();
    expect(screen.getByText('the record')).toBeInTheDocument();
    expect(screen.queryByText('Nothing chosen')).toBeNull();
  });

  it('says how to choose where nothing is, and offers the list\'s ＋', () => {
    render(<ViewMain state="none" none={NONE}><p>the record</p></ViewMain>);
    expect(screen.getByText('Nothing chosen')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'New quest' })).toBeInTheDocument();
    expect(screen.queryByText('the record')).toBeNull();
  });

  it('draws skeleton rows while the chosen item loads, never the empty state', () => {
    render(<ViewMain state="loading" none={NONE}><p>the record</p></ViewMain>);
    const main = screen.getByRole('main');
    expect(main).toHaveAttribute('aria-busy', 'true');
    expect(main).toHaveAttribute('data-main-state', 'loading');
    expect(screen.queryByText('Nothing chosen')).toBeNull();
    expect(screen.queryByText('the record')).toBeNull();
    expect(main.querySelector('[aria-hidden] i')).not.toBeNull();
  });

  it('says an item has gone, rather than drawing nothing or the empty state', () => {
    render(<ViewMain state="gone" none={NONE} gone={GONE}><p>the record</p></ViewMain>);
    expect(screen.getByText('No record of that quest')).toBeInTheDocument();
    expect(screen.queryByText('Nothing chosen')).toBeNull();
    expect(screen.getByRole('main')).not.toHaveAttribute('aria-busy');
  });

  it('keeps the tighter gutters Sessions uses, as a column its conversation follows', () => {
    const ref = createRef<HTMLElement>();
    render(<ViewMain gutters="session" ref={ref}><p>the conversation</p></ViewMain>);
    expect(ref.current).toBe(screen.getByRole('main'));
    expect(ref.current).toHaveClass('flex', 'flex-col', 'px-4');
  });

  it('caps no content: the page follows the main area (UX5 U59)', () => {
    render(<ViewMain header={<h1>Overview</h1>}><p>the page</p></ViewMain>);
    expect(screen.getByRole('main').className).not.toMatch(/\bmax-w-/);
  });
});
