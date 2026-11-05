import type { Meta, StoryObj } from '@storybook/react-vite';
import { useMemo, useState } from 'react';
import { cn } from '../../lib/cn';
import { inDark } from '../flowFrames';
import { chinese } from '../storyLanguage';
import { storyWidths } from '../storyWidths';
import { SuppliedCanvas, type SuppliedWorld } from './SuppliedCanvas';
import { lineDrawn } from './suppliedCull';

// A large supplied world (CANVAS1e; D224, the canvas design §2.6, §4): the maps' canvas at 2,000 and 5,000 cards, culled past 400 and
// drawn as dots below 4 px of `text-meta`, at the main area's 80, 39 and 25rem, in English and 中文 and both themes. Opened fitted, the
// world is a field of dots on the canvas2D layer with the Tab stop and the selected still cards; opened at 100%, only the cards near
// the view are mounted. Keyboard: the arrows walk to a card that is not mounted, which is mounted and focused, and the view follows.

const W = 120;
const H = 36;

function fieldWorld(count: number): SuppliedWorld & { lines: { id: string; from: string; to: string }[] } {
  const side = Math.ceil(Math.sqrt(count * 2));
  const boxes = new Map<string, { x: number; y: number; w: number; h: number }>();
  const lines: { id: string; from: string; to: string }[] = [];
  for (let at = 0; at < count; at += 1) {
    const id = `card-${String(at).padStart(4, '0')}`;
    boxes.set(id, { x: (at % side) * (W + 40), y: Math.floor(at / side) * (H + 48), w: W, h: H });
    if (at % side !== 0) lines.push({ id: `line-${at}`, from: `card-${String(at - 1).padStart(4, '0')}`, to: id });
  }
  const columns = Math.min(count, side);
  const rows = Math.ceil(count / side);
  return { frame: { x: -24, y: -24, w: columns * (W + 40) + 8, h: rows * (H + 48) + 8 }, boxes, order: [...boxes.keys()], lines };
}

function Field({ count, opening }: { count: number; opening: number }) {
  const world = useMemo(() => fieldWorld(count), [count]);
  const [selected, setSelected] = useState<string | null>('card-0042');
  return (
    <SuppliedCanvas
      world={world} label={`a field of ${count} cards`} moves="a field: drag to move" fit="Show the whole field" findLabel="find a card"
      feature="story" storeKey={`field-${count}-${opening}`} selected={selected} onSelect={setSelected}
      zoom={{ min: 0.1, max: 2.5 }} opening={opening}
      nodeLabel={(id) => id} titleOf={(id) => id}
      renderNode={(id, state) => (
        <span className={cn('flex h-full w-full items-center rounded-card border bg-raised px-2', state.selected ? 'border-[3px] border-accent' : 'border-line-strong')}>
          {state.tier === 'full' && <span className="truncate font-mono text-meta text-ink">{id}</span>}
        </span>
      )}
      renderEdges={(context) => world.lines.filter((line) => lineDrawn(context.culled, line.from, line.to)).map((line) => {
        const [a, b] = [world.boxes.get(line.from)!, world.boxes.get(line.to)!];
        return (
          <g key={line.id} {...context.line(line.id)} aria-label={`${line.from} to ${line.to}`} style={{ pointerEvents: 'auto' }}>
            <path d={`M ${a.x + a.w} ${a.y + a.h / 2} L ${b.x} ${b.y + b.h / 2}`} className="stroke-line-strong" strokeWidth={1.5} fill="none" />
          </g>
        );
      })}
    />
  );
}

const meta: Meta<typeof Field> = {
  title: 'Design/Canvas/SuppliedLarge',
  component: Field,
  args: { count: 2000, opening: 0.1 },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof Field>;

/** Two thousand cards opened fitted: a field of dots, the selected and the Tab stop still cards. */
export const Dots: Story = {};
export const DotsHalf: Story = { decorators: [storyWidths.half] };
export const DotsNarrow: Story = { decorators: [storyWidths.narrow] };
export const DotsChineseDark: Story = { decorators: [storyWidths.wide, chinese, inDark] };

/** Two thousand cards at 100%: only those near the view are mounted, the lines that reach one drawn. */
export const Culled: Story = { args: { opening: 1 } };
export const CulledHalf: Story = { args: { opening: 1 }, decorators: [storyWidths.half] };
export const CulledDark: Story = { args: { opening: 1 }, decorators: [storyWidths.wide, inDark] };

/** Five thousand cards, fitted. */
export const ThousandsDots: Story = { args: { count: 5000 } };
