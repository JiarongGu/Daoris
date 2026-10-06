import type { ComponentType } from 'react';
import type { StarterDoor } from '../help/starters';
import { GetStartedDomain } from '../setupGuide';
import type { Notify } from '../ui';
import type { BrowserDriver } from '../work/browserDrivers';
import { AiDomain } from './AiDomain';
import { AppearanceDomain } from './AppearanceDomain';
import { BrowserDomain } from './BrowserDomain';
import { DriverDomain } from './DriverDomain';
import { LogsDomain } from './LogsDomain';
import { ToolsDomain } from './ToolsDomain';

/**
 * What the frame hands every domain; each takes the part it uses. `attached` is the frame's one answer
 * to "is a shell here", so no domain asks it a second way.
 */
export type SettingsDomainProps = {
  notify: Notify;
  attached: boolean;
  /** Where a Get started step's door leads (SETUP1a): another domain, Projects, or one of its drawers. */
  onGo: (door: StarterDoor) => void;
  /** Open Ask Daoris on a first message asking to be walked through the setup (SETUP1b). */
  onAsk?: (message: string) => void;
  /** Who is driving Daoris's browser (BRW8), named by the application, which holds the caches that name them. */
  drivers: readonly BrowserDriver[];
  /** Open a session in Sessions — only a shell has it. */
  onAttend?: (session: string) => void;
};

type SettingsDomain = {
  id: string;
  /** The catalogue key the domain list names it by. */
  label: string;
  /** A machine's domain, which a browser is never offered (D47 §4): not a disabled one, none. */
  machine: boolean;
  component: ComponentType<SettingsDomainProps>;
};

/**
 * Settings' domains, in the order its list shows them (D75 §2) — **the one place a domain is added**
 * (MOD4). A domain is a `settings/<Name>Domain.tsx` and a row here; the frame renders whichever row is
 * chosen and names none itself, and `domains.test.ts` holds both.
 *
 * Seven since UX6j (D150 §2.3): what is the machine's or the person's. Every other setting lives on its thing.
 */
export const SETTINGS_DOMAINS = [
  // The setup guide leads (SETUP1a, D97), named Get started again since Setup names a repository's and a workspace's tab
  // (UX6j). A browser's holds the one step it can know, the registry.
  { id: 'start', label: 'settings.domain.start', machine: false, component: GetStartedDomain },
  { id: 'appearance', label: 'settings.domain.appearance', machine: false, component: AppearanceDomain },
  { id: 'ai', label: 'settings.domain.ai', machine: false, component: AiDomain },
  // Workspace left for a workspace's page and each repository's (UX6g, D150 §3.1): its list is Repositories' list, its
  // wiring, defaults and clean-up the workspace's page, its rows per repository the repository's Setup.
  { id: 'driver', label: 'settings.domain.driver', machine: true, component: DriverDomain },
  // Agents left for a place of its own (UX6e, D150 §5): an agent is a product with accounts, and its page holds them.
  // The programs Daoris runs beside its agents (TOOLS7, D121 §4.1): each a file of this machine's, so a browser is
  // offered no such domain.
  { id: 'tools', label: 'settings.domain.tools', machine: true, component: ToolsDomain },
  // Permissions left as Workspace did (UX6g): reading across and a workspace's rules are its page's, a repository's its
  // Setup's, and the rules for every session and the proposals the agent's page's (UX6e). Plugins left for its place
  // (UX6j, D119 §5): nothing about a plugin is set here, and where plugins are looked for is Driver's row.
  { id: 'browser', label: 'settings.domain.browser', machine: true, component: BrowserDomain },
  // The machine log (LOG1c, D94): the machine's alone, so a browser is offered no such domain.
  { id: 'logs', label: 'settings.domain.logs', machine: true, component: LogsDomain },
] as const satisfies readonly SettingsDomain[];

/** Settings' domains, by the id each is opened at. */
export type SettingsSection = (typeof SETTINGS_DOMAINS)[number]['id'];

/**
 * A domain that left Settings and kept a row in it, by the domain that holds the row: Plugins' folder is Driver's (UX6j,
 * the plugins design §5), with its door to the Plugins place.
 */
const KEPT_IN: Readonly<Record<string, SettingsSection>> = { plugins: 'driver' };

/**
 * Where a domain remembered under `daoris.settings` opens now (UX6j): itself while Settings has it; one that left, at
 * the domain holding what stayed of it; and one that left for a page Settings holds no row of (Agents, UX6e; Workspace
 * and Permissions, UX6g), on Appearance, as any domain this window lacks does. A remembered id never opens nothing.
 */
export function rememberedDomain(id: string | null): SettingsSection {
  const kept = id !== null && Object.hasOwn(KEPT_IN, id) ? KEPT_IN[id] : undefined;
  return SETTINGS_DOMAINS.find((domain) => domain.id === id)?.id ?? kept ?? 'appearance';
}
