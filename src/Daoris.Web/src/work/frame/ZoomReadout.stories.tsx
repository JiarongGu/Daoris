import { useLayoutEffect, useState } from 'react';
import type { Meta, StoryObj } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import { applyZoom } from '../../design/zoom';
import { InTheme } from '../../plugins/storyIcons';
import { CommandCenter } from '../strip/CommandCenter';
import { ActivityBar, type ActivityItem, AppStrip, StatusBar } from './frame';
import { ZoomReadout } from './ZoomReadout';

// The window's zoom (ZOOM1, the UI/UX design §3.11): the window at a level, the content scaled and the frame (the title strip,
// the activity bar, the status bar) at its own size, the level said over the content as it changes and on the status bar while
// it is not 100%. The window is drawn at a fixed size in pixels, as a real window's viewport is, whatever the level.
// `measured/zoom.spec.ts` measures these stories in Chromium.

type Place = 'overview' | 'quests' | 'projects' | 'settings';
const PLACES: ActivityItem<Place>[] = [
  { tab: 'overview', label: 'Overview', icon: 'overview' },
  { tab: 'quests', label: 'Quests', icon: 'quests', waits: 2, detail: '2 waiting on you' },
  { tab: 'projects', label: 'Repositories', icon: 'projects' },
];
const SETTINGS: ActivityItem<Place>[] = [{ tab: 'settings', label: 'Settings', icon: 'settings' }];

/** The window at `level`: the document zoomed as `design/zoom.ts` zooms it, and put back at 100% when the story goes. */
function ZoomedWindow({ level, theme = 'light' }: { level: number; theme?: 'light' | 'dark' }) {
  const [said, setSaid] = useState(100);
  useLayoutEffect(() => {
    applyZoom(level);
    // From 100% to the level, so the readout says it as the window would.
    setSaid(level);
    return () => applyZoom(100);
  }, [level]);
  return (
    <Tooltip.Provider>
      <InTheme theme={theme}>
        <div data-window="" className="flex h-[480px] w-[960px] flex-col overflow-hidden border border-line bg-page">
          <AppStrip
            captionRoom
            menus={<span className="text-small text-ink-soft">Workspace · Edit · View · Go</span>}
            center={<CommandCenter scope="Every workspace" shortcut="Ctrl K" onOpen={() => {}} label="Search and run commands" />}
          />
          <div className="flex min-h-0 flex-1">
            <ActivityBar label="Views" items={PLACES} end={SETTINGS} active="quests" onSelect={() => {}} />
            <main data-content="" className="min-w-0 flex-1 overflow-auto p-4">
              <h1 className="m-0 text-view font-semibold">Quests</h1>
              <p className="mt-2 text-body text-ink-soft">
                The content grows and shrinks with the zoom; the title strip, the places and the status bar keep their size.
              </p>
            </main>
          </div>
          <StatusBar driver="running" sessions={2} workspace="work" remote={false} zoom={{ level, onReset: () => {} }} />
        </div>
        {/* Held up for as long as the story is open, so the readout can be looked at. */}
        <ZoomReadout level={said} lasts={2 ** 31 - 1} />
      </InTheme>
    </Tooltip.Provider>
  );
}

const meta: Meta<typeof ZoomedWindow> = {
  title: 'Chrome/Zoom',
  component: ZoomedWindow,
};
export default meta;

type Story = StoryObj<typeof ZoomedWindow>;

/** The page as drawn: nothing said, nothing on the status bar. */
export const At100: Story = { args: { level: 100 } };

/** One step in: the content larger, the readout and the status bar's level saying 110%. */
export const At110: Story = { args: { level: 110 } };

/** The largest step, in dark. */
export const At200: Story = { args: { level: 200, theme: 'dark' } };

/** The smallest step: the content at half, the frame at its own size. */
export const At50: Story = { args: { level: 50 } };
