import { type ReactNode, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { canBeAsked, type Registration } from './api';
import { AddProjectDrawer, ImportFolderDrawer, ManageProjectDrawer } from './ProjectManage';
import { DriverChoices } from './projects/DriverChoices';
import { ago, figure } from './format';
import { useRegistry, useRepositories } from './queries';
import { useScope } from './scope';
import { useDriver, useHarnesses, useLines, useSetDrivable, useSetHold, useSetTrees } from './shell';
import { doorOf } from './tools';
import {
  Button, Card, Chip, EmptyState, failure, Icon, Inline, type Notify, PageHeader, Prose, SkeletonRows, Tip,
  useErrorNotify, WhyGlyph,
} from './ui';

/**
 * The setup half of the platform (D38): who is in the family, what each repository owns and accepts —
 * as chips a person can scan — and, just as deliberately, who has not adopted yet. Membership is a
 * repository's own act (D32): Daoris never writes into a sibling, so nothing joins by being seen; the
 * join steps are proposed as text, never a button (D31's shape).
 */
export function ProjectsView({
  notify, addRequested = false, onAddOpened, importRequested = false, onImportOpened,
}: {
  notify: Notify;
  /**
   * The Workspace menu's *Add repository…* (D75), an EVENT like Quests' opening draft: consumed once,
   * and cleared by its holder through `onAddOpened`, or the drawer would reopen on every render.
   */
  addRequested?: boolean;
  onAddOpened?: () => void;
  /** And its *Import a folder…* (D77): the same kind of event, for the import drawer. */
  importRequested?: boolean;
  onImportOpened?: () => void;
}) {
  const { t } = useTranslation();
  const registry = useRegistry();
  const { workspace } = useScope();
  const repositories = useRepositories();
  const driver = useDriver();
  const roster = useHarnesses();
  const setDrivable = useSetDrivable();
  const setHold = useSetHold();
  const setTrees = useSetTrees();
  // Each repository's line as the driver resolves it (WSR2) — shell-only, and absent in a browser.
  const lines = useLines();
  const lineOf = (repository: string) => (Array.isArray(lines.data?.lines) ? lines.data.lines : [])
    .find((line) => line.repository === repository && line.branch);
  // The driver bridge included: a STATE that fails silently reads as a machine with no driver.
  useErrorNotify(registry.error ?? repositories.error ?? driver.error, notify);

  // The management surfaces exist where a shell does (D48 §7) — the same gate as every control, and
  // for the same reason: managing repositories means touching machine paths, and a browser has none.
  const attached = driver.data !== undefined;
  const [adding, setAdding] = useState(false);
  useEffect(() => {
    if (!addRequested || !attached) return;
    setAdding(true);
    onAddOpened?.();
  }, [addRequested, attached, onAddOpened]);
  const [importing, setImporting] = useState(false);
  useEffect(() => {
    if (!importRequested || !attached) return;
    setImporting(true);
    onImportOpened?.();
  }, [importRequested, attached, onImportOpened]);
  const [managing, setManaging] = useState<Registration | null>(null);

  // Starting and holding a CONVERSATION moved to the Work frame (design §3, D55): one home for the
  // stream, and starting a session belongs where its result appears. What stays here is the
  // registry's own business — who is in the family, and this machine's standing driver choices.
  const adopted = (registry.data ?? []).filter((r) => r.adopted);
  const outside = (registry.data ?? []).filter((r) => !r.adopted);
  const indexed = (name: string) => (repositories.data ?? []).find((r) => r.name === name);
  const named = (names: string[], repository: string) =>
    names.some((name) => name.toLowerCase() === repository.toLowerCase());
  const onDriverError = failure(notify);

  // Which door this machine's starts ride (INT3c): an unadopted repository is carried by the protocol
  // door only (D70), so on a direct one its row says a quest there will sit. Unknown says nothing.
  const door = doorOf(
    roster.data?.adapter,
    Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : []);

  /** This machine's driving row for one repository — the same row wherever it can be driven. */
  const driving = (repository: string, extra: { note?: string; action?: ReactNode; className?: string }) =>
    driver.data && (
      <DriverChoices
        drivable={named(driver.data.drivable, repository)}
        held={named(driver.data.holds, repository)}
        ownTree={named(driver.data.trees ?? [], repository)}
        onDrive={(next) => setDrivable.mutate({ repository, drivable: next }, { onError: onDriverError })}
        onHold={(next) => setHold.mutate({ repository, held: next }, { onError: onDriverError })}
        onTrees={(next) => setTrees.mutate({ repository, ownTree: next }, { onError: onDriverError })}
        {...extra}
      />
    );

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
      {importing && <ImportFolderDrawer onClose={() => setImporting(false)} notify={notify} />}
      {managing && (
        <ManageProjectDrawer project={managing} onClose={() => setManaging(null)} notify={notify} />
      )}

      {registry.isPending && <SkeletonRows rows={4} />}

      {/* 🔴 The first thing a new installation shows, and it was a header over a blank page: the
          fixture always holds a repository, so nothing had ever rendered this. It names the circle
          when the scope is one, because the machine may hold repositories in another. */}
      {registry.data?.length === 0 && (
        <EmptyState
          icon="projects"
          headline={workspace
            ? t('projects.empty.headlineIn', { workspace })
            : t('projects.empty.headline')}
          body={t(attached ? 'projects.empty.body' : 'projects.empty.bodyBrowser')}
          action={attached && (
            <Button onClick={() => setAdding(true)}>
              <Icon name="plus" size={14} />{t('projects.manage.add')}
            </Button>
          )}
        />
      )}

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
                        // `count` picks the plural form; the formatted string is what is shown.
                        count: counts.total,
                        total: figure(counts.total),
                        local: figure(counts.local),
                        canonical: figure(counts.canonical),
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
                    <Inline text={t('projects.undeclared')} />
                  </p>
                )}
              {/* The labels are a column and the chips wrap in their own, so a second line of chips
                  lines up under the first. As one flowing line, a wrapped chip fell back under its
                  label (POLISH4). */}
              {(project.owns.length > 0 || project.accepts.length > 0 || project.packs.length > 0
                || counts?.fed || project.workspace || lineOf(project.repository)) && (
                <dl className="m-0 mt-2 grid grid-cols-[max-content_1fr] items-baseline gap-x-3 gap-y-2">
                  {project.owns.length > 0 && (
                    <Row label={t('projects.owns')}>
                      {project.owns.map((item) => <Chip key={item}>{item}</Chip>)}
                    </Row>
                  )}
                  {project.accepts.length > 0 && (
                    <Row label={t('projects.accepts')}>
                      {project.accepts.map((item) => <Chip key={item} accent>{item}</Chip>)}
                    </Row>
                  )}
                  {project.packs.length > 0 && (
                    <Row label={t('projects.packs')}>
                      {project.packs.map((item) => <Chip key={item}>{item}</Chip>)}
                    </Row>
                  )}
                  {counts?.fed && (
                    /* Where this deployment's copy came from (D48 §6). Shown rather than implied: the
                       index is a claim about a commit, and a person who cannot see which commit has no
                       way to tell a current view from one a machine stopped feeding a month ago. */
                    <Row label={t('projects.fed')}>
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
                    </Row>
                  )}
                  {project.workspace && (
                    /* Which workspace this one shares with (D48). Shown rather than assumed: a machine
                       holding two workspaces would otherwise present them as one family, and the
                       person would have no way to tell from the list that it was two. */
                    <Row label={t('projects.workspace')}>
                      <Tip content={t('projects.workspaceTip')}><Chip>{project.workspace}</Chip></Tip>
                    </Row>
                  )}
                  {lineOf(project.repository) && (() => {
                    /* The branch this machine grows its work here from and lands it on (WSR2), and what
                       said so — Settings → Workspace is where it is set. */
                    const line = lineOf(project.repository)!;
                    return (
                      <Row label={t('projects.line')}>
                        <span className="text-small text-ink-soft">
                          <Inline text={t(`settings.lines.from.${line.source}`, {
                            branch: line.branch, workspace: line.workspace,
                          })}
                          />
                        </span>
                      </Row>
                    );
                  })()}
                </dl>
              )}
              {/* The person's standing choices for THIS machine's driver (D46 §6) — rendered only
                  where a shell answers; a browser has no driver to control, and shows nothing. */}
              {driving(project.repository, {
                className: 'mt-2.5 border-t border-line pt-2.5',
                action: (
                  <Button variant="ghost" onClick={() => setManaging(project)}>
                    {t('projects.manage.open')}
                  </Button>
                ),
              })}
            </Card>
          );
        })}
      </div>

      {outside.length > 0 && (
        <Card className="mt-3.5">
          <header className="flex items-baseline justify-between gap-4">
            <span className="flex items-center gap-1.5 text-body font-semibold">
              {t('projects.outside.title')}
              {/* The reasoning is one press away, not eight lines read before one row on every visit
                  (UX5 U36; the rule §4 settled for a settings page). */}
              <WhyGlyph why={t('projects.outside.body')} />
            </span>
            <span className="font-mono text-small tabular-nums text-ink-faint">{outside.length}</span>
          </header>
          <Prose className="mt-1.5">{t('projects.outside.lead')}</Prose>
          <ul className="m-0 mt-2 list-none p-0">
            {outside.map((project) => {
              const counts = indexed(project.repository);
              return (
                <li
                  key={project.repository}
                  aria-label={project.repository}
                  className="border-t border-line py-1.5 text-body first:border-t-0"
                >
                  <div className="flex items-baseline justify-between gap-4">
                    <span>{project.repository}</span>
                    <span className="font-mono text-meta text-ink-faint">
                      {counts && counts.total > 0
                        ? t('projects.outside.readable', {
                            count: counts.total, total: figure(counts.total),
                          })
                        : t('projects.nothingIndexed')}
                    </span>
                  </div>
                  {/* INT3c: one with a root here is drivable over the protocol door (D70), so it gets
                      the adopters' driving row — and none of adoption's own acts, like managing its
                      declaration, which writes into the repository. One with no root has nowhere to
                      start, and gets nothing. */}
                  {canBeAsked(project) && driving(project.repository, {
                    className: 'mt-1.5',
                    note: door === 'pipe' ? t('projects.outside.directDoor') : undefined,
                  })}
                </li>
              );
            })}
          </ul>
          {/* A sentence with its commands as code — it was all monospace, prose included (POLISH4). */}
          <p className="mt-3 max-w-prose rounded-control bg-accent-soft px-3 py-2.5 text-small">
            <Inline text={t('projects.outside.join')} />
          </p>
        </Card>
      )}
    </section>
  );
}

/** One row of a project card: its label in the card's label column, its content wrapping beside it. */
function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <>
      <dt className="text-meta text-ink-faint">{label}</dt>
      <dd className="m-0 flex min-w-0 flex-wrap items-baseline gap-1.5">{children}</dd>
    </>
  );
}
