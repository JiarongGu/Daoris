import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { SelectField } from '../../design/ui';

const DEFAULT = '__default';
const OTHER = '__other';

const FIELD = 'w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint';

/**
 * A model or an effort chosen from what the agent itself offers, with *other…* for a name typed (the account editor's way); where the
 * agent offers nothing the field is the typed name alone. An empty value is the account's default.
 */
export function OfferedField({ value, offered, label, otherLabel, onChange }: {
  value: string;
  /** The agent's own words, or null/undefined where it says none. */
  offered: readonly string[] | null | undefined;
  label: string;
  otherLabel: string;
  onChange: (value: string) => void;
}) {
  const { t } = useTranslation();
  const [other, setOther] = useState(value !== '' && !!offered && !offered.includes(value));
  const typed = (
    <input
      aria-label={offered ? otherLabel : label}
      value={value}
      spellCheck={false}
      placeholder={t('settings.ai.actors.accountDefault')}
      onChange={(event) => onChange(event.target.value)}
      className={FIELD}
    />
  );
  if (!offered) return typed;

  const shown = other ? OTHER : value === '' ? DEFAULT : value;
  return (
    <div className="grid gap-1">
      <SelectField
        value={offered.includes(shown) || shown === DEFAULT || shown === OTHER ? shown : OTHER}
        ariaLabel={label}
        options={[
          { value: DEFAULT, label: t('settings.ai.actors.accountDefault') },
          ...offered.map((each) => ({ value: each, label: each })),
          { value: OTHER, label: t('settings.ai.roles.otherName') },
        ]}
        onChange={(next) => {
          setOther(next === OTHER);
          onChange(next === DEFAULT || next === OTHER ? '' : next);
        }}
      />
      {other && typed}
    </div>
  );
}
