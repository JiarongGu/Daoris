import { useTranslation } from 'react-i18next';
import type { TrustHold } from '../signals';
import { Button, PathText, WaitingCard } from '../ui';

/**
 * The agent's trust question, asked by Daoris for a folder the driver is holding (D73).
 *
 * @remarks
 * **The grant is the person's, and this is where they give it.** The agent ignores a folder's own
 * `permissions.allow` until someone has trusted it there (DEPLOY1, both doors, measured), so the
 * driver holds rather than spend a session that could not take its quest. The flag is the person's
 * grant, so this asks exactly what the agent would ask, names the one file that is written, and
 * grants only on the press. Nothing grants it at adoption, sync or spawn.
 *
 * **Never wider than the hold.** It is handed a hold the driver produced: the folder and the file,
 * as a pair. The shell refuses any other pair, and the terminal's `daoris agent trust … --yes` is
 * the door that names any folder.
 *
 * A molecule: handed the hold, it reports the grant and the putting-down. It sits inline in a
 * quest's drawer and inside the drawer the band's row opens.
 */
export function TrustAsk({ hold, busy = false, onGrant, onCancel }: {
  hold: TrustHold;
  /** The grant is being written: the press is held so it cannot be given twice. */
  busy?: boolean;
  onGrant: () => void;
  /** Put the question down. Absent where there is nothing to return to. */
  onCancel?: () => void;
}) {
  const { t } = useTranslation();

  return (
    <WaitingCard title={t('trust.title')}>
      <p className="m-0 mt-1.5 text-meta text-ink"><PathText path={hold.folder} /></p>
      <p className="m-0 mt-2 text-body leading-relaxed">{t('trust.what')}</p>
      <p className="m-0 mt-1.5 text-small text-ink-soft">
        {hold.quest
          ? t('trust.holding.quest', { id: hold.quest })
          : t('trust.holding.ask', { id: hold.ask ?? '' })}
      </p>
      <p className="m-0 mt-2 text-small text-ink-faint">{t('trust.file')}</p>
      <p className="m-0 mt-0.5 text-meta text-ink-faint"><PathText path={hold.trustFile} /></p>
      <div className="mt-3 flex flex-wrap gap-2">
        <Button variant="primary" disabled={busy} onClick={onGrant}>{t('trust.grant')}</Button>
        {onCancel && <Button variant="ghost" onClick={onCancel}>{t('trust.cancel')}</Button>}
      </div>
    </WaitingCard>
  );
}
