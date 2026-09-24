import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Card, CheckField, Icon, Pill, Prose, SectionTitle, SelectField, SettingRow } from '../ui';

export type RuleListName = 'allow' | 'ask' | 'deny';
export type RuleScopeName = 'machine' | 'workspace' | 'repository';

/** A rule set Daoris ships, with the reason it exists — switched on or off by id, removable by nothing else. */
/** A default Daoris ships. `hook`, when present, is the tools a hook default judges (PERM3): it adds no rule. */
export type RuleDefault = { id: string; list: RuleListName; rules: string[]; why: string; on: boolean; hook?: string };

/**
 * One scope's rules. 🔴 The machine's carries no `name` on the wire: the bridge leaves a null out,
 * so absent is how the page knows it.
 */
export type RuleScopeRow = { scope: RuleScopeName; name?: string; allow: string[]; ask: string[]; deny: string[] };

/** The driver's RULES answer (PERM1, D72): the file, the defaults, and every scope it holds. */
export type AgentRulesState = { path: string; problem?: string; defaults: RuleDefault[]; scopes: RuleScopeRow[] };

export type RuleAddition = { list: RuleListName; rule: string; scope: RuleScopeName; name: string | undefined };

const LISTS: readonly RuleListName[] = ['allow', 'ask', 'deny'];
const LIST_TONE = { allow: 'done', ask: 'open', deny: 'declined' } as const;
const MACHINE = 'machine';

/**
 * What an agent Daoris starts may do (PERM1, D72): Claude Code's own permission rules, in Daoris's
 * scopes — this machine, a circle, a repository — handed to the harness at spawn.
 *
 * @remarks
 * **The screen's half of `daoris agent rules`** (D50): every change is an edit to the one file under
 * the home that the terminal edits too, and a refusal is the driver's own sentence, raised by whoever
 * holds this card. **Precedence is the harness's, never this page's**: `deny` beats `ask` beats
 * `allow` across every scope, so nothing here ranks one scope over another — each is shown as what
 * it adds. **A default is Daoris's**: it is switched off by id and removed by nothing, so it has a
 * switch and no remove.
 *
 * A rarely-used form is one press away rather than open on every visit (platform-ux §4). Props only,
 * no hook from the query layer or the shell (components §2).
 */
export function AgentRules({ rules, circles, repositories, busy = false, onSwitchDefault, onRemove, onAdd }: {
  rules: AgentRulesState;
  /** The circles a rule can reach — the machine's own, from the registry. */
  circles: string[];
  /** The repositories a rule can reach, by the names the registry holds. */
  repositories: string[];
  busy?: boolean;
  onSwitchDefault: (id: string, on: boolean) => void;
  onRemove: (target: { scope: RuleScopeName; name: string | undefined; rule: string }) => void;
  onAdd: (addition: RuleAddition) => void;
}) {
  const { t } = useTranslation();
  const [adding, setAdding] = useState(false);
  const [list, setList] = useState<RuleListName>('allow');
  const [rule, setRule] = useState('');
  const [where, setWhere] = useState(MACHINE);

  const scopeTitle = (row: RuleScopeRow) => row.scope === 'machine'
    ? t('settings.rules.scopeMachine')
    : t(row.scope === 'workspace' ? 'settings.rules.scopeWorkspace' : 'settings.rules.scopeRepository', { name: row.name ?? '' });

  const held = rules.scopes.filter((row) => LISTS.some((name) => row[name].length > 0));

  // The scope choice as one value: `machine`, `workspace:<circle>` or `repository:<name>`.
  const places = [
    { value: MACHINE, label: t('settings.rules.scopeMachine') },
    ...circles.map((name) => ({ value: `workspace:${name}`, label: t('settings.rules.scopeWorkspace', { name }) })),
    ...repositories.map((name) => ({ value: `repository:${name}`, label: t('settings.rules.scopeRepository', { name }) })),
  ];

  const add = () => {
    const [scope, ...named] = where.split(':');
    onAdd({ list, rule: rule.trim(), scope: scope as RuleScopeName, name: named.length > 0 ? named.join(':') : undefined });
    setRule('');
  };

  return (
    <Card className="mt-3.5">
      <SectionTitle>{t('settings.rules.title')}</SectionTitle>
      <SettingRow
        label={t('settings.rules.title')}
        hint={t('settings.rules.hint')}
        why={t('settings.rules.why')}
        control={<span className="break-all font-mono text-small text-ink-faint">{rules.path}</span>}
      />

      {/* The driver's own sentence, verbatim: the defaults still hold when the file does not. */}
      {rules.problem && (
        <p className="max-w-prose border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft">
          {rules.problem}
        </p>
      )}

      <SectionTitle level={3}>{t('settings.rules.defaults')}</SectionTitle>
      <ul className="m-0 list-none p-0">
        {rules.defaults.map((shipped) => (
          <li key={shipped.id} aria-label={shipped.id}>
            <SettingRow
              label={(
                <span className="flex flex-wrap items-center gap-2">
                  <span className="font-mono">{shipped.id}</span>
                  <Pill tone={LIST_TONE[shipped.list]}>{t(`settings.rules.list.${shipped.list}`)}</Pill>
                </span>
              )}
              hint={(
                <span className="flex flex-col gap-0.5">
                  <span>{shipped.why}</span>
                  <span className="flex flex-wrap gap-x-2 font-mono text-meta">
                    {shipped.hook
                      ? <span>{t('settings.rules.hook', { tools: shipped.hook })}</span>
                      : shipped.rules.map((one) => <span key={one}>{one}</span>)}
                  </span>
                </span>
              )}
              control={(
                <CheckField
                  checked={shipped.on}
                  disabled={busy}
                  onChange={(on) => onSwitchDefault(shipped.id, on)}
                  label={t('settings.rules.defaultOn', { id: shipped.id })}
                  hideLabel
                />
              )}
            />
          </li>
        ))}
      </ul>

      {held.map((row) => (
        <div key={`${row.scope}:${row.name ?? ''}`} className="mt-3">
          <SectionTitle level={3}>{scopeTitle(row)}</SectionTitle>
          <ul aria-label={scopeTitle(row)} className="m-0 list-none p-0">
            {LISTS.flatMap((name) => row[name].map((one) => (
              <li key={`${name}:${one}`} className="flex items-center gap-2 border-t border-line py-1.5 first:border-t-0">
                <Pill tone={LIST_TONE[name]}>{t(`settings.rules.list.${name}`)}</Pill>
                <span className="min-w-0 flex-1 truncate font-mono text-body">{one}</span>
                <Button
                  variant="ghost"
                  disabled={busy}
                  aria-label={t('settings.rules.remove', { rule: one })}
                  onClick={() => onRemove({ scope: row.scope, name: row.name, rule: one })}
                >
                  <Icon name="x" size={13} />
                </Button>
              </li>
            )))}
          </ul>
        </div>
      ))}

      {!adding ? (
        <Button variant="ghost" className="mt-3" onClick={() => setAdding(true)}>
          <Icon name="plus" size={13} />
          {t('settings.rules.addTitle')}
        </Button>
      ) : (
        <div className="mt-3 border-t border-line pt-3">
          <SectionTitle level={3}>{t('settings.rules.addTitle')}</SectionTitle>
          {/* Sized to what each field holds: a list is one word, a rule is a line, a scope a name. */}
          <div className="mt-2 grid max-w-[48rem] gap-2 md:grid-cols-[7rem_minmax(0,1fr)_14rem]">
            <SelectField
              value={list}
              onChange={(value) => setList(value as RuleListName)}
              ariaLabel={t('settings.rules.listLabel')}
              options={LISTS.map((name) => ({ value: name, label: t(`settings.rules.list.${name}`) }))}
            />
            <input
              value={rule}
              aria-label={t('settings.rules.ruleLabel')}
              placeholder="Bash(npm run test:*)"
              onChange={(event) => setRule(event.target.value)}
              className="rounded-control border border-line-strong bg-raised px-2.5 py-1.5 font-mono text-body text-ink"
            />
            <SelectField value={where} onChange={setWhere} ariaLabel={t('settings.rules.scopeLabel')} options={places} />
          </div>
          <Prose className="mt-2 text-small">{t('settings.rules.addBody')}</Prose>
          <div className="mt-3 flex gap-2">
            <Button disabled={busy || !rule.trim()} onClick={add}>{t('settings.rules.add')}</Button>
            <Button variant="ghost" onClick={() => { setAdding(false); setRule(''); }}>{t('common.cancel')}</Button>
          </div>
        </div>
      )}
    </Card>
  );
}
