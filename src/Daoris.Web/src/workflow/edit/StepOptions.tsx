import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { CheckField, SelectField } from '../../design/ui';
import type { EditorRole, OptionField, StepStaffing } from './answers';
import { choiceLabel, optionLabel } from './editWords';
import { type DraftNode, optionOf } from './graphDraft';
import { type RolePlace, StepRole } from './StepRole';

/** The option a step names the role that takes it by (ROLESTEP1b; the driver's `NodeOptionFields.Role`). */
export const ROLE_OPTION = 'role';

/** The option a land names its target line by. */
const TARGET_OPTION = 'target';

/** A choice left unset, and a step left to its work's own role: a select's item needs a value, and an empty one is none. */
const NONE = ':none';

/** A value the step's option holds, as one of its fields reads it. */
export type OptionValue = string | boolean | string[] | undefined;

const fieldClass = 'min-h-[1.9rem] min-w-0 max-w-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink';

/**
 * **A step's options** (WORKFLOWEDIT1b, SETUPUX1r): each field the palette says its kind reads (`NodeOptionFields`), worded in the page's
 * words with the name the saved step uses left to *Details*, drawn by its type, a choice from the code's own list, and the role that takes
 * it offered from the working roles that do its work. A molecule: the step and the roles arrive as props, and each change goes out, an
 * emptied one clearing the option.
 */
export function StepOptions({ node, fields, roleTakes, roles, place = null, staffing, lines = [], notes = {}, onSet }: {
  node: DraftNode;
  fields: OptionField[];
  /** The works a role does this step's work by, its own first; none where no agent session takes it. */
  roleTakes: string[];
  roles: EditorRole[];
  /** The place the editor opened from, which a step's role is given and named for; and the role editor's reads and presses (SETUPUX1t). */
  place?: RolePlace | null;
  staffing?: StepStaffing;
  /** The lines the place declares, which a landing's target is chosen from; none, and the target is typed. */
  lines?: string[];
  /** What the page says under a field, by its name: the tail a method brings, a pattern's preview. */
  notes?: Record<string, string>;
  onSet: (name: string, value: OptionValue) => void;
}) {
  const { t, i18n } = useTranslation();
  const base = useId();
  if (fields.length === 0) return null;
  const kind = node.kind;
  const hint = (name: string) => {
    const key = `workflow.edit.option.${kind}.${name}.hint`;
    const said = [i18n.exists(key) ? t(key) : '', notes[name] ?? ''].filter(Boolean);
    return said.map((words) => <p key={words} className="m-0 text-meta text-ink-faint [overflow-wrap:anywhere]">{words}</p>);
  };

  return (
    <div className="grid min-w-0 gap-2">
      {fields.map((field) => {
        const id = `${base}-${field.name}`;
        const value = optionOf(node, field.name);
        const label = optionLabel(t, kind, field.name);
        if (field.name === ROLE_OPTION && roleTakes.length > 0) {
          return (
            <StepRole
              key={field.name}
              id={id}
              value={typeof value === 'string' && value ? value : ''}
              roleTakes={roleTakes}
              roles={roles}
              place={place}
              staffing={staffing}
              onChoose={(chosen) => onSet(field.name, chosen)}
            />
          );
        }

        if (field.name === TARGET_OPTION && field.type === 'text' && lines.length > 0) {
          const named = typeof value === 'string' && value ? value : '';
          // A line a saved version names that the place does not declare is kept in view, for the judge to say why.
          const offered = named && !lines.includes(named) ? [...lines, named] : lines;
          return (
            <div key={field.name} className="grid min-w-0 gap-1">
              <span id={id} className="text-meta text-ink-soft">{label}</span>
              <SelectField
                ariaLabel={label}
                value={named || NONE}
                onChange={(chosen) => onSet(field.name, chosen === NONE ? undefined : chosen)}
                options={[{ value: NONE, label: t('workflow.edit.option.land.target.own') }, ...offered.map((line) => ({ value: line, label: line }))]}
              />
              {hint(field.name)}
            </div>
          );
        }

        switch (field.type) {
          case 'choice':
            return (
              <div key={field.name} className="grid min-w-0 gap-1">
                <span id={id} className="text-meta text-ink-soft">{label}</span>
                <SelectField
                  ariaLabel={label}
                  value={typeof value === 'string' && value ? value : NONE}
                  onChange={(chosen) => onSet(field.name, chosen === NONE ? undefined : chosen)}
                  options={[
                    ...(field.required ? [] : [{ value: NONE, label: t('workflow.edit.option.none') }]),
                    ...field.choices.map((choice) => ({ value: choice, label: choiceLabel(t, kind, field.name, choice) })),
                  ]}
                />
                {hint(field.name)}
              </div>
            );
          case 'switch':
            return (
              <div key={field.name} className="grid min-w-0 gap-1">
                <CheckField label={label} checked={value === true} onChange={(checked) => onSet(field.name, checked)} />
                {hint(field.name)}
              </div>
            );
          case 'names':
            return (
              <div key={field.name} className="grid min-w-0 gap-1">
                <label htmlFor={id} className="text-meta text-ink-soft">{t('workflow.edit.option.names', { name: label })}</label>
                {/* Read once typed, so a comma being typed is not taken away from under the caret. */}
                <input
                  id={id}
                  className={fieldClass}
                  defaultValue={Array.isArray(value) ? value.join(', ') : ''}
                  onBlur={(event) => onSet(field.name, event.target.value.split(',').map((each) => each.trim()).filter(Boolean))}
                />
                {hint(field.name)}
              </div>
            );
          default:
            return (
              <div key={field.name} className="grid min-w-0 gap-1">
                <label htmlFor={id} className="text-meta text-ink-soft">{label}</label>
                <input
                  id={id}
                  className={fieldClass}
                  aria-required={field.required || undefined}
                  value={typeof value === 'string' ? value : ''}
                  onChange={(event) => onSet(field.name, event.target.value)}
                />
                {hint(field.name)}
              </div>
            );
        }
      })}
    </div>
  );
}
