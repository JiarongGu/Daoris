import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from '../../locales/format';
import { useDriver, useGitIdentity, useSetGitAccess, useSetGitUse, useToolPick, useTools } from '../../bridge/shell';
import { Card, CardHeader, failure, Inline, type Notify, Prose, SettingList, useErrorNotify } from '../../design/ui';
import { GitAccessRows, resolveGitAccess } from '../repository/GitAccess';
import { DomainLoading } from '../DomainLoading';
import {
  type GitActs, GitDefaults, GitIdentities, GitOwn, type GitTestAnswer,
} from './Git';
import { GitAdd } from './GitAdd';
import { GitReads } from './GitReads';

/**
 * Settings → Git: the git identities Daoris's git acts as, a host's default by way, and the
 * hosts where the person's own sign-ins are used. The screen's half of `daoris git identity`, `git default` and `git use`,
 * over the same files. It holds the presses; the cards below it hold none.
 *
 * @remarks
 * **A widening is confirmed here, in the card that asks** (§8): an import, choosing the person's own sign-ins and turning read only
 * off each open an `InlineConfirm` in the warning tone before the press is sent, and the route carries no flag for it.
 */
export function GitDomain({ notify, host: openHost, onClosed }: { notify: Notify; host?: string; onClosed?: () => void }) {
  const { t } = useTranslation();
  const list = useTools();
  const identity = useGitIdentity();
  const use = useSetGitUse();
  const pick = useToolPick();
  const driver = useDriver();
  const setAccess = useSetGitAccess();
  const machineAccess = driver.data?.machineGitAccess;
  const [adding, setAdding] = useState(openHost !== undefined);
  useErrorNotify(list.error, notify);
  const failed = failure(notify);

  const page = list.data?.identities;
  const acts = useMemo<GitActs>(() => ({
    add: async (input) => {
      const answer = input.way === 'ssh'
        ? await identity.mutateAsync({
          action: 'add-ssh', host: input.host, label: input.label,
          ...(input.name ? { name: input.name, email: input.email } : {}),
          ...(input.importFile ? { import: input.importFile } : {}),
          ...(input.passphrase ? { passphrase: input.passphrase } : {}),
          ...(input.rsa ? { rsa: true } : {}),
        })
        : await identity.mutateAsync({
          action: 'add-https', host: input.host, label: input.label,
          user: input.user ?? '', token: input.token ?? '',
          ...(input.name ? { name: input.name, email: input.email } : {}),
        });
      return {
        identity: String(answer.identity), label: String(answer.label), host: String(answer.host), way: answer.way as 'ssh' | 'https',
        publicKey: (answer.publicKey as string | null | undefined) ?? null,
      };
    },
    test: async (each, options) => (await identity.mutateAsync({ action: 'test', identity: each.id, ...options })) as unknown as GitTestAnswer,
    rename: (each, label) => identity.mutateAsync({ action: 'rename', identity: each.id, label }),
    commitsAs: (each, who) => identity.mutateAsync(who
      ? { action: 'commits-as', identity: each.id, name: who.name, email: who.email }
      : { action: 'commits-as', identity: each.id, clear: true }),
    readOnly: (each, on) => identity.mutateAsync({ action: 'read-only', identity: each.id, on }),
    apiToken: (each, token) => identity.mutateAsync(token === null
      ? { action: 'token', identity: each.id, kind: 'api', clear: true }
      : { action: 'token', identity: each.id, kind: 'api', token }),
    remove: (each) => identity.mutateAsync({ action: 'remove', identity: each.id }),
    makeDefault: (each) => identity.mutateAsync({ action: 'default', host: each.host, identity: each.id }),
    own: (host, on) => use.mutateAsync({ scope: 'machine', host, ...(on ? { value: 'own' as const } : {}) }),
    copy: (text) => {
      void navigator.clipboard.writeText(text).then(() => notify(t('settings.git.copied')), () => notify(t('settings.git.copyFailed'), 'error'));
    },
  }), [identity, use, notify, t]);

  if (!page && (list.isFetching || list.data === null)) return <DomainLoading rows={4} />;
  if (!page) return null;

  const wayToday = page.identities.at(-1)?.way ?? 'ssh';
  const browse = () => pick.mutateAsync({ title: t('settings.git.pickTitle'), kind: 'key' }, { onError: failed }).then((answer) => {
    if (!answer.can) {
      notify(t('settings.git.noPicker'), 'error');
      return null;
    }
    return answer.file ?? null;
  }, (error) => { notify(sentence(error), 'error'); return null; });

  return (
    <>
      <Prose className="mb-1"><Inline text={t('settings.git.intro')} /></Prose>
      <Prose className="text-small"><Inline text={t('settings.git.terminal')} /></Prose>
      {page.problem && (
        <p className="mt-3 border-l-[3px] border-warn bg-raised px-3.5 py-2 text-body text-ink-soft">
          <Inline text={t('settings.git.fileProblem', { problem: page.problem })} />
        </p>
      )}
      {adding && (
        <GitAdd
          acts={acts}
          knownHosts={page.hosts}
          wayToday={wayToday}
          host={openHost}
          pick={browse}
          onClose={() => { setAdding(false); onClosed?.(); }}
        />
      )}
      <GitIdentities identities={page.identities} acts={acts} adding={adding} onAdd={() => setAdding(true)} />
      <GitDefaults identities={page.identities} acts={acts} onError={(said) => notify(said, 'error')} />
      <GitOwn hosts={page.own} acts={acts} knownHosts={page.hosts} />
      <GitReads reads={page.reads ?? []} />
      {machineAccess && (
        <Card id="settings-git-access" className="scroll-mt-3">
          <CardHeader title={t('settings.git.access.machine')} />
          <SettingList>
            <GitAccessRows
              owner="machine"
              classes={resolveGitAccess([{ classes: machineAccess, source: 'machine' }])}
              above={resolveGitAccess([])}
              busy={setAccess.isPending}
              onChange={(edit, answered) => setAccess.mutate({ machine: true, ...edit }, {
                onSuccess: () => answered?.done(),
                onError: (error) => (answered ? answered.refused(sentence(error)) : failed(error)),
              })}
            />
          </SettingList>
        </Card>
      )}
    </>
  );
}
