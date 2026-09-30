import { useTranslation } from 'react-i18next';
import { SelectField } from '../ui';

/** One value an option takes, in the agent's own words. */
export type SessionOptionChoice = { value: string; name: string; description?: string | null };

/**
 * One of a conversation's options as its agent offered it on the protocol door (AGT6b, D98) — the
 * model, or the effort the protocol calls a thought level. The shape lives here so a molecule names it
 * without importing the shell (SURF6).
 */
export type SessionOption = {
  id: string;
  /** What the agent calls it — content, never translated. */
  name: string;
  category: string | null;
  current: string;
  choices: SessionOptionChoice[];
};

/**
 * A conversation's model and effort, beside its composer (AGT6b, D98): one choice each, as the agent
 * offered them, and a change is the organism's to send.
 *
 * @remarks
 * **The agent's words, and Daoris's none.** Each option is named as the agent names it and offers
 * exactly the values it offered, so Daoris names no model of its own (D24); the label only says it is
 * this conversation's. A value the agent reports that it did not list is still shown as itself, rather
 * than as an empty choice.
 */
export function SessionOptions({ options, busy = false, onChange }: {
  options: SessionOption[];
  /** A change is on its way to the agent: nothing more is sent until it answers. */
  busy?: boolean;
  onChange: (option: string, value: string) => void;
}) {
  const { t } = useTranslation();
  if (options.length === 0) return null;

  return (
    <span role="group" aria-label={t('work.options.group')} className="flex flex-wrap items-center gap-1.5">
      {options.map((option) => (
        <SelectField
          key={option.id}
          value={option.current}
          disabled={busy}
          onChange={(value) => { if (value !== option.current) onChange(option.id, value); }}
          ariaLabel={t('work.options.label', { name: option.name })}
          options={[
            ...option.choices.map((choice) => ({ value: choice.value, label: choice.name })),
            ...(option.choices.some((choice) => choice.value === option.current)
              ? [] : [{ value: option.current, label: option.current }]),
          ]}
        />
      ))}
    </span>
  );
}
