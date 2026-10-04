import { useTranslation } from 'react-i18next';
import type { StarterDoor } from '../help/starters';
import { useRegistry } from '../queries';
import { useAcross, useDriver, useRuleAction, useRules, useSetReadAcross, useSetWriteAcross } from '../shell';
import { Button, failure, Inline, type Notify, useErrorNotify } from '../ui';
import { workspacesOf } from '../workspaces';
import { AcrossList } from './Across';
import { AgentRules } from './AgentRules';
import { DomainLoading } from './DomainLoading';

/**
 * What an agent Daoris starts may do: whether it reads and writes across repositories (READ1, D107), then
 * the rules a workspace or a repository adds in the machine's `permissions.json` (PERM1, D72), the file the
 * driver composes each spawn's rules from and `daoris agent rules` edits (D50). Daoris's defaults, the rules
 * for every session on this machine and the proposals are the agent's page's since UX6e (D150 §3.1); this
 * domain says so with a door, until a workspace's and a repository's pages take the rest (UX6f, UX6g).
 */
export function PermissionsDomain({ notify, onGo }: { notify: Notify; onGo?: (door: StarterDoor) => void }) {
  return (
    <>
      <AcrossSettings notify={notify} />
      <RulesSettings notify={notify} onGo={onGo} />
    </>
  );
}

/**
 * Reading and writing across (D107): the driver's own resolution for each repository here, and the screen's
 * half of `daoris driver across` (D50). Desktop-only, because the workspaces and the file are this machine's.
 * First on the page, because it answers the question a person brings here first: may an agent look next door.
 */
function AcrossSettings({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const answer = useAcross();
  const driver = useDriver();
  const setRead = useSetReadAcross();
  const setWrite = useSetWriteAcross();
  useErrorNotify(answer.error, notify);

  // Its place held until the first answer (FRAME1g): a first open drew a blank page under the domain's name.
  if (!answer.data && answer.isFetching) return <DomainLoading rows={3} />;
  // An older shell has never heard of the question: the card is absent rather than the page blank.
  const repositories = Array.isArray(answer.data?.repositories) ? answer.data.repositories : null;
  if (!repositories) return null;
  const failed = failure(notify);

  return (
    <AcrossList
      repositories={repositories}
      workspaceReads={driver.data?.workspaceReadAcross ?? []}
      busy={setRead.isPending || setWrite.isPending}
      onRead={(change) => setRead.mutate(change, {
        onSuccess: () => {
          const name = change.repository ?? change.workspace ?? '';
          notify(change.read === undefined
            ? t('settings.across.cleared', { name })
            : t(change.read ? 'settings.across.savedOn' : 'settings.across.savedOff', { name }));
        },
        onError: failed,
      })}
      onWrite={(change) => setWrite.mutate(change, {
        onSuccess: () => notify(t(change.allow ? 'settings.across.declared' : 'settings.across.withdrawn', {
          repository: change.repository, to: change.to,
        })),
        onError: failed,
      })}
    />
  );
}

/**
 * The rules a workspace or a repository adds (PERM1, D72). **The scopes a rule can reach are the registry's**: its
 * circles and its repositories, by the names the driver composes against. A refusal is the driver's sentence, verbatim.
 */
function RulesSettings({ notify, onGo }: { notify: Notify; onGo?: (door: StarterDoor) => void }) {
  const { t } = useTranslation();
  const answer = useRules();
  const act = useRuleAction();
  const registry = useRegistry('machine');
  useErrorNotify(answer.error, notify);

  // Its place held until the first answer, as the card above holds its own.
  if (!answer.data && answer.isFetching) return <DomainLoading />;
  // An older shell has never heard of the question: the card is absent rather than the page blank.
  const rules = answer.data && Array.isArray(answer.data.defaults) && Array.isArray(answer.data.scopes) ? answer.data : null;
  if (!rules) return null;

  const rows = registry.data ?? [];
  const circles = workspacesOf(rows);
  const repositories = rows.map((row) => row.repository).sort();
  // NAME1b: said inside a toast's sentence, so the fragments rather than the sections' sentence-case titles.
  const where = (scope: string, name: string | undefined) => scope === 'machine'
    ? t('settings.rules.proposals.everywhere')
    : t(scope === 'workspace' ? 'settings.rules.where.workspace' : 'settings.rules.where.repository', { name: name ?? '' });
  const failed = failure(notify);

  return (
    <AgentRules
      rules={rules}
      part="scoped"
      circles={circles}
      repositories={repositories}
      busy={act.isPending}
      // The rest of what agents may do is on the agent's page (UX6e): said once, with its door.
      elsewhere={(
        <div className="mb-3 flex flex-wrap items-center gap-x-3 gap-y-1.5 border-l-[3px] border-accent bg-page/60 px-3 py-2">
          <span className="min-w-0 flex-1 basis-72 text-small text-ink-soft"><Inline text={t('settings.rules.elsewhere')} /></span>
          {onGo && (
            <Button variant="ghost" onClick={() => onGo({ view: 'agents', agentPart: 'rules' })}>
              {t('settings.rules.elsewhereOpen')}
            </Button>
          )}
        </div>
      )}
      onAnswer={() => {}}
      onSwitchDefault={() => {}}
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
