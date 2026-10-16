import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { SystemWiring } from '../data/wiring';
import { Button, PathText, Prose, SettingRow } from '../design/ui';
import { moment, size } from '../locales/format';
import { type Answered, InlineConfirm } from '../work/InlineConfirm';
import { Fact, Unread, WiringCard } from './WiringParts';

/** The catalogue word for where the tool home was read from, in `settings.daoris.toolHome.from.*`. */
const FROM_WORD = ['variable', 'default'];

/**
 * What Daoris's sessions left in the person's own tool home (OWNSPACE2, D175 point 3, D88): the project folders its own
 * working roots made there, listed by name with their size and when they were last written, and removed only by the person's
 * press after the list. The person's own projects are never listed. Read-only until *Remove these*, which asks first.
 *
 * @remarks
 * A molecule: drawn from props, so every state is reachable without a shell. The driver judges each folder again right before it
 * goes, so the press says what was removed and what stayed; the list is what the next read finds.
 */
export function WiringToolHome({ toolHome, measuring = false, onClear }: {
  toolHome: NonNullable<SystemWiring['toolHome']>;
  measuring?: boolean;
  onClear: (names: string[], answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(false);
  const { folders } = toolHome;
  const counted = folders.every((each) => each.bytes !== null);
  const total = folders.reduce((sum, each) => sum + (each.bytes ?? 0), 0);
  return (
    <WiringCard id="settings-daoris-tool-home" title={t('settings.daoris.card.toolHome')}>
      <SettingRow
        label={t('settings.daoris.label.toolHome')}
        hint={toolHome.from && FROM_WORD.includes(toolHome.from) ? t(`settings.daoris.toolHome.from.${toolHome.from}`) : undefined}
        control={<Fact value={toolHome.path} why={toolHome.pathWhy}><PathText path={toolHome.path ?? ''} className="text-small text-ink-soft" /></Fact>}
      />
      <SettingRow label={t('settings.daoris.label.toolFolders')} hint={t('settings.daoris.toolHome.what')}>
        {folders.length === 0 ? (
          <Prose className="text-small">{t(toolHome.present ? 'settings.daoris.toolHome.none' : 'settings.daoris.toolHome.absent')}</Prose>
        ) : (
          <div className="grid gap-2">
            <ul className="m-0 grid list-none gap-1.5 p-0">
              {folders.map((each) => (
                <li key={each.name} className="flex flex-wrap items-baseline gap-x-3 text-small text-ink-faint">
                  <span className="font-mono text-ink-soft wrap-anywhere">{each.name}</span>
                  <span>{each.bytes !== null ? size(each.bytes) : <Unread why={measuring && !each.bytesWhy ? 'measuring' : each.bytesWhy} />}</span>
                  {each.newest && <span>{t('settings.daoris.toolHome.newest', { when: moment(each.newest) })}</span>}
                </li>
              ))}
            </ul>
            {!asking && (
              <p className="m-0">
                <Button variant="ghost" onClick={() => setAsking(true)}>{t('settings.daoris.button.removeThese')}</Button>
              </p>
            )}
            {asking && (
              <InlineConfirm
                label={t('settings.daoris.toolHome.askTitle')}
                says={t(counted ? 'settings.daoris.toolHome.askSays' : 'settings.daoris.toolHome.askSaysUncounted', { count: folders.length, size: size(total) })}
                meanIt={t('settings.daoris.button.removeThem')}
                onConfirm={(answered) => onClear(folders.map((each) => each.name), answered)}
                onClose={() => setAsking(false)}
              />
            )}
          </div>
        )}
      </SettingRow>
    </WiringCard>
  );
}
