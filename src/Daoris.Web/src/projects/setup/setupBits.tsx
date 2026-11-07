import { useTranslation } from 'react-i18next';
import { Button, CheckField, Chip, Icon, Inline, SelectField, SettingRow } from '../../design/ui';
import type { LandsHere, ReachSetup } from './RepositorySetup';

export const JOIN = ' · ';

/**
 * The switch *Daoris may change this checkout's files*: off by default; on, it says whether sessions already run in
 * this checkout, which a later act of Daoris's here would meet. It writes nothing to the repository.
 */
export function CheckoutSwitch({ checkout, busy }: { checkout: NonNullable<ReachSetup['checkout']>; busy: boolean }) {
  const { t } = useTranslation();
  const label = t('projects.setup.checkout.label');
  return (
    <SettingRow
      label={label}
      why={t('projects.setup.checkout.why')}
      control={<CheckField checked={checkout.on} disabled={busy} onChange={checkout.onChange} label={label} hideLabel />}
    >
      {checkout.on && checkout.running === null && <p className="m-0 text-small text-ink-soft">{t('projects.setup.checkout.unread')}</p>}
      {checkout.on && typeof checkout.running === 'number' && (
        <p data-checkout-sessions="" className="m-0 text-small text-ink-soft">
          {checkout.running === 0 ? t('projects.setup.checkout.none') : t('projects.setup.checkout.running', { count: checkout.running })}
        </p>
      )}
    </SettingRow>
  );
}

/** How its work lands: the saved version's own `land` options, edited here. */
export function LandingSetting({ twin, lands }: {
  /** The terminal twin of the row: a repository's, or a workspace's. */
  twin: string;
  lands: Extract<LandsHere, { is: 'version' }>;
}) {
  const { t } = useTranslation();
  return (
    <SettingRow label={t('projects.setup.landing')} hint={twin}>
      <p className="m-0 text-small text-ink-soft"><Inline text={lands.summary} /></p>
      <div className="mt-2">{lands.editor}</div>
    </SettingRow>
  );
}

/**
 * What its sessions may also write into: each declared relationship with its take-back, and a choice of the other
 * repositories of its workspace. A declaration is the person's standing say-so, in one direction.
 */
export function WritesInto({ repository, writesTo, candidates, busy, onWrite }: {
  repository: string;
  writesTo: string[];
  candidates: string[];
  busy: boolean;
  onWrite: (to: string, allow: boolean) => void;
}) {
  const { t } = useTranslation();
  return (
    <SettingRow
      label={t('projects.setup.writes')}
      hint={t('projects.setup.twin.writes', { repository })}
      control={candidates.length > 0 && (
        <SelectField
          value=""
          disabled={busy}
          ariaLabel={t('settings.across.addWrite', { repository })}
          placeholder={t('settings.across.addWritePlaceholder')}
          options={candidates.map((name) => ({ value: name, label: name }))}
          onChange={(to) => to && onWrite(to, true)}
        />
      )}
    >
      {writesTo.length === 0
        ? <p className="m-0 text-small text-ink-soft">{t('settings.across.writesNone')}</p>
        : (
          <ul aria-label={t('settings.across.writesIntoList', { repository })} className="m-0 flex list-none flex-wrap gap-1.5 p-0">
            {writesTo.map((to) => (
              <li key={to} className="inline-flex items-center gap-0.5">
                <Chip>{to}</Chip>
                <Button
                  variant="ghost"
                  disabled={busy}
                  aria-label={t('settings.across.stopWriting', { repository, to })}
                  onClick={() => onWrite(to, false)}
                >
                  <Icon name="x" size={12} />
                </Button>
              </li>
            ))}
          </ul>
        )}
    </SettingRow>
  );
}
