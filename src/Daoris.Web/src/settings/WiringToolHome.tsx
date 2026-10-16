import { useTranslation } from 'react-i18next';
import type { SystemWiring } from '../data/wiring';
import { PathText, Prose, SettingRow } from '../design/ui';
import { moment, size } from '../locales/format';
import { Fact, Unread, WiringCard } from './WiringParts';

/** The catalogue word for where the tool home was read from, in `settings.daoris.toolHome.from.*`. */
const FROM_WORD = ['process', 'account', 'default'];

/** How many session records a row names before it counts the rest. */
const NAMED = 2;

/**
 * What Daoris's sessions left in the person's own tool home (OWNSPACE2, D175 point 3, D88): the folders whose names equal a
 * working directory in which one of Daoris's own session records shows a session ran, listed with the records that name them,
 * their size, when they were last written, and what would keep each from a removal. **Read-only: Daoris removes nothing here
 * yet** (OWNSPACE2e hardens the press), so the card says they can be removed by hand. The person's own folders are never listed.
 *
 * @remarks
 * A molecule: drawn from props, so every state is reachable without a shell.
 */
export function WiringToolHome({ toolHome, measuring = false }: {
  toolHome: NonNullable<SystemWiring['toolHome']>;
  measuring?: boolean;
}) {
  const { t, i18n } = useTranslation();
  const { folders } = toolHome;
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
                <li key={each.name} className="grid gap-0.5 text-small text-ink-faint">
                  <span className="flex flex-wrap items-baseline gap-x-3">
                    <span className="font-mono text-ink-soft wrap-anywhere">{each.name}</span>
                    <span>{each.bytes !== null ? size(each.bytes) : <Unread why={measuring && !each.bytesWhy ? 'measuring' : each.bytesWhy} />}</span>
                    {each.newest && <span>{t('settings.daoris.toolHome.newest', { when: moment(each.newest) })}</span>}
                  </span>
                  {each.by.length > 0 && (
                    <span>
                      {t('settings.daoris.toolHome.by', {
                        ids: each.by.slice(0, NAMED).join(', ') + (each.by.length > NAMED ? ` +${each.by.length - NAMED}` : ''),
                      })}
                    </span>
                  )}
                  {each.keeps && <span className="text-ink-soft">{t(`settings.daoris.toolHome.keeps.${each.keeps}`, { defaultValue: each.keeps })}</span>}
                </li>
              ))}
            </ul>
          )}
          {left > 0 && <Prose className="text-small">{t('settings.daoris.toolHome.left', { count: left })}</Prose>}
          {toolHome.caseInsensitive && folders.length > 0 && <Prose className="text-small">{t('settings.daoris.toolHome.caseFolded')}</Prose>}
          {folders.length > 0 && <Prose className="text-small">{t('settings.daoris.toolHome.byHand')}</Prose>}
        </div>
      </SettingRow>
    </WiringCard>
  );
}
