import type { Meta, StoryObj } from '@storybook/react-vite';
import { PageStopped } from './PageBoundary';

// The page's error screen (PAGELOOP1), the states a reviewer must see: the window's, with the machine log that heard it; a
// message longer than its line; and a browser's, which has no log to name.

const meta: Meta<typeof PageStopped> = {
  title: 'Shell/PageStopped',
  component: PageStopped,
  args: {
    message: 'Minified React error #185; visit https://react.dev/errors/185 for the full message.',
    logged: true,
    onReload: () => {},
  },
};
export default meta;

type Story = StoryObj<typeof PageStopped>;

/** In the window: what broke, that nothing behind it did, where the log keeps it, and the reload. */
export const InTheWindow: Story = {};

/** A message longer than its line keeps to one, the whole of it on its tip. */
export const LongMessage: Story = {
  args: { message: `Cannot read properties of undefined (reading 'id') ${'in a component deep in the session list '.repeat(4)}` },
};

/** In a browser: no machine log heard it, so the screen names none. */
export const InABrowser: Story = { args: { logged: false } };
