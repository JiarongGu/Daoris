import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline, Prose, SkeletonRows, Tip } from '../design/ui';
import { PageSection } from '../work/frame/ViewMain';
import type { CurrentWorkflow, WorkflowRepository } from './current';
import type { Answered } from '../work/InlineConfirm';
import { type WorkflowEditorBridge, WorkflowEditor } from './edit/WorkflowEditor';
import { partSaid } from './said';
import { type WorkflowDoors, WorkflowChart } from './WorkflowChart';
import { type VerifyAsk, WorkflowStartNode } from './WorkflowStartNode';

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
  /**
   * The person's *Accept the gates* on the start node's landed route (FABRIC-PIPELINE1a): the declaration at the line's tip, accepted
   * by its blob. None, and the start offers no press.
   */
  onAcceptGates?: (answered: Answered) => void;
  /** An acceptance is on its way. */
  accepting?: boolean;
  /** The person's *Verify…* of a branch or a commit under the accepted gates (FABRIC-PIPELINE1d); none, and the start offers no press. */
  onVerify?: (ask: VerifyAsk, answered: Answered) => void;
  /** The step editor's bridge (WORKFLOWEDIT1b); none, and the tab says the terminal's way to start a workflow of one's own. */
  editor?: WorkflowEditorBridge;
};

/**
 * **A Workflow's head** (WORKFLOW1b): what it shows and why, *Current workflow*, its version, whose rules it reads, the person's part
 * composed from the steps, and its terminal twin (D50). The Workflow tab draws it first, and so does the Agents place's Workflow
 * section (AGENTSPLACE1h).
 */
export function WorkflowHead({ page, name, current }: { page: 'repository' | 'workspace'; name: string; current: CurrentWorkflow }) {
  const { t } = useTranslation();
  const headId = useId();
  const read = page === 'repository'
    ? [
        t('workflow.head.repository', { repository: name }),
        ...(current.registered === false ? [t('workflow.head.unregistered', { workspace: current.workspace })] : []),
      ].join(t('workflow.set.join'))
    : t('workflow.head.workspace', { workspace: name });

  return (
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
  );
}

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
 * **The start node comes first** (FABRIC-PIPELINE1a, `WorkflowStartNode`), where the answer carries one: each route, each kind
 * among them, wired to the workflow it names, named or Current. The chart below it draws Current; *Compose steps…* opens the step
 * editor (WORKFLOWEDIT1b) below it, whose *Use here* chooses a saved version for this page's place.
 */
export function WorkflowTab({
  page, name, current, reading = false, refusal = null, doors, onAcceptGates, accepting = false, onVerify, editor,
}: WorkflowTabProps & {
  page: 'repository' | 'workspace';
  /** The repository or the workspace whose page it is. */
  name: string;
}) {
  const { t } = useTranslation();

  if (!current) {
    return (
      <div aria-busy={reading || undefined} className="grid gap-2">
        <Prose className="text-small"><Inline text={refusal ?? t('workflow.reading')} /></Prose>
        {!refusal && <SkeletonRows rows={4} />}
      </div>
    );
  }

  return (
    <div className="grid min-w-0 gap-5">
      <WorkflowHead page={page} name={name} current={current} />

      {/* FABRIC-PIPELINE1a: the start node first, the routes into the workflows; a driver before it answers none. */}
      {current.start && (
        <WorkflowStartNode
          start={current.start}
          page={page}
          scope={{ repository: current.repository ?? (page === 'repository' ? name : null), workspace: current.workspace }}
          onAccept={onAcceptGates}
          accepting={accepting}
          onVerify={onVerify}
        />
      )}

      <WorkflowChart workflow={current} page={page} name={name} doors={doors} />

      <Compose editor={editor} place={page === 'repository' ? { repository: name } : { workspace: name }} />

      {/* A host may leave a null out of its answer, so none answered is the registry not read. */}
      {page === 'workspace' && <Repositories repositories={current.repositories ?? null} onOpen={doors?.repository} />}
    </div>
  );
}

/**
 * A workflow of one's own (WORKFLOWEDIT1b, in place of INTEGRATE1q's presets in words): *Compose steps…* opens the step editor, told so its
 * reads are asked only while it shows, and the terminal's twin stands beside it. With no editor handed, only the twin.
 */
function Compose({ editor, place }: { editor?: WorkflowEditorBridge; place: { repository: string } | { workspace: string } }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const show = (next: boolean) => { setOpen(next); editor?.onOpen?.(next); };
  return (
    <PageSection title={t('workflow.compose.title')}>
      {editor && open
        ? <WorkflowEditor {...editor} place={place} onClose={() => show(false)} />
        : (
          <div className="grid min-w-0 gap-2">
            <Prose className="text-small">{t('workflow.compose.body')}</Prose>
            {editor && (
              <Button variant="ghost" className="justify-self-start border border-line" onClick={() => show(true)}>{t('workflow.compose.open')}</Button>
            )}
            <p className="m-0 text-meta text-ink-faint"><Inline text={t('workflow.compose.twin')} /></p>
          </div>
        )}
    </PageSection>
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
