import type { Meta, StoryObj } from '@storybook/react-vite';
import { IDENT_HUES } from './icon';
import { PluginIcon } from './PluginIcon';
import { InTheme, PNG_ICON, SVG_ICON } from './storyIcons';

// A plugin's icon (PLUGUI2, D140 §3): its own, drawn as an image of the bytes the driver handed, or its monogram, at the
// strip's, a row's and a page's size, in both themes.

/** One plugin for each identity hue, by an id whose hash lands on it, with a 中文 name among them. */
const BY_HUE: Record<string, { id: string; name: string }> = {
  moss: { id: 'acme.notes', name: 'Session notes' },
  teal: { id: 'acme.quiet', name: '夜间暂停委托' },
  slate: { id: 'land-github', name: 'Land on GitHub' },
  iris: { id: 'acme.index', name: 'index' },
  plum: { id: 'acme.gate', name: 'Acme gate' },
  stone: { id: 'quiet-hours', name: 'Quiet hours' },
};

function Sheet() {
  return (
    <div className="grid gap-3">
      {(['page', 'row', 'strip'] as const).map((size) => (
        <div key={size} className="flex flex-wrap items-center gap-3">
          {IDENT_HUES.map((hue) => <PluginIcon key={hue} {...BY_HUE[hue]!} size={size} />)}
          <PluginIcon id="acme.svg" name="Declared SVG" icon={SVG_ICON} size={size} />
          <PluginIcon id="acme.png" name="Declared PNG" icon={PNG_ICON} size={size} />
          <PluginIcon id="acme.off" name="Off" size={size} dimmed />
          <PluginIcon id="acme.svg" name="Declared SVG, off" icon={SVG_ICON} size={size} dimmed />
        </div>
      ))}
    </div>
  );
}

const meta: Meta<typeof Sheet> = {
  title: 'Plugins/PluginIcon',
  component: Sheet,
};
export default meta;

type Story = StoryObj<typeof Sheet>;

/** The six monograms, a declared SVG and PNG, and an off plugin's, at a page's, a row's and the strip's size; light. */
export const Light: Story = { decorators: [(Story) => <InTheme theme="light"><Story /></InTheme>] };

/** The same in dark: each monogram's hue has a dark value of its own, computed to the same contrast. */
export const Dark: Story = { decorators: [(Story) => <InTheme theme="dark"><Story /></InTheme>] };

/** A declared icon that will not load gives way to the monogram, as a path handed by mistake does. */
export const NotDrawn: Story = {
  render: () => (
    <div className="flex items-center gap-3">
      <PluginIcon id="acme.gate" name="Acme gate" icon="data:image/png;base64,AAAA" size="page" />
      <PluginIcon id="acme.gate" name="Acme gate" icon="C:/somewhere/data/plugins/acme.gate/icon.svg" size="page" />
    </div>
  ),
};
