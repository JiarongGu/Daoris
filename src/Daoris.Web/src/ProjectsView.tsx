import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Registration } from './api';
import { AddProjectDrawer, ManageProjectDrawer } from './ProjectManage';
import { ago, sentence } from './format';
import { useRegistry, useRepositories } from './queries';
import { useDriver, useSetDrivable, useSetHold, useSetTrees } from './shell';
import {
  Button, Card, CheckField, Chip, type Notify, PageHeader, Prose, SkeletonRows, Tip, useErrorNotify,
} from './ui';

/**
 * The setup half of the platform (D38): who is in the family, what each repository owns and accepts —
 * as chips a person can scan — and, just as deliberately, who cannot be asked yet. Membership is a
 * repository's own act (D32): Daoris never writes into a sibling, so nothing joins by being seen; the
 * join steps are proposed as text, never a button (D31's shape).
 */
export function ProjectsView({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const registry = useRegistry();
  const repositories = useRepositories();
  const driver = useDriver();
  const setDrivable = useSetDrivable();
  const setHold = useSetHold();
  const setTrees = useSetTrees();
  // The driver bridge included: a STATE that fails silently reads as a machine with no driver.
  useErrorNotify(registry.error ?? repositories.error ?? driver.error, notify);

  // The management surfaces exist where a shell does (D48 §7) — the same gate as every control, and
  // for the same reason: managing repositories means touching machine paths, and a browser has none.
  const attached = driver.data !== undefined;
  const [adding, setAdding] = useState(false);
  const [managing, setManaging] = useState<Registration | null>(null);

  // Starting and holding a CONVERSATION moved to the Work frame (design §3, D55): one home for the
  // stream, and starting a session belongs where its result appears. What stays here is the
  // registry's own business — who is in the family, and this machine's standing driver choices.
  const adopted = (registry.data ?? []).filter((r) => r.adopted);
  const outside = (registry.data ?? []).filter((r) => !r.adopted);
  const indexed = (name: string) => (repositories.data ?? []).find((r) => r.name === name);
  const named = (names: string[], repository: string) =>
    names.some((name) => name.toLowerCase() === repository.toLowerCase());
  const onDriverError = (e: unknown) => notify(sentence(e), 'error');

  return (
    <section>
      <PageHeader
        title={t('projects.title')}
        description={t('projects.description')}
        action={attached
          ? <Button variant="primary" onClick={() => setAdding(true)}>{t('projects.manage.add')}</Button>
          : undefined}
      />

      {adding && <AddProjectDrawer onClose={() => setAdding(false)} notify={notify} />}
      {managing && (
        <ManageProjectDrawer project={managing} onClose={() => setManaging(null)} notify={notify} />
      )}

      {registry.isPending && <SkeletonRows rows={4} />}

      <div className="grid items-start gap-3.5 lg:grid-cols-2">
        {adopted.map((project) => {
          const counts = indexed(project.repository);
          return (
            <Card key={project.repository}>
              <header className="flex items-baseline justify-between gap-4">
                <span className="inline-flex items-center gap-2 text-body font-semibold">
                  <Tip content={t('projects.adoptedDot')}>
                    <span className="inline-block size-2 shrink-0 rounded-full bg-accent" />
                  </Tip>
                  {project.repository}
                </span>
                <span className="whitespace-nowrap font-mono text-small tabular-nums text-ink-faint">
                  {/* One sentence for "the index holds nothing of this", whether the repository is
                      absent from the index or present with a count of zero — the deployed family
                      had both, and read "0 entries · 0 local · 0 canonical" beside "nothing indexed
                      yet" beside "—" for the same fact. */}
                  {counts && counts.total > 0
                    ? t('projects.entries', {
                        total: counts.total.toLocaleString(),
                        local: counts.local.toLocaleString(),
                        canonical: counts.canonical.toLocaleString(),
                      })
                    : t('projects.nothingIndexed')}
                </span>
              </header>
              {project.summary
                ? <p className="mt-1.5 text-body text-ink-soft">{project.summary}</p>
                : (
                  /* Addressable regardless — adoption gates addressing, declaration does not (D34) —
                     but an asker deserves to know they would be guessing. */
                  <p className="mt-2 border-l-[3px] border-warn bg-raised px-3.5 py-2 text-body text-ink-soft">
                    {t('projects.undeclared')}
                  </p>
                )}
              {project.owns.length > 0 && (
                <p className="mt-2 flex flex-wrap items-baseline gap-1.5">
                  <span className="min-w-12 text-meta text-ink-faint">{t('projects.owns')}</span>
                  {project.owns.map((item) => <Chip key={item}>{item}</Chip>)}
                </p>
              )}
              {project.accepts.length > 0 && (
                <p className="mt-2 flex flex-wrap items-baseline gap-1.5">
                  <span className="min-w-12 text-meta text-ink-faint">{t('projects.accepts')}</span>
                  {project.accepts.map((item) => <Chip key={item} accent>{item}</Chip>)}
                </p>
              )}
              {project.packs.length > 0 && (
                <p className="mt-2 flex flex-wrap items-baseline gap-1.5">
                  <span className="min-w-12 text-meta text-ink-faint">{t('projects.packs')}</span>
                  {project.packs.map((item) => <Chip key={item}>{item}</Chip>)}
                </p>
              )}
              {counts?.fed && (
                /* Where this deployment's copy came from (D48 §6). Shown rather than implied: the
                   index is a claim about a commit, and a person who cannot see which commit has no
                   way to tell a current view from one a machine stopped feeding a month ago. */
                <p className="mt-2 flex flex-wrap items-baseline gap-1.5">
                  <span className="min-w-12 text-meta text-ink-faint">{t('projects.fed')}</span>
                  <Tip content={t('projects.fedTip', {
                    commit: counts.fed.commit,
                    branch: counts.fed.branch,
                    origin: counts.fed.origin ?? t('projects.fedUnknownOrigin'),
                  })}
                  >
                    <span className="font-mono text-small text-ink-soft">
                      {counts.fed.shortCommit} · {ago(counts.fed.committedAt)}
                    </span>
                  </Tip>
                </p>
              )}
              {project.workspace && (
                /* Which circle this one shares with (D48). Shown rather than assumed: a machine
                   holding two workspaces would otherwise present them as one family, and the
                   person would have no way to tell from the list that it was two. */
                <p className="mt-2 flex flex-wrap items-baseline gap-1.5">
                  <span className="min-w-12 text-meta text-ink-faint">{t('projects.workspace')}</span>
                  <Tip content={t('projects.workspaceTip')}><Chip>{project.workspace}</Chip></Tip>
                </p>
              )}
              {driver.data && (
                /* The person's standing choices for THIS machine's driver (D46 §6) — rendered only
                   where a shell answers; a browser has no driver to control, and shows nothing. */
                <p className="mt-2.5 flex flex-wrap items-center gap-4 border-t border-line pt-2.5">
                  <span className="min-w-12 text-meta text-ink-faint">{t('projects.driver.label')}</span>
                  <CheckField
                    checked={named(driver.data.drivable, project.repository)}
                    onChange={(next) => setDrivable.mutate(
                      { repository: project.repository, drivable: next }, { onError: onDriverError })}
                    label={t('projects.driver.drive')}
                  />
                  {named(driver.data.drivable, project.repository) && (
                    <CheckField
                      checked={named(driver.data.holds, project.repository)}
                      onChange={(next) => setHold.mutate(
                        { repository: project.repository, held: next }, { onError: onDriverError })}
                      label={t('projects.driver.hold')}
                    />
                  )}
                  {/* Session trees (D51): this repository's sessions open their own worktree, so the
                      person's uncommitted work in the checkout stops holding the driver. */}
                  <CheckField
                    checked={named(driver.data.trees ?? [], project.repository)}
                    onChange={(next) => setTrees.mutate(
                      { repository: project.repository, ownTree: next }, { onError: onDriverError })}
                    label={t('projects.driver.trees')}
                  />
                  <Button variant="ghost" className="ml-auto" onClick={() => setManaging(project)}>
                    {t('projects.manage.open')}
                  </Button>
                </p>
              )}
            </Card>
          );
        })}
      </div>

      {outside.length > 0 && (
        <Card className="mt-3.5">
          <header className="flex items-baseline justify-between gap-4">
            <span className="text-body font-semibold">{t('projects.outside.title')}</span>
            <span className="font-mono text-small tabular-nums text-ink-faint">{outside.length}</span>
          </header>
          <Prose className="mt-1.5">{t('projects.outside.body')}</Prose>
          <ul className="m-0 mt-2 list-none p-0">
            {outside.map((project) => {
              const counts = indexed(project.repository);
              return (
                <li
                  key={project.repository}
                  className="flex items-baseline justify-between gap-4 border-t border-line py-1.5 text-body first:border-t-0"
                >
                  <span>{project.repository}</span>
                  <span className="font-mono text-meta text-ink-faint">
                    {counts && counts.total > 0
                      ? t('projects.outside.readable', { count: counts.total.toLocaleString() })
                      : t('projects.nothingIndexed')}
                  </span>
                </li>
              );
            })}
          </ul>
          <p className="mt-3 rounded-control bg-accent-soft px-3 py-2.5 font-mono text-small">
            {t('projects.outside.join')}
          </p>
        </Card>
      )}
    </section>
  );
}
