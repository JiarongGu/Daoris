import type { Decorator } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';

// What a flowchart's stories share (FLOWCHART1a1; the flowchart design §4.4): its frame's widths and its themes. Stories
// only: nothing in the product imports this.

/** The frame's widths (§3.2): the side bar's floor, the main area's floor, a 680 px window's main area, a wide pane. */
export const FLOW_WIDTHS = [300, 400, 544, 1200] as const;

/**
 * The story at each width, one frame each with its width named above it, so each flow measures its own pane. A frame adds
 * nothing the page does not have: no padding, and an outline that takes no room. `data-frame` is how the measured-box
 * check finds each one.
 */
export const atWidths = (widths: readonly number[] = FLOW_WIDTHS): Decorator => (Story) => (
  <div className="grid gap-6">
    {widths.map((width) => (
      <section key={width} aria-label={`${width} px`} data-frame={width} className="grid gap-1">
        <p className="m-0 font-mono text-meta text-ink-faint">{width} px</p>
        <div className="outline-1 outline-dashed outline-line" style={{ width }}><Story /></div>
      </section>
    ))}
  </div>
);

/** The page in a chosen theme, outside the frames, so its padding takes nothing from one. */
export const inDark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;
export const inLight: Decorator = (Story) => <InTheme theme="light"><Story /></InTheme>;
