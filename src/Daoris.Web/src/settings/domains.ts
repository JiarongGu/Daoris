import type { ComponentType } from 'react';
import type { StarterDoor } from '../help/starters';
import { GetStartedDomain } from '../help/setupGuide';
import type { Notify } from '../design/ui';
import type { BrowserDriver } from '../work/strip/browserDrivers';
import { AiDomain } from './AiDomain';
import { AppearanceDomain } from './AppearanceDomain';
import { BrowserDomain } from './BrowserDomain';
import { DriverDomain } from './DriverDomain';
import { GitDomain } from './git/GitDomain';
import { LogsDomain } from './LogsDomain';
import { SpaceDomain } from './SpaceDomain';
import { ToolsDomain } from './ToolsDomain';
import { UpdatesDomain } from './UpdatesDomain';
import { WiringDomain } from './WiringDomain';

/**
 * What the frame hands every domain; each takes the part it uses. `attached` is the frame's one answer
 * to "is a shell here", so no domain asks it a second way.
 */
export type SettingsDomainProps = {
  notify: Notify;
  attached: boolean;
  /** Where a Get started step's door leads: another domain, Projects, or one of its drawers. */
  onGo: (door: StarterDoor) => void;
  /** Open Ask Daoris on a first message asking to be walked through the setup. */
  onAsk?: (message: string) => void;
  /** Who is driving Daoris's browser, named by the application, which holds the caches that name them. */
  drivers: readonly BrowserDriver[];
  /** Open a session in Sessions — only a shell has it. */
  onAttend?: (session: string) => void;
  /** The host a door named: Settings → Git opens its add form filled with it, and is told when that form closes. */
  host?: string;
  onClosed?: () => void;
};

type SettingsDomain = {
  id: string;
  /** The catalogue key the domain list names it by. */
  label: string;
  /** A machine's domain, which a browser is never offered: not a disabled one, none. */
  machine: boolean;
  /** The catalogue keys of its cards and rows, by which the palette finds it in the reader's language. */
  rows: readonly string[];
  component: ComponentType<SettingsDomainProps>;
};

/**
 * Settings' domains, in the order its list shows them — **the one place a domain is added**.
 * A domain is a `settings/<Name>Domain.tsx` and a row here; the frame renders whichever row is
 * chosen and names none itself, and `domains.test.ts` holds both.
 *
 * Each is what a person comes to set, the machine's or the person's own. Every other setting lives on its thing.
 */
export const SETTINGS_DOMAINS = [
  // The setup guide leads, named Get started again since Setup names a repository's and a workspace's tab.
  // A browser's holds the one step it can know, the registry.
  {
    id: 'start', label: 'settings.domain.start', machine: false, component: GetStartedDomain,
    rows: ['setup.step.agent.title', 'setup.step.helper.title', 'setup.step.repositories.title', 'setup.step.rules.title'],
  },
  {
    id: 'appearance', label: 'settings.domain.appearance', machine: false, component: AppearanceDomain,
    rows: ['settings.theme.label', 'settings.language.label'],
  },
  // How sessions run here: the driver's dials, this machine's access and the plugins' settings. Agents left for a
  // place of its own (an agent is a product with accounts, and its page holds them), Plugins for its place.
  {
    id: 'driver', label: 'settings.domain.driver', machine: true, component: DriverDomain,
    rows: ['settings.notify.label', 'settings.cap.label', 'settings.quiet.label', 'settings.cooloff.label', 'projects.setup.section.access', 'plugin.folder'],
  },
  {
    id: 'ai', label: 'settings.domain.ai', machine: false, component: AiDomain,
    rows: ['settings.ai.search.label', 'settings.ai.helpMode.label', 'settings.ai.roles.title'],
  },
  // Who Daoris's git acts as on a code host: identities, a host's default and the person's own
  // sign-ins; the machine's alone, so a browser is offered none.
  {
    id: 'git', label: 'settings.domain.git', machine: true, component: GitDomain,
    rows: ['settings.git.identities', 'settings.git.defaults', 'settings.git.own', 'settings.git.access.machine'],
  },
  // The programs Daoris runs beside its agents: each a file of this machine's.
  { id: 'tools', label: 'settings.domain.tools', machine: true, component: ToolsDomain, rows: ['settings.tools.locations.title'] },
  {
    id: 'browser', label: 'settings.domain.browser', machine: true, component: BrowserDomain,
    rows: ['settings.browser.which.title', 'settings.browser.links.label', 'settings.browser.favorites.title', 'settings.browser.extensions.title'],
  },
  // What Daoris keeps on this disk: its folders, each with its size, and the rules that keep and free it.
  {
    id: 'space', label: 'settings.domain.space', machine: true, component: SpaceDomain,
    rows: ['settings.daoris.card.folders', 'settings.daoris.card.space', 'settings.daoris.label.folder.cache', 'settings.daoris.button.freeUp'],
  },
  // The install's update, then the versions it carries.
  { id: 'updates', label: 'settings.domain.updates', machine: true, component: UpdatesDomain, rows: ['settings.update.title', 'settings.daoris.card.versions'] },
  // How this install is wired here: the terminal and the service first, then the facts behind them. It shows what is
  // true; the domain that sets a thing keeps its control.
  {
    id: 'wiring', label: 'settings.domain.wiring', machine: true, component: WiringDomain,
    rows: [
      'settings.daoris.card.terminal', 'settings.daoris.label.putOnPath', 'settings.daoris.card.service', 'settings.daoris.label.changeAddress',
      'settings.daoris.card.files', 'settings.daoris.card.variables', 'settings.daoris.card.loop', 'settings.daoris.card.knowledge',
      'settings.daoris.card.logs', 'settings.daoris.card.toolHome',
    ],
  },
  // The machine log, which Help's *Machine log* opens.
  { id: 'logs', label: 'settings.domain.logs', machine: true, component: LogsDomain, rows: [] },
] as const satisfies readonly SettingsDomain[];

/** Settings' domains, by the id each is opened at. */
export type SettingsSection = (typeof SETTINGS_DOMAINS)[number]['id'];

/**
 * A domain that left Settings and kept a row in it, by the domain that holds the row: Plugins' folder is Driver's,
 * with its door to the Plugins place.
 */
const KEPT_IN: Readonly<Record<string, SettingsSection>> = { plugins: 'driver' };

/**
 * Where a domain remembered under `daoris.settings` opens now: itself while Settings has it; one that left, at
 * the domain holding what stayed of it; and one that left for a page Settings holds no row of (Agents, Workspace
 * and Permissions), on Appearance, as any domain this window lacks does. A remembered id never opens nothing.
 */
export function rememberedDomain(id: string | null): SettingsSection {
  const kept = id !== null && Object.hasOwn(KEPT_IN, id) ? KEPT_IN[id] : undefined;
  return SETTINGS_DOMAINS.find((domain) => domain.id === id)?.id ?? kept ?? 'appearance';
}
