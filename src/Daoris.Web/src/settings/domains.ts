import type { ComponentType } from 'react';
import type { StarterDoor } from '../help/starters';
import { GetStartedDomain } from '../setupGuide';
import type { Notify } from '../ui';
import type { BrowserDriver } from '../work/browserDrivers';
import { AgentsDomain } from './AgentsDomain';
import { AiDomain } from './AiDomain';
import { AppearanceDomain } from './AppearanceDomain';
import { BrowserDomain } from './BrowserDomain';
import { DriverDomain } from './DriverDomain';
import { LogsDomain } from './LogsDomain';
import { PermissionsDomain } from './PermissionsDomain';
import { PluginsDomain } from './PluginsDomain';
import { WorkspaceDomain } from './WorkspaceDomain';

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
 */
export const SETTINGS_DOMAINS = [
  // The setup guide leads (SETUP1a, D97). A browser's holds the one step it can know, the registry.
  { id: 'start', label: 'settings.domain.start', machine: false, component: GetStartedDomain },
  { id: 'appearance', label: 'settings.domain.appearance', machine: false, component: AppearanceDomain },
  { id: 'ai', label: 'settings.domain.ai', machine: false, component: AiDomain },
  // Its list of workspaces is for everyone; its wiring is the machine's, and only a shell sees that.
  { id: 'workspace', label: 'settings.domain.workspace', machine: false, component: WorkspaceDomain },
  { id: 'driver', label: 'settings.domain.driver', machine: true, component: DriverDomain },
  { id: 'agents', label: 'settings.domain.agents', machine: true, component: AgentsDomain },
  { id: 'permissions', label: 'settings.domain.permissions', machine: true, component: PermissionsDomain },
  { id: 'plugins', label: 'settings.domain.plugins', machine: true, component: PluginsDomain },
  { id: 'browser', label: 'settings.domain.browser', machine: true, component: BrowserDomain },
  // The machine log (LOG1c, D94): the machine's alone, so a browser is offered no such domain.
  { id: 'logs', label: 'settings.domain.logs', machine: true, component: LogsDomain },
] as const satisfies readonly SettingsDomain[];

/** Settings' domains, by the id each is opened at. */
export type SettingsSection = (typeof SETTINGS_DOMAINS)[number]['id'];
