import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { SystemWiring } from '../../data/wiring';
import { Button, PathText, Prose, SettingRow } from '../../design/ui';
import { type Answered, InlineConfirm } from '../../work/InlineConfirm';

/**
 * *Put on my PATH* and *Take off my PATH* (WIRING1e, the this-Daoris design §4), in the Terminal card. Each says first what it will
 * do, inline: the folder, that it is **your account's PATH only**, never the machine's, that terminals opened afterwards find `daoris`
 * and `daoris-driver`, and which `daoris` comes first if another does. A development run has no launchers and says so with no press.
 *
 * @remarks
 * A molecule: drawn from props. The domain sends the press and tells the ask how it ended (a refusal is said inside it).
 */
export function WiringPathRow({ terminal, busy = false, onPress }: {
  terminal: Pick<SystemWiring['terminal'], 'bin' | 'onAccountPath' | 'firstDaoris'>;
  busy?: boolean;
  /** The press: whether to put the folder on, and how to tell the ask how it ended. */
  onPress: (on: boolean, answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(false);
  const { bin, onAccountPath, firstDaoris } = terminal;
  const on = onAccountPath === true;
  // Read only where the account's PATH could be: an unread one is never written over.
  const offered = bin != null && onAccountPath !== null && onAccountPath !== undefined;

  return (
    <SettingRow
      label={t('settings.daoris.label.putOnPath')}
      control={offered
        ? !asking && <Button disabled={busy} onClick={() => setAsking(true)}>{t(on ? 'settings.daoris.button.pathOff' : 'settings.daoris.button.pathOn')}</Button>
        : <span className="text-small text-ink-faint">{t(bin == null ? 'settings.daoris.setup.path.development' : 'settings.daoris.setup.path.unread')}</span>}
    >
      {asking && bin != null && (
        <InlineConfirm
          tone="primary"
          block
          label={t(on ? 'settings.daoris.setup.path.offLabel' : 'settings.daoris.setup.path.onLabel')}
          meanIt={t(on ? 'settings.daoris.button.pathOffConfirm' : 'settings.daoris.button.pathOnConfirm')}
          busy={busy}
          onConfirm={(answered) => onPress(!on, answered)}
          onClose={() => setAsking(false)}
          className="min-w-0"
          says={(
            <div className="grid gap-1.5 text-small text-ink">
              <Prose className="m-0">{t(on ? 'settings.daoris.setup.path.offSays' : 'settings.daoris.setup.path.onSays')}</Prose>
              <PathText path={bin} className="text-small text-ink-soft" />
              <Prose className="m-0">{t('settings.daoris.setup.path.accountOnly')}</Prose>
              {!on && <Prose className="m-0">{t('settings.daoris.setup.path.terminals')}</Prose>}
              {!on && firstDaoris.is === 'another' && (
                <Prose className="m-0">{t('settings.daoris.setup.path.another', { path: firstDaoris.path ?? '' })}</Prose>
              )}
            </div>
          )}
        />
      )}
    </SettingRow>
  );
}
