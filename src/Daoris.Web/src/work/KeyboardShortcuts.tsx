import { useTranslation } from 'react-i18next';
import { Drawer, Prose, SectionTitle } from '../ui';

/** One menu's keys, as the drawer prints them: the menu's name, then each item's name and its keys. */
export type ShortcutGroup = { menu: string; rows: { label: string; keys: string[] }[] };

/**
 * Help › *Keyboard shortcuts* (UX7a, D152 §3.2): every key the window answers, by menu, in the reader's language, read
 * from the one table that builds the menus, so the drawer never lists a key the menus do not print.
 *
 * @remarks
 * Before it no list of keys existed anywhere; nine keys were answered and three were printed. A drawer, as *About Daoris*
 * is (D41 §4): a thing read once and closed. **A molecule**: the groups and the notes arrive, a close goes out.
 */
export function KeyboardShortcuts({ groups, browser = false, onClose }: {
  groups: readonly ShortcutGroup[];
  /** A browser: no Alt or F10 for the bar, and a sentence on the keys it keeps. */
  browser?: boolean;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  return (
    <Drawer title={t('menu.help.shortcuts')} onClose={onClose}>
      {groups.map((group) => (
        // Each menu set off from the one above it: a section title is the first thing in its section, so its own margin is none.
        <section key={group.menu} aria-label={group.menu} className="mt-5 first:mt-0">
          <SectionTitle>{group.menu}</SectionTitle>
          <dl className="m-0 grid grid-cols-[1fr_auto] gap-x-4 gap-y-1 text-small">
            {group.rows.map((row) => (
              <div key={row.label} className="contents">
                <dt className="min-w-0 truncate text-ink">{row.label}</dt>
                <dd className="m-0 flex flex-wrap justify-end gap-1">
                  {row.keys.map((key) => (
                    <kbd key={key} className="rounded border border-line px-1 font-mono text-meta text-ink-soft">{key}</kbd>
                  ))}
                </dd>
              </div>
            ))}
          </dl>
        </section>
      ))}
      <div className="mt-5 flex flex-col gap-2 border-t border-line pt-3">
        {!browser && <Prose className="text-small">{t('menu.keys.bar')}</Prose>}
        <Prose className="text-small">{t('menu.keys.field')}</Prose>
        {browser && <Prose className="text-small">{t('menu.keys.browser')}</Prose>}
      </div>
    </Drawer>
  );
}
