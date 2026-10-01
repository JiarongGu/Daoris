import { useTranslation } from 'react-i18next';
import { Button, Inline, Pill, Prose } from '../ui';
import type { KitPoint } from '../settings/PluginKit';
import { ViewMain } from '../work/ViewMain';
import type { OfferShown } from './catalog';
import { Lead, NameRows, PageHead, PageSection, PointRows } from './PluginPage';

/**
 * **One of Daoris's own plugins, not installed here** (D119 §3.2, D103): a header with its name, its version,
 * *not installed* and *Install*; its description; Points, Agents and Servers from its manifest; *What it needs*, its
 * README's bullets verbatim; and a line saying that installing runs nothing.
 *
 * @remarks
 * **A molecule**: the press goes out, and the organism chooses the plugin it became. An offer that cannot be installed as
 * it stands gives the driver's sentence in *Install*'s place. Its README's words are content, shown as they are.
 */
export function OfferPage({ offer, kitPoints, installing = false, onInstall }: {
  offer: OfferShown;
  kitPoints?: KitPoint[] | null;
  installing?: boolean;
  onInstall: (id: string) => void;
}) {
  const { t } = useTranslation();

  const head = (
    <PageHead
      title={offer.name || offer.id}
      version={offer.version}
      pills={<Pill tone="neutral">{t('plugin.offers.notInstalled')}</Pill>}
      id={offer.id}
      line={offer.description}
      acts={!offer.problem && (
        <Button
          disabled={installing}
          aria-busy={installing || undefined}
          aria-label={t('plugin.offers.installNamed', { id: offer.id })}
          onClick={() => onInstall(offer.id)}
        >
          {t('plugin.offers.install')}
        </Button>
      )}
    />
  );

  return (
    <ViewMain header={head}>
      {offer.problem && <Lead text={offer.problem} />}

      {offer.points.length > 0 && (
        <PageSection title={t('plugin.section.points')}><PointRows points={offer.points} kitPoints={kitPoints} /></PageSection>
      )}
      {offer.harnesses.length > 0 && (
        <PageSection title={t('plugin.section.agents')}><NameRows names={offer.harnesses} /></PageSection>
      )}
      {offer.servers.length > 0 && (
        <PageSection title={t('plugin.section.servers')}><NameRows names={offer.servers} /></PageSection>
      )}
      {offer.needs.length > 0 && (
        <PageSection title={t('plugin.section.needs')}>
          <ul className="m-0 max-w-prose list-disc pl-5 text-small text-ink-soft">
            {offer.needs.map((need) => <li key={need} className="break-words"><Inline text={need} /></li>)}
          </ul>
        </PageSection>
      )}

      <Prose className="mt-6 text-small">{t('plugin.offer.note')}</Prose>
      <p className="m-0 mt-8 border-t border-line pt-3 text-meta text-ink-faint">
        <Inline text={t('plugin.offer.terminal', { id: offer.id })} />
      </p>
    </ViewMain>
  );
}
