import { type ComponentType, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { WithHarnessRuns } from './harnessRuns';
import { useDriver } from './shell';
import type { Notify } from './ui';
import type { StarterDoor } from './help/starters';
import type { SetupStepId } from './help/setup';
import type { BrowserDriver } from './work/browserDrivers';
import { useListPanes } from './work/listPanes';
import { ViewFrame, type ViewLayout } from './work/ViewFrame';
import { ViewMain } from './work/ViewMain';
// The frame reaches a domain only through the list (MOD4): each domain is `settings/<Name>Domain.tsx`,
// and the list in `settings/domains.ts` is the one place one is added. `DomainList` draws the list's rows.
import { SETTINGS_DOMAINS, type SettingsDomainProps, type SettingsSection } from './settings/domains';
import { DomainList } from './settings/DomainList';

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
 * HELP6 each place Ask Daoris's go may name: each step of the setup guide. Session branches, the lines, the landing,
 * the wiring and reading across left with Settings → Workspace and Permissions for a workspace's page (UX6g), whose
 * doors name its tab and section instead.
 */
export type SettingsAnchor = 'usage' | 'proposals' | 'update' | `step-${SetupStepId}`;

/** Settings' domains in the order its list shows them — what Ask Daoris's places are held to (HELP6). */
export const SETTINGS_SECTIONS: readonly SettingsSection[] = SETTINGS_DOMAINS.map(({ id }) => id);

/** What Settings is handed, wherever it is drawn. */
export type SettingsProps = {
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
};

/**
 * **Settings on the frame** (D118 §2, §5; FRAME1g): its domains are its list pane and the domain chosen is
 * its main area, handed to the window's frame as every view with a list hands its own.
 *
 * @remarks
 * 🔴 **It was a page drawing its own list** (audit ST1–ST3, ST10): an 11 rem column inside the main area,
 * which never closed and never resized, and below a 768 px viewport stacked above the domain, ten rows over
 * it on a shell — the arrangement D56 rejected for the activity bar. Its list is now the frame's list pane:
 * 176–320 px and 176 to start, closed to its strip by the person, a strip by itself where the domain would
 * fall below its floor, and laid over the domain from that strip. Settings makes nothing, so it has no `＋`.
 *
 * Every way in names its domain (D75), through the application's one opener (D118 §3i), so the domain
 * chosen is the caller's to hold, kept as it always was under `daoris.settings` (`listPanes.ts`).
 */
export function useSettingsLayout({ notify, section = 'appearance', onSection, onGo = () => {}, onAskSetup, browserDrivers = [], onAttend, anchor = null, onAnchored }: SettingsProps): ViewLayout {
  const { t } = useTranslation();
  // The same "is a shell here" answer every control uses — one detection path, not two that drift.
  const attached = useDriver().data !== undefined;
  const offered = SETTINGS_DOMAINS.filter((domain) => attached || !domain.machine);
  // A domain this window cannot show opens on Appearance: a machine's domain in a browser, or one a
  // shell remembered.
  const shown = offered.some((domain) => domain.id === section) ? section : 'appearance';
  const named = SETTINGS_DOMAINS.find((domain) => domain.id === shown)!;

  return {
    list: {
      view: 'settings',
      name: t('settings.title'),
      labels: { open: t('settings.list.open'), close: t('settings.list.close'), resize: t('settings.list.resize') },
      chosen: shown,
      body: (
        <DomainList
          label={t('settings.domains')}
          domains={offered.map(({ id, label }) => ({ id, label: t(label) }))}
          chosen={shown}
          onChoose={(id) => onSection?.(id as SettingsSection)}
          // Absent, never disabled (D47 §4), and said where the absence is.
          note={attached ? undefined : t('settings.list.browser')}
        />
      ),
    },
    main: (
      // Keyed by the domain, so choosing another draws its cards anew, their state with them, and opens it
      // at its top rather than at the scroll the last domain was left at.
      <SettingsMain
        key={shown}
        title={t(named.label)}
        Domain={named.component}
        domain={{
          notify, attached, onGo, onAsk: onAskSetup, drivers: browserDrivers, onAttend,
        }}
        anchor={anchor}
        onAnchored={onAnchored}
      />
    ),
  };
}

/**
 * The domain chosen, as the main area (D118 §3b): its header names it, and its cards follow. The part a
 * door named is brought into view once it is drawn (UX5 U72).
 */
function SettingsMain({ title, Domain, domain, anchor, onAnchored }: {
  title: string;
  Domain: ComponentType<SettingsDomainProps>;
  domain: SettingsDomainProps;
  anchor: SettingsAnchor | null;
  onAnchored?: () => void;
}) {
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

  return (
    // The application holds a tool's running action above every view (SIGNIN1); drawn alone, Settings
    // holds its own, so it still works where nothing above does.
    <WithHarnessRuns notify={domain.notify}>
      <ViewMain
        // The domain's name, which the list says too: it is what tells the person where they are when the
        // list is a strip, and why a card alone in its domain still carries no title of its own.
        header={(
          <header className="mb-5">
            <h1 className="text-view font-[650] tracking-[-0.01em]">{title}</h1>
          </header>
        )}
      >
        {/* A card stacked under another keeps its own top margin; the first in a domain does not. */}
        <div className="min-w-0 [&>*:first-child]:mt-0">
          <Domain {...domain} />
        </div>
      </ViewMain>
    </WithHarnessRuns>
  );
}

/**
 * **Settings drawn alone**: its list pane and its main area in a browser's frame of its own, with its own
 * list memory — what its suites and each domain's render, as a surface drawn alone holds its own running
 * action (`WithHarnessRuns`). The application hands `useSettingsLayout` to its frame instead.
 */
export function SettingsView(props: SettingsProps) {
  const lists = useListPanes();
  const [over, setOver] = useState(false);
  return <ViewFrame layout={useSettingsLayout(props)} lists={lists} over={over} onOver={setOver} />;
}
