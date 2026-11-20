import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Card, cardFrame, Tile, WaitingCard } from './Card';

/** The classes a card's frame wears, each on its own. */
const classes = (element: HTMLElement) => element.className.split(/\s+/).filter(Boolean);

/** A 3 px edge on any side, written as Tailwind writes it. */
const THICK = /^border(?:-[xytrblse])?-(?:\[3px\]|3)$/;

describe('the card (the window design §7.3, §7.4)', () => {
  it('pads 12 by 16 and wears no left edge of its own', () => {
    render(<Card>body</Card>);
    const card = screen.getByText('body');
    expect(classes(card)).toEqual(expect.arrayContaining(['rounded-card', 'border', 'border-line', 'bg-raised', 'px-4', 'py-3']));
    expect(classes(card).filter((each) => THICK.test(each))).toEqual([]);
    expect(card.className).not.toMatch(/border-l-(?!line\b)/);
  });

  it("wears a 3 px edge only as a wait's or a failure's, each in its own tone", () => {
    render(<><Card edge="wait">waits</Card><Card edge="failed">failed</Card></>);
    expect(classes(screen.getByText('waits'))).toEqual(expect.arrayContaining(['border-l-[3px]', 'border-l-you', 'px-4', 'py-3']));
    expect(classes(screen.getByText('failed'))).toEqual(expect.arrayContaining(['border-l-[3px]', 'border-l-fail', 'px-4', 'py-3']));
  });

  it('places by its class and keeps its frame', () => {
    render(<Card id="settings-update" className="scroll-mt-3">placed</Card>);
    const card = screen.getByText('placed');
    expect(card).toHaveAttribute('id', 'settings-update');
    expect(classes(card)).toEqual(expect.arrayContaining(['scroll-mt-3', 'px-4', 'py-3']));
  });

  it("is the one frame a card drawn as another element wears, and the waiting card's", () => {
    expect(cardFrame()).toBe(cardFrame(undefined));
    expect(cardFrame().split(' ')).toEqual(expect.arrayContaining(['px-4', 'py-3']));
    expect(cardFrame().split(' ').filter((each) => THICK.test(each))).toEqual([]);
    expect(cardFrame('wait')).toContain('border-l-[3px]');
    render(<WaitingCard title="A folder to trust" />);
    const waiting = screen.getByText('A folder to trust').parentElement!;
    expect(classes(waiting)).toEqual(expect.arrayContaining(['px-4', 'py-3', 'border-l-[3px]', 'border-l-you']));
  });

  it('a tile is a card: the same padding, and a warned one wears its warning in words, never an edge', () => {
    render(<Tile label="Open quests" value={3} note="oldest has sat 12d" warn />);
    const tile = screen.getByText('Open quests').parentElement!;
    expect(classes(tile)).toEqual(expect.arrayContaining(['px-4', 'py-3']));
    expect(classes(tile).filter((each) => THICK.test(each))).toEqual([]);
  });
});
