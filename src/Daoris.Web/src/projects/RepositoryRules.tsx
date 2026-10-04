import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { LIST_TONE, RULE_LISTS, type RuleListName } from '../settings/AgentRules';
import { Button, Icon, Pill, SelectField } from '../ui';

/** One scope's three lists of Claude Code's rules (PERM1, D72): what it allows, asks for and denies. */
export type RuleLists = { allow: string[]; ask: string[]; deny: string[] };

/**
 * **Claude Code's rules for one repository** (UX6f, D150 §3.1): the rules this machine hands every session there beside
 * its own and its workspace's, each with its remove, and *Add a rule* one press away. The same file `daoris agent rules …
 * --repository <name>` edits (D50), so a refusal is the driver's sentence, said by whoever holds the page.
 *
 * @remarks
 * **Only this repository's scope**: the machine's rules, Daoris's defaults and the workspace's are said where they are
 * set, and nothing here ranks one scope over another, since precedence is the harness's (PERM1). The words stay in the
 * box until the driver has taken them, so a rule it refused can be corrected rather than typed again (REV3).
 *
 * Props only, no hook from the query layer or the shell (components §2).
 */
export function RepositoryRules({ rules, busy = false, onAdd, onRemove }: {
  rules: RuleLists;
  busy?: boolean;
  /** Add a rule to a list; call `added` once the driver has taken it. Absent, nothing can be added. */
  onAdd?: (list: RuleListName, rule: string, added: () => void) => void;
  /** Remove one of its rules. Absent, nothing can be removed. */
  onRemove?: (rule: string) => void;
}) {
  const { t } = useTranslation();
  const [adding, setAdding] = useState(false);
  const [list, setList] = useState<RuleListName>('allow');
  const [rule, setRule] = useState('');
  const held = RULE_LISTS.flatMap((name) => rules[name].map((one) => ({ name, one })));

  return (
    <div className="grid gap-2">
      {held.length === 0
        ? <p className="m-0 text-small text-ink-soft">{t('projects.setup.rulesNone')}</p>
        : (
          <ul aria-label={t('projects.setup.rules')} className="m-0 list-none p-0">
            {held.map(({ name, one }) => (
              <li key={`${name}:${one}`} className="flex items-center gap-2 border-t border-line py-1 first:border-t-0 first:pt-0">
                <Pill tone={LIST_TONE[name]}>{t(`settings.rules.list.${name}`)}</Pill>
                <span className="min-w-0 flex-1 font-mono text-small wrap-anywhere">{one}</span>
                {onRemove && (
                  <Button variant="ghost" disabled={busy} aria-label={t('settings.rules.remove', { rule: one })} onClick={() => onRemove(one)}>
                    <Icon name="x" size={12} />
                  </Button>
                )}
              </li>
            ))}
          </ul>
        )}
      {onAdd && !adding && (
        <div>
          <Button variant="ghost" onClick={() => setAdding(true)}><Icon name="plus" size={13} />{t('settings.rules.addTitle')}</Button>
        </div>
      )}
      {onAdd && adding && (
        <form
          className="flex min-w-0 flex-wrap items-center gap-2"
          onSubmit={(event) => {
            event.preventDefault();
            if (rule.trim()) onAdd(list, rule.trim(), () => { setRule(''); setAdding(false); });
          }}
        >
          <SelectField
            value={list}
            onChange={(value) => setList(value as RuleListName)}
            ariaLabel={t('settings.rules.listLabel')}
            options={RULE_LISTS.map((name) => ({ value: name, label: t(`settings.rules.list.${name}`) }))}
          />
          <input
            value={rule}
            autoFocus
            aria-label={t('settings.rules.ruleLabel')}
            placeholder="Bash(npm run test:*)"
            onChange={(event) => setRule(event.target.value)}
            className="w-56 min-w-0 max-w-full rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint"
          />
          <Button type="submit" disabled={busy || !rule.trim()}>{t('settings.rules.add')}</Button>
          <Button variant="ghost" onClick={() => { setAdding(false); setRule(''); }}>{t('common.cancel')}</Button>
        </form>
      )}
    </div>
  );
}
