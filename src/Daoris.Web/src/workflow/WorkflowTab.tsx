import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline, Prose, SkeletonRows, Tip } from '../ui';
import { PageSection } from '../work/ViewMain';
import type { CurrentWorkflow, WorkflowRepository } from './current';
import { partSaid } from './said';
import { type WorkflowDoors, WorkflowChart } from './WorkflowChart';

export type { WorkflowDoors } from './WorkflowChart';

/** What a page hands its Workflow tab: the answer, or that it is on its way or was refused, and the doors it may open. */
export type WorkflowTabProps = {
  /** The Current the shell answered; absent while it is read, or where it was refused. */
  current?: CurrentWorkflow | null;
  /** The answer is on its way. */
  reading?: boolean;
  /** The read was refused, in the driver's sentence that says why. */
  refusal?: string | null;
  doors?: WorkflowDoors;
};

/**
 * **A Workflow tab** (WORKFLOW1b; D157 point 12, the workflow design §6.1, §6.2): on a repository's page and a workspace's,
 * how work there moves, drawn read-only from the rules as they stand (Current, design §2.7). It heads with what it shows
 * and why: *Current workflow*, its version, whose rules it reads, the person's part composed from the steps, and its
 * terminal twin (D50). Then the chart; and on a workspace's page, its repositories, each saying whether it follows this or
 * sets rules of its own, with a door to its own.
 *
 * @remarks
 * **A molecule**: the answer arrives as a prop and every door goes out. While the answer is on its way it says so, and a
 * refusal is said in its place, in the driver's words (the platform language §4: a page never goes blank).
 *
 * **Named workflows, kinds and the editor are not here yet** (WORKFLOW1d, 1e, 1g): with none, every repository follows
 * Current, and that is what this draws (design §2.8).
 */
export function WorkflowTab({ page, name, current, reading = false, refusal = null, doors }: WorkflowTabProps & {
  page: 'repository' | 'workspace';
  /** The repository or the workspace whose page it is. */
  name: string;
}) {
  const { t } = useTranslation();
  const headId = useId();

  if (!current) {
    return (
      <div aria-busy={reading || undefined} className="grid gap-2">
        <Prose className="text-small"><Inline text={refusal ?? t('workflow.reading')} /></Prose>
        {!refusal && <SkeletonRows rows={4} />}
      </div>
    );
  }

  const read = page === 'repository'
    ? [
        t('workflow.head.repository', { repository: name }),
        ...(current.registered === false ? [t('workflow.head.unregistered', { workspace: current.workspace })] : []),
      ].join(t('workflow.set.join'))
    : t('workflow.head.workspace', { workspace: name });

  return (
    <div className="grid min-w-0 gap-5">
      <section aria-labelledby={headId} className="grid min-w-0 gap-1.5">
        <div className="flex min-w-0 flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
          <h2 id={headId} className="m-0 text-title font-semibold text-ink">{t('workflow.head.title')}</h2>
          <Tip content={t('workflow.versionTip')}>
            <span className="font-mono text-meta text-ink-faint">{t('workflow.version', { version: current.version })}</span>
          </Tip>
        </div>
        <Prose className="text-small"><Inline text={read} /></Prose>
        <Prose className="text-ink"><Inline text={partSaid(t, current)} /></Prose>
        <p className="m-0 text-meta text-ink-faint">
          <Inline text={page === 'repository'
            ? t('workflow.twin.repository', { repository: name })
            : t('workflow.twin.workspace', { workspace: name })}
          />
        </p>
      </section>

      <WorkflowChart workflow={current} page={page} name={name} doors={doors} />

      {/* A host may leave a null out of its answer, so none answered is the registry not read. */}
      {page === 'workspace' && <Repositories repositories={current.repositories ?? null} onOpen={doors?.repository} />}
    </div>
  );
}

/**
 * A workspace's repositories against its Current: each follows it, or sets a rule of its own and so follows its own, which
 * a rule it sets replaces whole (D145 §1, D154 §1.3, D155 §2.3), with a door to that repository's Workflow tab.
 */
function Repositories({ repositories, onOpen }: {
  /** Null where the registry was not read. */
  repositories: WorkflowRepository[] | null;
  onOpen?: (repository: string) => void;
}) {
  const { t } = useTranslation();
  const title = t('workflow.repositories.title');
  return (
    <PageSection title={title}>
      {repositories === null
        ? <Prose className="text-small">{t('workflow.repositories.unread')}</Prose>
        : repositories.length === 0
          ? <Prose className="text-small">{t('workflow.repositories.none')}</Prose>
          : (
            <>
              <Prose className="mb-2 text-small">{t('workflow.repositories.body')}</Prose>
              {/* The names are a column and the words beside them another (platform-ux §4, *labels are a column*): each row
                  takes the list's two columns as its own. */}
              <ul aria-label={title} className="m-0 grid list-none grid-cols-[minmax(0,max-content)_minmax(0,1fr)] gap-x-3 gap-y-1 p-0">
                {repositories.map(({ repository, own }) => (
                  <li key={repository} className="col-span-2 grid grid-cols-subgrid items-center">
                    {/* One that sets its own rules is a door to its own workflow, named by its name as a workspace's
                        Details names each repository's door. */}
                    {own && onOpen
                      ? (
                        <Button
                          variant="ghost"
                          className="min-w-0 justify-self-start border border-line px-2.5 py-0.5 text-small text-ink [overflow-wrap:anywhere]"
                          aria-label={t('workflow.door.repositoryNamed', { repository })}
                          onClick={() => onOpen(repository)}
                        >
                          {repository}
                        </Button>
                      )
                      // The same box without its line, so a name that is no door stands where a door's name does.
                      : (
                        <span className="inline-flex min-h-[1.9rem] min-w-0 items-center border border-transparent px-2.5 py-0.5 text-small text-ink [overflow-wrap:anywhere]">
                          {repository}
                        </span>
                      )}
                    <span className="justify-self-start rounded-full border border-line-strong px-2 py-px text-meta text-ink-soft">
                      {t(own ? 'workflow.repositories.own' : 'workflow.repositories.follows')}
                    </span>
                  </li>
                ))}
              </ul>
            </>
          )}
    </PageSection>
  );
}
