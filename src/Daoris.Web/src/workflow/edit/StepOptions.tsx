import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { CheckField, SelectField } from '../../design/ui';
import { authoredText } from '../../locales/authored';
import type { EditorRole, OptionField } from './answers';
import { type DraftNode, optionOf } from './graphDraft';

/** The option a step names the role that takes it by (ROLESTEP1b; the driver's `NodeOptionFields.Role`). */
export const ROLE_OPTION = 'role';

/** A choice left unset, and a step left to its work's own role: a select's item needs a value, and an empty one is none. */
const NONE = ':none';

/** A value the step's option holds, as one of its fields reads it. */
export type OptionValue = string | boolean | string[] | undefined;

const fieldClass = 'min-h-[1.9rem] min-w-0 max-w-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink';

/** A role's name in the page's language, or its id where its record names none. */
const roleName = (role: EditorRole, language: string) => authoredText(role.name, language) || role.id;

/**
 * **A step's options** (WORKFLOWEDIT1b): each field the palette says its kind reads (`NodeOptionFields`), drawn by its type, a choice
 * from the code's own list, and the role that takes it offered from the working roles that do its work. A molecule: the step and the
 * roles arrive as props, and each change goes out, an emptied one clearing the option.
 */
export function StepOptions({ node, fields, roleTakes, roles, onSet }: {
  node: DraftNode;
  fields: OptionField[];
  /** The works a role does this step's work by, its own first; none where no agent session takes it. */
  roleTakes: string[];
  roles: EditorRole[];
  onSet: (name: string, value: OptionValue) => void;
}) {
  const { t, i18n } = useTranslation();
  const base = useId();
  if (fields.length === 0) return null;

  return (
    <div className="grid min-w-0 gap-2">
      {fields.map((field) => {
        const id = `${base}-${field.name}`;
        const value = optionOf(node, field.name);
        if (field.name === ROLE_OPTION && roleTakes.length > 0) {
          const taking = roles.filter((role) => role.takes.some((work) => roleTakes.includes(work)));
          const named = typeof value === 'string' && value ? value : '';
          // A role a saved version names that no longer does the work is kept in view, for the judge to say why.
          const offered = named && !taking.some((role) => role.id === named) ? [...taking, { id: named, name: null, takes: [] }] : taking;
          return (
            <div key={field.name} className="grid min-w-0 gap-1">
              <span id={id} className="text-meta text-ink-soft">{t('workflow.edit.role.label')}</span>
              <SelectField
                ariaLabel={t('workflow.edit.role.label')}
                value={named || NONE}
                onChange={(chosen) => onSet(field.name, chosen === NONE ? undefined : chosen)}
                options={[
                  { value: NONE, label: t('workflow.edit.role.own') },
                  ...offered.map((role) => ({ value: role.id, label: roleName(role, i18n.language) })),
                ]}
              />
              {taking.length === 0 && <p className="m-0 text-meta text-ink-faint">{t('workflow.edit.role.none')}</p>}
            </div>
          );
        }

        switch (field.type) {
          case 'choice':
            return (
              <div key={field.name} className="grid min-w-0 gap-1">
                <code id={id} className="text-meta text-ink-soft">{field.name}</code>
                <SelectField
                  ariaLabel={field.name}
                  value={typeof value === 'string' && value ? value : NONE}
                  onChange={(chosen) => onSet(field.name, chosen === NONE ? undefined : chosen)}
                  options={[
                    ...(field.required ? [] : [{ value: NONE, label: t('workflow.edit.option.none') }]),
                    ...field.choices.map((choice) => ({ value: choice, label: choice })),
                  ]}
                />
              </div>
            );
          case 'switch':
            return <CheckField key={field.name} label={field.name} checked={value === true} onChange={(checked) => onSet(field.name, checked)} />;
          case 'names':
            return (
              <label key={field.name} className="grid min-w-0 gap-1">
                <span className="text-meta text-ink-soft">{t('workflow.edit.option.names', { name: field.name })}</span>
                {/* Read once typed, so a comma being typed is not taken away from under the caret. */}
                <input
                  className={fieldClass}
                  defaultValue={Array.isArray(value) ? value.join(', ') : ''}
                  onBlur={(event) => onSet(field.name, event.target.value.split(',').map((each) => each.trim()).filter(Boolean))}
                />
              </label>
            );
          default:
            return (
              <label key={field.name} className="grid min-w-0 gap-1">
                <code className="text-meta text-ink-soft">{field.name}</code>
                <input
                  className={fieldClass}
                  aria-required={field.required || undefined}
                  value={typeof value === 'string' ? value : ''}
                  onChange={(event) => onSet(field.name, event.target.value)}
                />
              </label>
            );
        }
      })}
    </div>
  );
}
