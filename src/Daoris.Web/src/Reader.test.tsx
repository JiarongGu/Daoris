import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import './i18n';
import type { Entry } from './api';
import { Reader } from './Reader';

const ENTRY = {
  id: 'e1', repository: 'game', kind: 'Knowledge', provenance: 'Local', title: 'world-streaming',
  path: '.claude/knowledge/world-streaming.md',
  body: '# World streaming — chunk hydration order\n\nThe example game\'s own knowledge: **chunk hydration** runs neighbours-first, so a player\nturning the camera never sees an unhydrated seam.',
} as unknown as Entry;

describe('Reader', () => {
  /**
   * Doctrine is read as it is written: the body is the source, unrendered, so the page gives no
   * second opinion about what the file says.
   */
  it('shows the entry as it is written', () => {
    render(<Tooltip.Provider><Reader entry={ENTRY} onClose={() => {}} /></Tooltip.Provider>);

    const body = screen.getByText(/# World streaming/);
    expect(body.tagName).toBe('PRE');
    expect(body.textContent).toContain('**chunk hydration**');
  });

  /**
   * 🔴 UX5 U43: a source is wrapped near a hundred characters, and the 32rem drawer holds about
   * sixty of its monospace, so every line broke once more and the entry read as a zigzag of long and
   * short lines. The reader is wide enough for a source line, which keeps it as it is written.
   */
  it('is wide enough for a line of the source, so the file is not broken twice', () => {
    render(<Tooltip.Provider><Reader entry={ENTRY} onClose={() => {}} /></Tooltip.Provider>);

    expect(screen.getByRole('dialog').className).toContain('w-[min(52rem,calc(100%-3rem))]');
  });
});
