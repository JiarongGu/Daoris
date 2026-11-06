import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Prose, SettingRow } from '../../design/ui';
import { type Answered, InlineConfirm } from '../../work/InlineConfirm';

/**
 * *Change the service's address* (WIRING1e, the this-Daoris design §4), in the Service card: the account's `DAORIS_SERVICE_URL`, set
 * to an address of this machine or cleared for the default. The ask says first the old value and the new, that it takes effect when
 * Daoris starts again, and that a terminal reads it too; a malformed address or one that is not this machine's is refused inside it.
 *
 * @remarks
 * A molecule: drawn from props. `account` is the account's own value, null where it is not set, which is the default.
 */
export function WiringAddressRow({ account, busy = false, onPress }: {
  account: string | null;
  busy?: boolean;
  /** The press: the address, or null for the default, and how to tell the ask how it ended. */
  onPress: (url: string | null, answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState<'set' | 'clear' | null>(null);
  const [typed, setTyped] = useState('');
  const next = asking === 'clear' ? null : typed.trim();
  const was = account ?? t('settings.daoris.setup.address.default');

  return (
    <SettingRow
      label={t('settings.daoris.label.changeAddress')}
      hint={t('settings.daoris.setup.address.hint')}
      control={!asking && (
        <>
          <Button disabled={busy} onClick={() => { setTyped(account ?? ''); setAsking('set'); }}>{t('settings.daoris.button.addressChange')}</Button>
          {account !== null && <Button disabled={busy} onClick={() => setAsking('clear')}>{t('settings.daoris.button.addressDefault')}</Button>}
        </>
      )}
    >
      {asking && (
        <div className="grid gap-2">
          {asking === 'set' && (
            <input
              type="text"
              aria-label={t('settings.daoris.setup.address.input')}
              value={typed}
              disabled={busy}
              onChange={(event) => setTyped(event.target.value)}
              placeholder="http://127.0.0.1:5198"
              className="w-full rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-body text-ink"
            />
          )}
          <InlineConfirm
            tone="primary"
            block
            label={t('settings.daoris.setup.address.label')}
            meanIt={t('settings.daoris.button.addressConfirm')}
            busy={busy}
            ready={next === null || next.length > 0}
            onConfirm={(answered) => onPress(next === '' ? null : next, answered)}
            onClose={() => setAsking(null)}
            className="min-w-0"
            says={(
              <div className="grid gap-1.5 text-small text-ink">
                <Prose className="m-0">{t('settings.daoris.setup.address.says', { was, next: next ?? t('settings.daoris.setup.address.default') })}</Prose>
                <Prose className="m-0">{t('settings.daoris.setup.address.later')}</Prose>
              </div>
            )}
          />
        </div>
      )}
    </SettingRow>
  );
}
