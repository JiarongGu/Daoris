import { useTranslation } from 'react-i18next';
import type { SystemWiring, WiringVariable } from '../../data/wiring';
import { PathText, Pill, SettingRow } from '../../design/ui';
import { Fold, Unread, WiringCard } from './WiringParts';

/** What a set variable's value is: a key says it is set with the audit prefix, a value is itself. */
function Value({ variable }: { variable: WiringVariable }) {
  const { t } = useTranslation();
  if (variable.secret) return <Pill tone="done">{t('settings.daoris.variables.key', { prefix: variable.prefix ?? '' })}</Pill>;
  if (variable.value === null) return <Unread why={variable.valueWhy} />;
  return <PathText path={variable.value} className="text-small text-ink-soft" />;
}

/** What is used where a variable is not set: a code the catalogue words, else the value itself (a number, an address). */
function Instead({ what }: { what: string }) {
  const { t, i18n } = useTranslation();
  const key = `settings.daoris.instead.${what}`;
  return <>{/^[a-z-]+$/.test(what) && i18n.exists(key) ? t(key) : what}</>;
}

function SetRow({ variable }: { variable: WiringVariable }) {
  const { t } = useTranslation();
  const readers = variable.readBy.map((reader) => t(`settings.daoris.readBy.${reader}`, { defaultValue: reader })).join(', ');
  // Where the window's value differs from the account's, both (§3.5); a key is never shown, so never compared.
  const differs = !variable.secret && variable.accountSet === true && variable.account !== variable.value;
  return (
    <SettingRow
      label={<span className="font-mono">{variable.name}</span>}
      hint={(
        <span className="grid gap-0.5">
          <span>{t(`settings.daoris.variable.${variable.from}`)}</span>
          {readers && <span>{t('settings.daoris.variables.readBy', { readers })}</span>}
        </span>
      )}
      control={<Value variable={variable} />}
    >
      {differs && variable.account !== null && (
        <p className="m-0 text-small text-ink-soft">{t('settings.daoris.variables.account', { value: variable.account })}</p>
      )}
    </SettingRow>
  );
}

function UnsetRow({ variable }: { variable: WiringVariable }) {
  const { t } = useTranslation();
  return (
    <SettingRow
      label={<span className="font-mono">{variable.name}</span>}
      control={variable.from === 'unknown'
        ? <Unread why={variable.valueWhy} />
        : variable.instead
          ? <span className="text-small text-ink-soft">{t('settings.daoris.variables.instead')}{' '}<Instead what={variable.instead} /></span>
          : <span className="text-small text-ink-faint">{t('settings.daoris.why.not-set')}</span>}
    />
  );
}

/**
 * Variables (the this-Daoris design §3.5): every variable Daoris reads, from the code's one list. A set one is a row: its
 * value, where it came from, who reads it, and the account's where that differs; a key says *set* with its audit prefix and
 * never its value (§5). The ones not set fold under, each with what is used instead.
 *
 * @remarks
 * A molecule: drawn from props. An adopted service's own variables are not this window's to know, so the row says so.
 */
export function WiringVariablesCard({ variables }: { variables: SystemWiring['variables'] }) {
  const { t } = useTranslation();
  const set = variables.filter((variable) => variable.set);
  const unset = variables.filter((variable) => !variable.set);
  return (
    <WiringCard id="settings-daoris-variables" title={t('settings.daoris.card.variables')}>
      {set.map((variable) => <SetRow key={variable.name} variable={variable} />)}
      {unset.length > 0 && (
        <Fold label={t('settings.daoris.variables.unset', { count: unset.length })}>
          {unset.map((variable) => <UnsetRow key={variable.name} variable={variable} />)}
        </Fold>
      )}
    </WiringCard>
  );
}
