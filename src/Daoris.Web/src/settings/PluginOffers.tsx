import { useTranslation } from 'react-i18next';
import { Button, Card, Inline, Pill, Prose, SectionTitle, SettingRow } from '../ui';

/**
 * One of Daoris's own plugins the install carries (PLUG9 d, D103), as the driver's `PLUGINS` answers it:
 * what it declares, what its README says it needs, and whether this machine has installed it. The
 * bridge leaves a null out, so an older shell's offer may carry no `problem`.
 */
export type PluginOfferShown = {
  id: string;
  name: string;
  version: string;
  description: string;
  problem?: string | null;
  harnesses: string[];
  points: string[];
  servers: string[];
  needs: string[];
  installed: boolean;
  /** Its declared icon as its own bytes (PLUGUI2, D140 §3.2), never a path; absent until the host answers it. */
  icon?: string | null;
  iconProblem?: string | null;
};

/**
 * Daoris's own plugins, offered by the install and not installed here (PLUG9 d, D103): each with what it
 * speaks on or hands, and what it needs on the machine in its README's own words, and an Install that is
 * `daoris plugin add --offer <id>`'s copy. 🔴 Installing one runs nothing; the driver starts it later, and
 * a landing plugin only where a workspace's rule names it.
 */
export function PluginOffersCard({ offers, busy = false, onInstall }: {
  offers: PluginOfferShown[];
  busy?: boolean;
  onInstall: (id: string) => void;
}) {
  const { t } = useTranslation();
  const shown = offers.filter((offer) => !offer.installed);
  if (shown.length === 0) return null;

  const what = (offer: PluginOfferShown) => [
    offer.harnesses.length > 0 ? t('plugin.declares', { harnesses: offer.harnesses.join(', ') }) : null,
    offer.points.length > 0 ? t('plugin.speaks', { points: offer.points.join(', ') }) : null,
    offer.servers.length > 0 ? t('plugin.offers.hands', { servers: offer.servers.join(', ') }) : null,
  ].filter(Boolean).join('; ') || t('plugin.quiet');

  return (
    <Card id="settings-plugin-offers" className="mt-3.5 scroll-mt-3">
      <SectionTitle>{t('plugin.offers.title')}</SectionTitle>
      <Prose className="mb-2 mt-1 text-small text-ink-soft"><Inline text={t('plugin.offers.body')} /></Prose>
      {shown.map((offer) => (
        <SettingRow
          key={offer.id}
          label={(
            <span className="flex flex-wrap items-center gap-2">
              <span>{offer.name}</span>
              {offer.version && <span className="font-mono text-small text-ink-faint">{offer.version}</span>}
              <Pill tone="neutral">{t('plugin.offers.notInstalled')}</Pill>
            </span>
          )}
          hint={(
            <span className="flex flex-col gap-0.5">
              {!offer.problem && <span>{what(offer)}{offer.description ? ` — ${offer.description}` : ''}</span>}
              <span className="font-mono text-meta">{offer.id}</span>
            </span>
          )}
          control={!offer.problem && (
            <Button
              variant="ghost"
              disabled={busy}
              aria-label={t('plugin.offers.installNamed', { id: offer.id })}
              onClick={() => onInstall(offer.id)}
            >
              {t('plugin.offers.install')}
            </Button>
          )}
        >
          {/* The driver's own sentence for one that cannot be installed as it stands: content, not chrome. */}
          {offer.problem && (
            <p className="border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft">
              <Inline text={offer.problem} />
            </p>
          )}
          {/* What it needs, in its README's words, which a person sets up themselves: content, not chrome. */}
          {!offer.problem && offer.needs.length > 0 && (
            <div className="text-small text-ink-soft">
              <p className="m-0 font-medium">{t('plugin.offers.needs')}</p>
              <ul aria-label={t('plugin.offers.needsOf', { id: offer.id })} className="m-0 mt-0.5 list-disc pl-5">
                {offer.needs.map((need) => <li key={need} className="break-words"><Inline text={need} /></li>)}
              </ul>
            </div>
          )}
        </SettingRow>
      ))}
      <p className="m-0 mt-3 text-meta text-ink-faint"><Inline text={t('plugin.offers.terminal')} /></p>
    </Card>
  );
}
