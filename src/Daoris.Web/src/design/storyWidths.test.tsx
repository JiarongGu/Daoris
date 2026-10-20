import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { CENTRE_FLOOR } from '../work/frame/layout';
import { GUTTERS } from '../work/frame/ViewMain';
import { rooted } from '../test/sources';
import { localFrames, MAIN_WIDTHS, NOT_IN_A_MAIN_AREA, sessionWidths, storyWidths } from './storyWidths';

/**
 * R6 (the UI/UX design, UIFIT1): a story shows the widths the frame gives, without help. A main-area organism's stories
 * are drawn at wide, half and narrow from one helper, and no decorator adds what the page does not have: a gap, a cap, a
 * padding. Every R1 defect passed its stories, which were drawn at 46rem with a gap of their own.
 */
// Quests' main area is drawn from `asks/` and `quests/` as well (STORYGAP1): an ask's page, its sections, a quest's.
const stories = rooted(import.meta.glob('/src/{settings,projects,work,workflow,asks,quests}/**/*.stories.tsx', {
  eager: true, query: '?raw', import: 'default',
}) as Record<string, string>);

describe('the story widths', () => {
  it('are the frame\'s: 80rem, 39rem and the main area\'s 400 px floor', () => {
    expect(MAIN_WIDTHS).toEqual({ wide: '80rem', half: '39rem', narrow: '25rem' });
    expect(Number.parseFloat(MAIN_WIDTHS.narrow) * 16).toBe(CENTRE_FLOOR);
  });

  it('draws the story in a main area of that width, with the page\'s own gutters and nothing else', () => {
    for (const [name, width] of Object.entries(MAIN_WIDTHS) as [keyof typeof MAIN_WIDTHS, string][]) {
      const Story = () => <span>drawn</span>;
      const { container, unmount } = render(<>{storyWidths[name](Story, {} as never)}</>);
      const box = container.querySelector('[data-main-width]')!;
      expect(box.getAttribute('data-main-width')).toBe(width);
      expect((box as HTMLElement).style.width).toBe(width);
      expect(box.getAttribute('class')).toContain('@container/main');
      // The gutters are ViewMain's page gutters at that width: 24 px, 12 at the narrow one, as the window gives them.
      const gutters = name === 'narrow'
        ? GUTTERS.page.split(' ').filter((each) => each.startsWith('max-md:')).map((each) => each.slice('max-md:'.length))
        : GUTTERS.page.split(' ').filter((each) => !each.includes(':'));
      for (const gutter of gutters) expect(box.getAttribute('class')).toContain(gutter);
      expect(box.getAttribute('class')).not.toMatch(/\bgap-|\bmax-w-\[|\bmax-w-\d/);
      unmount();
    }
  });

  it("draws one box when a file's width and a story's nest, at the story's: never a narrow box inside a wide one", () => {
    const Story = () => <span>drawn</span>;
    const Inner = () => <>{storyWidths.narrow(Story, {} as never)}</>;
    const { container } = render(<>{storyWidths.wide(Inner, {} as never)}</>);
    const boxes = container.querySelectorAll('[data-main-width]');
    expect(boxes).toHaveLength(1);
    expect(boxes[0]!.getAttribute('data-main-width')).toBe(MAIN_WIDTHS.narrow);
    expect(boxes[0]!.getAttribute('class')).toContain('px-3');
  });

  it("draws a session's page at the same widths in its own gutters, and a story's session width inside a file's page width", () => {
    for (const [name, width] of Object.entries(MAIN_WIDTHS) as [keyof typeof MAIN_WIDTHS, string][]) {
      const Story = () => <span>drawn</span>;
      const { container, unmount } = render(<>{sessionWidths[name](Story, {} as never)}</>);
      const box = container.querySelector('[data-main-width]')!;
      expect(box.getAttribute('data-main-width')).toBe(width);
      for (const gutter of GUTTERS.session.split(' ')) expect(box.getAttribute('class')).toContain(gutter);
      expect(box.getAttribute('class')).not.toContain('px-6');
      unmount();
    }
    const Story = () => <span>drawn</span>;
    const Inner = () => <>{sessionWidths.narrow(Story, {} as never)}</>;
    const { container } = render(<>{storyWidths.wide(Inner, {} as never)}</>);
    const boxes = container.querySelectorAll('[data-main-width]');
    expect(boxes).toHaveLength(1);
    expect(boxes[0]!.getAttribute('data-main-width')).toBe(MAIN_WIDTHS.narrow);
    expect(boxes[0]!.getAttribute('class')).toContain('px-4');
  });

  it('catches a decorator that frames a story by itself — the check, in the shapes the settings stories had', () => {
    expect(localFrames([['./settings/wiring/Wiring.stories.tsx',
      'decorators: [(Story) => <div className="grid w-[46rem] max-w-full gap-3.5"><Story /></div>],']])).toHaveLength(1);
    expect(localFrames([['./settings/accounts/AccountUse.stories.tsx',
      'decorators: [(Story) => <div className="max-w-3xl rounded-card border border-line bg-page/60 p-3"><Story /></div>],']]))
      .toHaveLength(1);
    expect(localFrames([['./work/x.stories.tsx', 'const mainAt = (width: number): Decorator => (Story) => <div className="flex" style={{ width }}><Story /></div>;']]))
      .toHaveLength(1);
    // And what must keep passing: the helper, a theme, a language, a border and a height.
    expect(localFrames([['./x.stories.tsx', 'decorators: [storyWidths.narrow, chinese, dark],']])).toEqual([]);
    expect(localFrames([['./x.stories.tsx', '(Story) => <InTheme theme="dark"><Story /></InTheme>']])).toEqual([]);
    expect(localFrames([['./x.stories.tsx', '(Story) => <div className="flex min-h-[48rem] border border-line bg-page"><Story /></div>']])).toEqual([]);
  });

  /**
   * STORYGAP1 moved every main-area story to the helper: what still draws a frame of its own is a part of another region, named
   * with where it is mounted, and a new one fails here. A main-area story is never added to the list: it takes the helper.
   */
  it('holds: every story of a component mounted in a main area draws at the helper\'s widths, but the parts of other regions', () => {
    const found = localFrames(Object.entries(stories)).map((hit) => hit.split(' ')[0]!);
    expect([...new Set(found)].sort()).toEqual(Object.keys(NOT_IN_A_MAIN_AREA).sort());
    for (const why of Object.values(NOT_IN_A_MAIN_AREA)) expect(why).toMatch(/\.tsx:\d+/);
  });
});
