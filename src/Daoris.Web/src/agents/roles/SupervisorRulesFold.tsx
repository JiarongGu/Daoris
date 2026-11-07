import type { TFunction } from 'i18next';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { RoleChangePress, SupervisorRuleWire, SupervisorRulesRead } from '../../data/wiring';
import { Button, FormField, FormGrid, FormRow, Icon, SelectField, WhyGlyph } from '../../design/ui';
import { list } from '../../locales/format';
import { type Answered, Refused } from '../../work/InlineConfirm';
import { NumberField } from './RolePolicy';
import {
  blankRule, draftOf, inheritedFrom, MACHINE, nextCount, pressOf, readable, reaction, recount, replaced, type RuleDraft, rowOf, sameAsStored, setOf,
} from './supervisorRulesDraft';

const ANY = '';
const COLUMNS = ['name', 'number', 'name', 'short', 'acts'] as const;

/** A rule in the person's words, as the inherited lines and the *replaces* line say it. */
export function ruleSaid(t: TFunction, rule: Pick<SupervisorRuleWire, 'at' | 'do' | 'upTo'>): string {
  const act = t(`settings.supervisor.act.${rule.do}`);
  return rule.do === 'wait' && rule.upTo ? t('settings.supervisor.saidWait', { at: rule.at, act, minutes: rule.upTo }) : t('settings.supervisor.said', { at: rule.at, act });
}

/** A wider place in the person's words: this machine, or a workspace or repository by its name. */
export function placeSaid(t: TFunction, place: string): string {
  if (place === MACHINE) return t('settings.supervisor.machine');
  const [kind, name] = [place.slice(0, place.indexOf(':')), place.slice(place.indexOf(':') + 1)];
  return kind === 'workspace' ? t('settings.supervisor.workspace', { name }) : t('settings.supervisor.repository', { name });
}

/**
 * **A place's supervisor rules** (CONTEXTDRIVE1g, the context counters design §7.1): *when this count reaches n here, do this*, as rows in
 * the role editor's form, written whole at the revision read. Each rule set here says the wider place's rule it replaces; an inherited rule is
 * drawn under its place with *Set here instead…*. A molecule: it reads no bridge, and the press goes out; the driver judges and its sentence
 * stands under *Save*, the draft kept.
 */
export function SupervisorRulesFold({ place, placeName, read, roles, busy, refused, onSave, onCancel }: {
  place: string; placeName: string; read: SupervisorRulesRead; roles: readonly { value: string; label: string }[]; busy: boolean;
  /** The driver's sentence for the last press, kept under *Save* with the draft. */
  refused: string | null;
  onSave: (press: RoleChangePress, answered: Answered) => void;
  onCancel?: () => void;
}) {
  const { t } = useTranslation();
  const stored = setOf(read, place);
  const [draft, setDraft] = useState(() => draftOf(place, stored));
  const [said, setSaid] = useState<string | null>(null);
  const [keys, setKeys] = useState(() => draft.rows.length);
  const lineRoles = roles.map((role) => role.value);
  const inherited = inheritedFrom(read, draft, lineRoles);
  const unchanged = sameAsStored(draft, stored);
  const locked = busy;

  const patch = (key: number, next: (row: RuleDraft) => RuleDraft) => setDraft((was) => ({ ...was, rows: was.rows.map((row) => (row.key === key ? next(row) : row)) }));
  const countOf = (code: string) => read.counts.find((each) => each.count === code);
  const add = () => {
    const count = nextCount(read.counts, draft.rows);
    if (!count) return;
    setDraft((was) => ({ ...was, rows: [...was.rows, blankRule(count, keys)] }));
    setKeys(keys + 1);
  };
  const copyHere = (rule: SupervisorRuleWire) => {
    setDraft((was) => ({
      ...was,
      rows: [...was.rows, rowOf(rule, keys)],
    }));
    setKeys(keys + 1);
  };
  const submit = () => {
    if (locked || unchanged || !readable(draft)) return;
    setSaid(null);
    onSave(pressOf(draft), { done: () => setSaid(null), refused: setSaid });
  };
  // A rule on tries is the machine's last bound: it is never taken out there, only changed.
  const mayRemove = (row: RuleDraft) => !(place === MACHINE && row.count === 'tries' && row.roles.length === 0);
  const roleOptions = [{ value: ANY, label: t('settings.supervisor.everyRole') }, ...roles];
  const roleNames = (ids: readonly string[]) => list(ids.map((id) => roles.find((role) => role.value === id)?.label ?? id));
  const first = draft.rows.length === 0;

  return (
    <form
      aria-label={t('settings.supervisor.title', { place: placeName })}
      className="grid gap-2"
      onSubmit={(event) => {
        event.preventDefault();
        submit();
      }}
    >
      <p className="m-0 flex items-center gap-1 text-small font-medium text-ink">
        {t('settings.supervisor.title', { place: placeName })}
        <WhyGlyph why={t('settings.supervisor.why')} />
      </p>
      {!first && (
        <FormGrid columns={COLUMNS} floor="40rem">
          <FormRow head>
            <span className="text-meta text-ink-faint">{t('settings.supervisor.field.when')}</span>
            <span className="text-meta text-ink-faint">{t('settings.supervisor.field.reaches')}</span>
            <span className="text-meta text-ink-faint">{t('settings.supervisor.field.then')}</span>
            <span className="text-meta text-ink-faint">{t('settings.supervisor.field.for')}</span>
            <span />
          </FormRow>
          {draft.rows.map((row, index) => {
            const n = index + 1;
            const count = countOf(row.count);
            const replacing = replaced(read, draft, row, lineRoles);
            const replacedAt = [...new Set(replacing.map((each) => each.place))];
            return (
              <FormRow key={row.key}>
                <FormField label={t('settings.supervisor.field.when')} when="stacked">
                  <SelectField
                    value={row.count} disabled={locked} ariaLabel={t('settings.supervisor.countOf', { n })}
                    onChange={(code) => { const next = countOf(code); if (next) patch(row.key, (was) => recount(was, next)); }}
                    options={read.counts.map((each) => ({ value: each.count, label: t(`settings.supervisor.count.${each.count}`) }))}
                  />
                </FormField>
                <FormField label={t('settings.supervisor.field.reaches')} when="stacked">
                  <NumberField
                    value={row.at} label={t('settings.supervisor.atOf', { n })} width="w-14"
                    onChange={(at) => patch(row.key, (was) => ({ ...was, at }))}
                    step={(by) => patch(row.key, (was) => ({ ...was, at: String(Math.min(count?.most ?? 99, Math.max(count?.least ?? 1, (Number(was.at) || 0) + by))) }))}
                  />
                </FormField>
                <FormField label={t('settings.supervisor.field.then')} when="stacked">
                  <SelectField
                    value={row.do} disabled={locked} ariaLabel={t('settings.supervisor.doOf', { n })}
                    onChange={(action) => patch(row.key, (was) => reaction(was, action))}
                    options={(count?.actions ?? [row.do]).map((action) => ({ value: action, label: t(`settings.supervisor.act.${action}`) }))}
                  />
                </FormField>
                <FormField label={t('settings.supervisor.field.for')} when="stacked">
                  {row.roles.length > 1 ? (
                    // A rule set at the terminal for several roles: the fold edits one role, so it keeps these as read.
                    <span className="text-small text-ink" aria-label={t('settings.supervisor.forOf', { n })} data-rule-roles={row.roles.join(',')}>{roleNames(row.roles)}</span>
                  ) : (
                    <SelectField
                      value={row.roles[0] ?? ANY} disabled={locked} ariaLabel={t('settings.supervisor.forOf', { n })}
                      onChange={(role) => patch(row.key, (was) => ({ ...was, roles: role === ANY ? [] : [role] }))} options={roleOptions}
                    />
                  )}
                </FormField>
                <span className="flex items-center">
                  <Button
                    variant="ghost" disabled={locked || !mayRemove(row)} aria-label={t('settings.supervisor.removeOf', { n })}
                    onClick={() => setDraft((was) => ({ ...was, rows: was.rows.filter((each) => each.key !== row.key) }))}
                  >
                    <Icon name="x" size={12} />
                  </Button>
                </span>
                {(row.do === 'wait' || replacing.length > 0 || row.count === 'failures-on-choice' || row.count === 'tries') && (
                  <span className="col-span-full grid gap-1 text-small text-ink-faint">
                    {row.do === 'wait' && (
                      <span className="inline-flex flex-wrap items-center gap-1 text-ink-soft">
                        {t('settings.supervisor.upTo')}
                        <NumberField value={row.upTo} label={t('settings.supervisor.upToOf', { n })} width="w-16" onChange={(upTo) => patch(row.key, (was) => ({ ...was, upTo }))} />
                        {t('settings.supervisor.minutes')}
                      </span>
                    )}
                    {replacedAt.map((at) => (
                      <span key={at} data-replaces={at}>
                        {t('settings.supervisor.replaces', { place: placeSaid(t, at), rules: replacing.filter((each) => each.place === at).map((each) => ruleSaid(t, each.rule)).join('; ') })}
                      </span>
                    ))}
                    {(row.count === 'failures-on-choice' || row.count === 'tries') && <span>{t(`settings.supervisor.note.${row.count}`)}</span>}
                  </span>
                )}
              </FormRow>
            );
          })}
        </FormGrid>
      )}
      {first && <p className="m-0 text-small text-ink-soft">{t('settings.supervisor.none', { place: placeName })}</p>}
      {inherited.length > 0 && (
        <ul className="m-0 grid list-none gap-1 p-0 text-small text-ink-soft">
          {inherited.map(({ place: from, rule }, at) => (
            <li key={`${from}-${rule.count}-${rule.roles?.[0] ?? ANY}-${at}`} className="flex flex-wrap items-center gap-2" data-inherited={rule.count} data-from={from}>
              <span>
                {rule.roles && rule.roles.length > 0
                  ? t('settings.supervisor.inheritedFor', { count: t(`settings.supervisor.count.${rule.count}`), rule: ruleSaid(t, rule), roles: roleNames(rule.roles), place: placeSaid(t, from) })
                  : t('settings.supervisor.inherited', { count: t(`settings.supervisor.count.${rule.count}`), rule: ruleSaid(t, rule), place: placeSaid(t, from) })}
              </span>
              <Button variant="ghost" disabled={locked} onClick={() => copyHere(rule)}>{t('settings.supervisor.setHere')}</Button>
            </li>
          ))}
        </ul>
      )}
      <div className="flex flex-wrap items-center gap-2">
        <Button variant="ghost" disabled={locked || read.counts.length === 0} onClick={add}>
          <Icon name="plus" size={12} />
          {t('settings.supervisor.add')}
        </Button>
      </div>
      <p className="m-0 text-small text-ink-faint">{t('settings.supervisor.always')}</p>
      <p className="m-0 text-small text-ink-faint">{t('settings.supervisor.consequence')}</p>
      {(refused ?? said) && <Refused sentence={(refused ?? said)!} />}
      <div className="flex flex-wrap items-center gap-2">
        <Button type="submit" variant="primary" disabled={locked || unchanged || !readable(draft)}>{t('settings.supervisor.save')}</Button>
        <Button variant="ghost" disabled={locked || unchanged} onClick={() => { setDraft(draftOf(place, stored)); setSaid(null); onCancel?.(); }}>{t('settings.supervisor.cancel')}</Button>
      </div>
    </form>
  );
}
