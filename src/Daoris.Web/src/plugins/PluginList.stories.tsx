import type { Meta, StoryObj } from '@storybook/react-vite';
import { LIST_BOUNDS, LIST_STRIP, type ListLayout } from '../work/layout';
import { ListMore, ListPane } from '../work/ListPane';
import { type OfferShown, pluginGroups, type PluginShown } from './catalog';
import { PluginList, PluginStrip } from './PluginList';
import { InTheme, PNG_ICON, SVG_ICON } from './storyIcons';

// The Plugins view's list as a catalogue (PLUGUI2, D140 §2; PLUGUI1b, D119 §3.1) on its list pane, in every state the
// design names: the catalogue in both themes, empty, loading, an error with no answer ever, a refused row, a 中文 name,
// the strip, and laid over the main area.

const open = (width: number): ListLayout => ({ mode: 'open', width, beside: width, auto: false });
const CLOSED: ListLayout = { mode: 'strip', width: LIST_STRIP, beside: LIST_STRIP, auto: false };
const DRAWN: ListLayout = { mode: 'strip', width: LIST_STRIP, beside: LIST_STRIP, auto: true };

const plugin = (over: Partial<PluginShown>): PluginShown => ({
  id: 'acme.gate', name: 'Acme gate', version: '1.2.0', description: 'Holds quests overnight.',
  enabled: true, problem: null, harnesses: [], points: ['quest/consider'], running: true,
  folder: 'C:/somewhere/data/plugins/acme.gate', data: 'C:/somewhere/data/plugins/.data/acme.gate',
  ...over,
});

// A catalogue a machine could hold: an icon of its own (SVG, PNG) and monograms, each source, an update waiting, a
// plugin with no description, a refused one and an off one.
const PLUGINS: PluginShown[] = [
  plugin({
    harnesses: ['acme-agent'], points: ['quest/consider', 'session/ended'], icon: SVG_ICON,
    source: { kind: 'folder', folder: 'C:/somewhere/checkouts/plugins/acme.gate' }, update: 'waits',
  }),
  plugin({
    id: 'in-app-browser', name: 'In-app browser', version: '0.1.0', description: "Daoris's browser, handed to every session.",
    points: [], running: false, source: { kind: 'offer', offer: 'in-app-browser' }, update: 'current',
  }),
  plugin({ id: 'acme.notes', name: 'Session notes', description: '', points: ['session/ended'], running: false, icon: PNG_ICON, source: { kind: 'none' } }),
  plugin({ id: 'acme.agents', name: 'Acme agents', harnesses: ['acme-agent', 'acme-review'], points: [], running: false, source: { kind: 'unread', problem: 'it is not JSON' } }),
  plugin({ id: 'future', name: 'Future', version: '', description: '', problem: 'needs plugin API 99, and this build speaks 1 — update Daoris.', points: [], running: false }),
  plugin({ id: 'quiet-hours', name: 'Quiet hours', enabled: false, running: false, source: { kind: 'folder', folder: 'C:/somewhere/plugins/quiet-hours' } }),
];

const OFFERS: OfferShown[] = [
  {
    id: 'in-app-browser', name: 'In-app browser', version: '0.1.0', description: 'Daoris\'s browser, handed to every session.',
    problem: null, harnesses: [], points: [], servers: ['browser'], needs: [], installed: false,
  },
  {
    id: 'land-github', name: 'Land on GitHub', version: '0.2.0', description: 'Pushes a landed branch and opens its pull request.',
    problem: null, harnesses: [], points: ['work/land'], servers: [], needs: ['`gh` on the PATH, signed in'], installed: false,
  },
];

const KINDS = [{ id: 'ask', label: 'Ask Daoris for a plugin' }, { id: 'make', label: 'Make a plugin…' }];

type Args = {
  plugins: PluginShown[];
  offers: OfferShown[];
  chosen: string | null;
  layout: ListLayout;
  loading?: boolean;
  unanswered?: string;
};

/** The list as the view hands it to its pane: its ＋ with two kinds, its ⋯, its strip, its body. */
function PluginsListPane({ plugins, offers, chosen, layout, loading = false, unanswered }: Args) {
  const groups = pluginGroups(plugins, offers);
  const empty = !loading && !unanswered && plugins.length === 0 && offers.length === 0;
  return (
    <ListPane
      name="Plugins"
      labels={{ open: 'Show the plugin list', close: 'Hide the plugin list', resize: 'plugin list width' }}
      layout={layout}
      bounds={LIST_BOUNDS.plugins}
      make={{ label: 'Add a plugin', kinds: KINDS, onMake: () => {} }}
      more={<ListMore label="More actions" items={[{ id: 'try', label: 'Try a folder…' }]} onChoose={() => {}} />}
      strip={<PluginStrip groups={groups} chosen={chosen} onChoose={() => {}} />}
      loading={loading}
      empty={empty ? {
        headline: 'No plugins on this machine',
        body: 'A plugin adds an agent sessions can run on, hands every session a server, or speaks at a point the driver asks. Ask Daoris for one, or make one yourself.',
      } : undefined}
      onOpen={() => {}}
      onClose={() => {}}
      onDismiss={() => {}}
      onResize={() => {}}
    >
      <PluginList groups={groups} chosen={chosen} unanswered={unanswered} onChoose={() => {}} onInstall={() => {}} />
    </ListPane>
  );
}

const meta: Meta<typeof PluginsListPane> = {
  title: 'Plugins/PluginList',
  component: PluginsListPane,
  args: { plugins: PLUGINS, offers: OFFERS, chosen: 'acme.gate', layout: open(LIST_BOUNDS.plugins.initial) },
  // The frame's height, and a main area beside the list for it to sit beside or lie over.
  decorators: [(Story) => (
    <div className="relative flex h-[30rem] w-[56rem] max-w-full border border-line bg-page">
      <Story />
      <div className="min-w-0 flex-1 p-4 text-body text-ink-soft">The main area: the chosen plugin's page.</div>
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof PluginsListPane>;

/**
 * The catalogue (D140 §2): *Installed*, the one waiting on you first, then on, then off; then Daoris's own plugins not
 * installed. Each row its icon, its name and version and state, what it gives, then where it came from, what it adds
 * and *update available*.
 */
export const Catalogue: Story = {};

/** The catalogue in the light theme, chosen. */
export const CatalogueLight: Story = { decorators: [(Story) => <InTheme theme="light"><Story /></InTheme>] };

/** The catalogue in the dark theme, chosen: the monograms' dark hues, a declared icon as its author drew it. */
export const CatalogueDark: Story = { decorators: [(Story) => <InTheme theme="dark"><Story /></InTheme>] };

/** A refused plugin chosen: it waits on the person, and its word wears the waiting hue. */
export const Refused: Story = { args: { chosen: 'future' } };

/** Nothing on this machine and nothing offered: the empty state, with the ＋'s two kinds. */
export const Empty: Story = { args: { plugins: [], offers: [] } };

/** A first load: skeleton rows, never the empty state. */
export const Loading: Story = { args: { loading: true } };

/** An error with no answer ever: the list says the sentence in place, and is never blank. */
export const ErrorNoAnswer: Story = {
  args: { plugins: [], offers: [], unanswered: 'This machine did not answer in time — the driver may be busy. Try again in a moment.' },
};

/** A plugin named in 中文: its name is content, never translated. */
export const ChineseName: Story = {
  args: {
    plugins: [plugin({ id: 'acme.quiet', name: '夜间暂停委托', description: '夜里不开始新的委托。' }), ...PLUGINS.slice(1)],
    chosen: 'acme.quiet',
  },
};

/** Closed by the person: its strip, each installed plugin's initial and its mark; offers are not on it. */
export const Strip: Story = { args: { layout: CLOSED, chosen: 'future' } };

/** A strip the window drew for want of room: the same strip, whose open lays the list over. */
export const StripByTheWindow: Story = { args: { layout: DRAWN } };

/** A strip the window drew, opened: the list over the main area, beside its strip. */
export const LaidOver: Story = { args: { layout: { mode: 'over', width: LIST_BOUNDS.plugins.initial, beside: LIST_STRIP, auto: true } } };
