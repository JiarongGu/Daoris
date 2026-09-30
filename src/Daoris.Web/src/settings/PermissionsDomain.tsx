import { useTranslation } from 'react-i18next';
import { useRegistry } from '../queries';
import { useRuleAction, useRuleProposal, useRules } from '../shell';
import { failure, type Notify, useErrorNotify } from '../ui';
import { workspacesOf } from '../workspaces';
import { AgentRules } from './AgentRules';
import { proposalChange } from './proposals';

/**
 * What an agent Daoris starts may do (PERM1, D72) — the machine's `permissions.json`, the file the
 * driver composes each spawn's rules from and `daoris agent rules` edits (D50).
 *
 * **The scopes a rule can reach are the registry's**: its circles and its repositories, by the names
 * the driver composes against. A refusal is the driver's sentence, verbatim.
 */
export function PermissionsDomain({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const answer = useRules();
  const act = useRuleAction();
  const settle = useRuleProposal();
  const registry = useRegistry('machine');
  useErrorNotify(answer.error, notify);

  // An older shell has never heard of the question: the card is absent rather than the page blank.
  const rules = answer.data && Array.isArray(answer.data.defaults) && Array.isArray(answer.data.scopes) ? answer.data : null;
  if (!rules) return null;

  const rows = registry.data ?? [];
  const circles = workspacesOf(rows);
  const repositories = rows.map((row) => row.repository).sort();
  const where = (scope: string, name: string | undefined) => scope === 'machine'
    ? t('settings.rules.scopeMachine')
    : t(scope === 'workspace' ? 'settings.rules.scopeWorkspace' : 'settings.rules.scopeRepository', { name: name ?? '' });
  const failed = failure(notify);

  return (
    <AgentRules
      rules={rules}
      circles={circles}
      repositories={repositories}
      busy={act.isPending || settle.isPending}
      onAnswer={(id, accept) => {
        const proposal = rules.proposals?.find((one) => one.id === id);
        settle.mutate({ id, accept }, {
          onSuccess: () => notify(t(accept ? 'settings.rules.proposals.accepted' : 'settings.rules.proposals.declined', {
            change: proposal ? proposalChange(proposal) : `#${id}`,
          })),
          onError: failed,
        });
      }}
      onSwitchDefault={(id, on) => act.mutate({ action: 'default', id, on }, {
        onSuccess: () => notify(t('settings.rules.switched', { id, state: t(on ? 'settings.rules.on' : 'settings.rules.off') })),
        onError: failed,
      })}
      onRemove={({ scope, name, rule }) => act.mutate({ action: 'remove', rule, scope, name }, {
        onSuccess: () => notify(t('settings.rules.removed', { rule, where: where(scope, name) })),
        onError: failed,
      })}
      onAdd={({ list, rule, scope, name }, added) => act.mutate({ action: 'add', list, rule, scope, name }, {
        onSuccess: () => {
          notify(t('settings.rules.added', { rule, list: t(`settings.rules.list.${list}`), where: where(scope, name) }));
          added();
        },
        onError: failed,
      })}
    />
  );
}
