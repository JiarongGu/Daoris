import { render, screen } from '@testing-library/react';
import { useRef } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useTall } from './useWidth';

function Probe({ reserve }: { reserve: number }) {
  const held = useRef<HTMLDivElement>(null);
  const tall = useTall(held, reserve);
  return <div ref={held} data-testid="held">{tall ?? 'none'}</div>;
}

const rect = (top: number, bottom: number) => ({ top, bottom, height: bottom - top, left: 0, right: 800, width: 800, x: 0, y: top, toJSON: () => ({}) });

describe('useTall', () => {
  afterEach(() => vi.restoreAllMocks());

  it('measures to the bottom of the region it is drawn in, which the bottom panel ends above the window', () => {
    vi.spyOn(window, 'innerHeight', 'get').mockReturnValue(1000);
    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
      return (this.dataset.region === 'main' ? rect(40, 600) : rect(100, 140)) as DOMRect;
    });
    render(<div data-region="main"><Probe reserve={140} /></div>);
    expect(screen.getByTestId('held')).toHaveTextContent('360');
  });

  it('measures to the window where no region holds it or the region is not laid out', () => {
    vi.spyOn(window, 'innerHeight', 'get').mockReturnValue(1000);
    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
      return (this.dataset.region === 'main' ? rect(0, 0) : rect(100, 140)) as DOMRect;
    });
    render(<div data-region="main"><Probe reserve={140} /></div>);
    expect(screen.getByTestId('held')).toHaveTextContent('760');
  });
});
