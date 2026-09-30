import { type ComponentType, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from './lib/cn';
import { WithHarnessRuns } from './harnessRuns';
import { useDriver } from './shell';
import { type Notify, PageHeader } from './ui';
import type { StarterDoor } from './help/starters';
import type { SetupStepId } from './help/setup';
import type { BrowserDriver } from './work/browserDrivers';
// The frame reaches a domain only through the list (MOD4): each domain is `settings/<Name>Domain.tsx`,
// and the list in `settings/domains.ts` is the one place one is added.
import { SETTINGS_DOMAINS, type SettingsDomainProps, type SettingsSection } from './settings/domains';

export type { SettingsSection } from './settings/domains';

/**
 * Settings (D66): the application's own — how it looks, which language it speaks — and, on the
 * desktop, everything that is true about THIS machine beneath them.
 *
 * @remarks
 * 🔴 There was no settings page, so no way to change the theme. The machine's page was the only settings there were, reachable from a sliders
 * icon labelled *Machine*, and the theme was the OS's alone. Appearance leads because it is the part
 * everyone has; a browser shows only that part, because a browser may learn nothing of a machine
 * (D47 §4) — the machine's half is absent there, never disabled.
 */

/**
 * A part of a domain a menu item is named for (UX5 U72), found by the id `settings-<anchor>` — and since
 * HELP6 each place Ask Daoris's go may name: Session branches, and each step of the setup guide.
 */
export type SettingsAnchor = 'usage' | 'proposals' | 'wiring' | 'lines' | 'landing' | 'sweep' | `step-${SetupStepId}`;

/** Settings' domains in the order its list shows them — what Ask Daoris's places are held to (HELP6). */
export const SETTINGS_SECTIONS: readonly SettingsSection[] = SETTINGS_DOMAINS.map(({ id }) => id);

/**
 * Settings (D66, as amended by D75): one page, its domains in a list at its left, one shown at a time,
 * as an IDE's settings are.
 *
 * @remarks
 * 🔴 **It was one long page**, and every way in (three menu items, the status bar's driver, remote and
 * tier, a waiting proposal's row) opened it at its top. A domain is now reachable by name, and the
 * one chosen is the caller's to hold, so a menu can open *Permissions* rather than the page.
 *
 * Every domain is cards the page already held. The two doors are unchanged (D50): each row is still
 * the file a terminal edits.
 */
export function SettingsView({
  notify, section = 'appearance', onSection, anchor = null, onAnchored, onGo = () => {}, onAskSetup,
  browserDrivers = [], onAttend,
}: {
  notify: Notify;
  section?: SettingsSection;
  onSection?: (section: SettingsSection) => void;
  /** Who is driving Daoris's browser (BRW8), named by the application, which holds the caches that name them. */
  browserDrivers?: readonly BrowserDriver[];
  /** Open a session in Sessions — only a shell has it. */
  onAttend?: (session: string) => void;
  /** Where a Get started step's door leads (SETUP1a): another domain, Projects, or one of its drawers. */
  onGo?: (door: StarterDoor) => void;
  /** Open Ask Daoris on a first message asking to be walked through the setup (SETUP1b). */
  onAskSetup?: (message: string) => void;
  /**
   * The part of the domain a menu item named, brought into view once it is drawn (UX5 U72): the
   * Agents menu's *Usage* opened its domain at the top, a screen above the usage.
   */
  anchor?: SettingsAnchor | null;
  /** Told once the part is in view, so a later visit opens at the domain's top again. */
  onAnchored?: () => void;
}) {
  const { t } = useTranslation();
  // Watched for until it exists: a part is drawn by the card holding it when that card's query
  // answers, which re-renders the card and not this page, so a check after this page's renders
  // missed it.
  const anchored = useRef(onAnchored);
  anchored.current = onAnchored;
  useEffect(() => {
    if (!anchor) return undefined;
    const bring = () => {
      const part = document.getElementById(`settings-${anchor}`);
      if (!part) return false;
      part.scrollIntoView({ block: 'start' });
      anchored.current?.();
      return true;
    };
    if (bring()) return undefined;
    const watch = new MutationObserver(() => { if (bring()) watch.disconnect(); });
    watch.observe(document.body, { childList: true, subtree: true });
    return () => watch.disconnect();
  }, [anchor]);
  // The same "is a shell here" answer every control uses — one detection path, not two that drift.
  const attached = useDriver().data !== undefined;
  const offered = SETTINGS_DOMAINS.filter((domain) => attached || !domain.machine);
  // A domain this window cannot show opens on Appearance: a machine's domain in a browser, or one a
  // shell remembered.
  const shown = offered.some((domain) => domain.id === section) ? section : 'appearance';
  // Keyed by the domain, so choosing another unmounts this one's cards and their state, as it always did.
  const Domain: ComponentType<SettingsDomainProps> = SETTINGS_DOMAINS.find((domain) => domain.id === shown)!.component;

  return (
    // The application holds a tool's running action above every view (SIGNIN1); rendered alone, this
    // page holds its own, so it still works where nothing above does.
    <WithHarnessRuns notify={notify}>
    <section>
      <PageHeader
        title={t('settings.title')}
        description={t(attached ? 'settings.description' : 'settings.descriptionBrowser')}
      />
      <div className="grid items-start gap-x-6 gap-y-3 md:grid-cols-[11rem_minmax(0,1fr)]">
        {/* It stays put while a long domain scrolls, as an IDE's settings list does. */}
        <nav aria-label={t('settings.domains')} className="md:sticky md:top-0">
          <ul className="m-0 list-none p-0">
            {offered.map(({ id, label }) => (
              <li key={id}>
                <button
                  type="button"
                  aria-current={id === shown ? 'page' : undefined}
                  onClick={() => onSection?.(id)}
                  className={cn(
                    'w-full rounded-control border-l-2 px-3 py-1.5 text-left text-body transition-colors duration-(--speed)',
                    id === shown
                      ? 'border-l-accent bg-accent-soft font-medium text-ink'
                      : 'border-l-transparent text-ink-soft hover:bg-accent-soft/50',
                  )}
                >
                  {t(label)}
                </button>
              </li>
            ))}
          </ul>
        </nav>
        {/* A card stacked under another keeps its own top margin; the first in a domain does not. */}
        <div className="min-w-0 [&>*:first-child]:mt-0">
          <Domain
            key={shown}
            notify={notify}
            attached={attached}
            onGo={onGo}
            onAsk={onAskSetup}
            drivers={browserDrivers}
            onAttend={onAttend}
          />
        </div>
      </div>
    </section>
    </WithHarnessRuns>
  );
}
