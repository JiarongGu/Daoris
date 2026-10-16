import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { SystemWiring } from '../data/wiring';
import { Button, PathText, Prose, SettingRow } from '../design/ui';
import { moment, size } from '../locales/format';
import { type Answered, InlineConfirm } from '../work/InlineConfirm';
import { Fact, Unread, WiringCard } from './WiringParts';

/** The catalogue word for where the tool home was read from, in `settings.daoris.toolHome.from.*`. */
const FROM_WORD = ['process', 'account', 'default'];

/**
 * What Daoris's sessions left in the person's own tool home (OWNSPACE2, D175 point 3, D88): the folders whose names equal a
 * working directory Daoris's own records hold, listed with their size and when they were last written, and moved aside for
 * fourteen days (never deleted) only by the person's press after the list. A folder a session may still use is listed and says
 * it stays; the person's own folders are never listed. Read-only until *Move these aside*, which asks first.
 *
 * @remarks
 * A molecule: drawn from props, so every state is reachable without a shell. The driver judges each folder again right before it
 * moves, so the press says what moved, where, and what stayed; the list is what the next read finds. Putting a folder back is not
 * on the page yet: the ask says where they go and for how long.
 */
export function WiringToolHome({ toolHome, measuring = false, onClear }: {
  toolHome: NonNullable<SystemWiring['toolHome']>;
  measuring?: boolean;
  onClear: (folders: { name: string; id: string }[], answered: Answered) => void;
}) {
  const { t, i18n } = useTranslation();
  const [asking, setAsking] = useState(false);
  const { folders } = toolHome;
  const movable = folders.filter((each) => each.keeps === null);
  const counted = movable.every((each) => each.bytes !== null);
  const total = movable.reduce((sum, each) => sum + (each.bytes ?? 0), 0);
  const left = toolHome.ambiguous + toolHome.uncertain;
  const whyKey = `settings.daoris.toolHome.listWhy.${toolHome.why}`;
  return (
    <WiringCard id="settings-daoris-tool-home" title={t('settings.daoris.card.toolHome')}>
      <SettingRow
        label={t('settings.daoris.label.toolHome')}
        hint={toolHome.from && FROM_WORD.includes(toolHome.from) ? t(`settings.daoris.toolHome.from.${toolHome.from}`) : undefined}
        control={<Fact value={toolHome.path} why={toolHome.pathWhy}><PathText path={toolHome.path ?? ''} className="text-small text-ink-soft" /></Fact>}
      />
      <SettingRow label={t('settings.daoris.label.toolFolders')} hint={t('settings.daoris.toolHome.what')}>
        <div className="grid gap-2">
          {toolHome.why && (
            <Prose className="text-small">{i18n.exists(whyKey) ? t(whyKey) : t('settings.daoris.why.unknown')}</Prose>
          )}
          {!toolHome.why && folders.length === 0 && (
            <Prose className="text-small">{t(toolHome.present ? 'settings.daoris.toolHome.none' : 'settings.daoris.toolHome.absent')}</Prose>
          )}
          {folders.length > 0 && (
            <ul className="m-0 grid list-none gap-1.5 p-0">
              {folders.map((each) => (
                <li key={each.name} className="flex flex-wrap items-baseline gap-x-3 text-small text-ink-faint">
                  <span className="font-mono text-ink-soft wrap-anywhere">{each.name}</span>
                  <span>{each.bytes !== null ? size(each.bytes) : <Unread why={measuring && !each.bytesWhy ? 'measuring' : each.bytesWhy} />}</span>
                  {each.newest && <span>{t('settings.daoris.toolHome.newest', { when: moment(each.newest) })}</span>}
                  {each.keeps && <span className="text-ink-soft">{t(`settings.daoris.toolHome.keeps.${each.keeps}`, { defaultValue: each.keeps })}</span>}
                </li>
              ))}
            </ul>
          )}
          {left > 0 && <Prose className="text-small">{t('settings.daoris.toolHome.left', { count: left })}</Prose>}
          {toolHome.caseInsensitive && folders.length > 0 && <Prose className="text-small">{t('settings.daoris.toolHome.caseFolded')}</Prose>}
          {movable.length > 0 && !asking && (
            <p className="m-0">
              <Button variant="ghost" onClick={() => setAsking(true)}>{t('settings.daoris.button.removeThese')}</Button>
            </p>
          )}
          {movable.length > 0 && asking && (
            <InlineConfirm
              label={t('settings.daoris.toolHome.askTitle')}
              says={t(counted ? 'settings.daoris.toolHome.askSays' : 'settings.daoris.toolHome.askSaysUncounted', {
                count: movable.length, size: size(total), days: toolHome.recoveryDays,
              })}
              meanIt={t('settings.daoris.button.removeThem')}
              onConfirm={(answered) => onClear(movable.map((each) => ({ name: each.name, id: each.id })), answered)}
              onClose={() => setAsking(false)}
            />
          )}
        </div>
      </SettingRow>
    </WiringCard>
  );
}
