import { useTranslation } from 'react-i18next';
import { Button, Inline } from '../design/ui';

/** A proposal of Ask Daoris's the route takes: what it changes, and the command that does the same; a go has none. */
export type HelpProposal = {
  id: string;
  /** A digest of the plan the card shows: the Apply sends it back, and the host applies only a plan still that one. */
  revision?: string;
  kind: 'go' | 'workflow';
  describe: string;
  terminal: string;
  why: string;
  /** A workflow version (`workflow`): drawn as the design's card. */
  workflow?: HelpWorkflowShown | null;
};

/**
 * What a workflow card will show, as the driver's judge reads the version: the workflow, the version it is
 * made on and the one it becomes, the steps' diff lines, the person's part, each outward act, what is still needed, who it
 * applies to, and whether the scope also chooses it. Lines are the driver's words, as it writes them.
 */
export type HelpWorkflowShown = {
  id: string;
  name: string;
  /** The version the edit is made on; absent for a new workflow. */
  base?: number | null;
  /** The number the saved version takes. */
  version: number;
  scope: string;
  diff: string[];
  yourPart: string[];
  outward: string[];
  /** What is missing; while any is, the plan offers no Apply (a draft). */
  needs: string[];
  appliesTo: string;
  choose: boolean;
  /** The route it is chosen for, `default`, `landed` or `kind:<id>`; absent where none, or from an older driver. */
  route?: string | null;
  /** Whether Apply saves a version; false for a route with no version. Absent from an older driver, which always saves. */
  saves?: boolean;
  /** Whether the workflow is a verification pipeline, its lines the graph's nodes. */
  verifies?: boolean;
  /** What the change asks less of the person, in the driver's words; only their Apply makes it. */
  lowers?: string[];
  /**
   * The repositories a chosen graph integrates in with no gates, by name; the page words the line. Absent from
   * an older driver, which said it in the plan's text alone. What a repository cannot carry stays in `needs`, as the driver sent it.
   */
  noGates?: string[];
  /**
   * On a change to the draft open in the editor (the `draft` door), the fingerprint of the draft it is against; null on every
   * other card, which saves or chooses, and absent from an older driver. A draft card saves nothing: its `version` is 0.
   */
  draft?: string | null;
  /** A draft card's changes against its draft, in the driver's order, each with the changes it needs taken with it. */
  changes?: WorkflowGraphChange[];
  /** What a save of a draft card's graph would refuse, each at its step or way, for the person to put right. */
  problems?: { code: string; node?: string | null; wire?: string | null; words: string }[];
  /** A draft card's candidate graph, whole, which the editor takes changes from and nothing saves. */
  proposed?: Record<string, unknown> | null;
};

/**
 * One change a draft card makes to its draft (the driver's `WorkflowGraphDiff`): a step, a way or a field of the graph, added,
 * removed or changed. `id` is `step:`, `way:` or `graph:`, then `+`, `-` or `~`, then the step's or way's id or the field; `said` is the
 * driver's English.
 */
export type WorkflowGraphChange = {
  id: string;
  what: 'step' | 'way' | 'graph';
  change: 'added' | 'removed' | 'changed';
  step?: string | null;
  /** The way's id: the proposal's, or the draft's for a way removed. */
  way?: string | null;
  /** A changed way's id in the draft, where the proposal moved it. */
  was?: string | null;
  kind?: string | null;
  fields: string[];
  needs: string[];
  said: string;
};

/** What a workflow proposal changes, in the design's order: the page's headings, each line the driver's English as it came. */
function WorkflowChange({ workflow }: { workflow: HelpWorkflowShown }) {
  const { t } = useTranslation();
  const section = (heading: string, lines: string[], key: string) => (
    <section aria-label={heading} className="mt-2">
      <h4 className="m-0 text-meta font-semibold text-ink">{heading}</h4>
      <ul className="m-0 mt-0.5 flex list-none flex-col gap-0.5 p-0">
        {lines.map((each, at) => <li key={`${key}-${at}`} className="m-0 break-words text-meta text-ink-soft"><Inline text={each} /></li>)}
      </ul>
    </section>
  );
  const kind = workflow.route?.startsWith('kind:') ? workflow.route.slice('kind:'.length) : null;
  const lowers = workflow.lowers ?? [];
  const noGates = workflow.noGates ?? [];
  return (
    <>
      <p className="m-0 mt-1 text-small text-ink">
        {workflow.saves === false
          ? t('help.proposal.workflow.versionKept', { version: workflow.version, name: workflow.name })
          : workflow.base != null
            ? t('help.proposal.workflow.versionEdit', { version: workflow.version, name: workflow.name, base: workflow.base })
            : t('help.proposal.workflow.versionNew', { version: workflow.version, name: workflow.name })}
      </p>
      {workflow.verifies && <p className="m-0 mt-1 text-meta text-ink-soft">{t('help.proposal.workflow.verification')}</p>}
      {lowers.length > 0 && section(t('help.proposal.workflow.lowers'), lowers, 'lowers')}
      {lowers.length > 0 && <p className="m-0 mt-1 text-meta text-ink-faint">{t('help.proposal.workflow.lowersNote')}</p>}
      {workflow.yourPart.length > 0 && section(t('help.proposal.workflow.yourPart'), workflow.yourPart, 'part')}
      {workflow.diff.length > 0 && section(t(workflow.verifies ? 'help.proposal.workflow.nodes' : 'help.proposal.workflow.steps'), workflow.diff, 'diff')}
      {workflow.outward.length > 0
        ? section(t('help.proposal.workflow.outward'), workflow.outward, 'outward')
        : section(t('help.proposal.workflow.outward'), [t('help.proposal.workflow.nothingLeaves')], 'outward')}
      {noGates.length > 0 && section(
        t('help.proposal.workflow.noGates'), noGates.map((repository) => t('help.proposal.workflow.noGatesLine', { repository })), 'no-gates',
      )}
      {workflow.needs.length > 0 && section(t('help.proposal.workflow.needs'), workflow.needs, 'need')}
      {workflow.needs.length > 0 && <p className="m-0 mt-1 text-meta text-ink-faint">{t('help.proposal.workflow.needsNote')}</p>}
      {section(t('help.proposal.workflow.appliesTo'), [workflow.appliesTo], 'applies')}
      {workflow.choose && (
        <p className="m-0 mt-1 text-meta text-ink-soft">
          {workflow.route === 'landed'
            ? t('help.proposal.workflow.chooseLanded', { scope: workflow.scope })
            : kind
              ? t('help.proposal.workflow.chooseKind', { scope: workflow.scope, kind })
              : t('help.proposal.workflow.choose', { scope: workflow.scope })}
        </p>
      )}
    </>
  );
}

/**
 * A change to the draft open in the editor: the place whose draft it is, its changes in the driver's lines as sent,
 * what a save of it would refuse (in the catalogue's words where the page has the code), and that nothing is saved or chosen: its one press
 * opens the editor, where the person takes its changes, and only their Save there saves.
 */
function DraftChange({ workflow }: { workflow: HelpWorkflowShown }) {
  const { t, i18n } = useTranslation();
  const problems = (workflow.problems ?? []).map((problem) =>
    (i18n.exists(`workflow.edit.refusal.${problem.code}`) ? t(`workflow.edit.refusal.${problem.code}`) : problem.words));
  const section = (heading: string, lines: string[], key: string, tone: string) => (
    <section aria-label={heading} className="mt-2">
      <h4 className="m-0 text-meta font-semibold text-ink">{heading}</h4>
      <ul className="m-0 mt-0.5 flex list-none flex-col gap-0.5 p-0">
        {lines.map((each, at) => <li key={`${key}-${at}`} className={`m-0 break-words text-meta ${tone}`}><Inline text={each} /></li>)}
      </ul>
    </section>
  );
  return (
    <>
      <p className="m-0 mt-1 text-small text-ink">{t('help.proposal.workflow.draftOf', { scope: workflow.scope })}</p>
      {section(t('help.proposal.workflow.draftChanges'), workflow.diff, 'change', 'text-ink-soft')}
      {problems.length > 0 && section(t('help.proposal.workflow.draftProblems'), problems, 'problem', 'text-ink-danger')}
      <p className="m-0 mt-1 text-meta text-ink-soft">{t('help.proposal.workflow.draftNote')}</p>
    </>
  );
}

/**
 * One change Ask Daoris proposes: what it changes, the terminal command that does the same, and why, with **apply** and
 * **not now**. The description is the driver's sentence, rendered verbatim; only the chrome translates.
 *
 * @remarks
 * A go carries no command: its press is *go there*, and *dismiss* only takes the card away, told to nobody. A workflow version
 * reads *Your part* first, then the steps' diff, the standing acts, what is needed first and who it applies to; a draft offers
 * only *Not now*, and a change to the open draft's one press, *Open in the editor*, saves and settles nothing.
 */
export function ProposalCard({ proposal, pending = false, onApply, onDismiss }: {
  proposal: HelpProposal;
  pending?: boolean;
  onApply: (id: string) => void;
  onDismiss: (id: string) => void;
}) {
  const { t } = useTranslation();
  const going = proposal.kind === 'go';
  const workflow = proposal.kind === 'workflow' ? proposal.workflow ?? null : null;
  const draft = workflow !== null && workflow.needs.length > 0;
  const composing = workflow !== null && typeof workflow.draft === 'string';

  return (
    <li className="rounded-control border border-accent/50 bg-raised px-3 py-2.5">
      <p className="m-0 text-small font-semibold text-accent">
        {t(going ? 'help.proposal.titleGo'
          : composing ? 'help.proposal.titleDraft' : workflow ? 'help.proposal.titleWorkflow' : 'help.proposal.title')}
      </p>
      {workflow
        ? composing ? <DraftChange workflow={workflow} /> : <WorkflowChange workflow={workflow} />
        : <p className="m-0 mt-1 text-small text-ink"><Inline text={proposal.describe} /></p>}
      {going && <p className="m-0 mt-1 text-meta text-ink-faint">{t('help.proposal.goNote')}</p>}
      {proposal.terminal && (
        <p className="m-0 mt-1 text-meta text-ink-faint"><Inline text={t('help.proposal.command', { command: proposal.terminal })} /></p>
      )}
      {/* The agent's reason quotes names as code, as the description does, so it renders as it does. */}
      {proposal.why && <p className="m-0 mt-1 text-small text-ink-soft"><Inline text={t('help.proposal.why', { why: proposal.why })} /></p>}
      <div className="mt-2 flex flex-wrap gap-2">
        {!draft && (
          <Button variant="primary" disabled={pending} onClick={() => onApply(proposal.id)}>
            {t(going ? 'help.proposal.go'
              : composing ? 'help.proposal.workflow.openEditor' : 'help.proposal.apply')}
          </Button>
        )}
        <Button variant="ghost" disabled={pending} onClick={() => onDismiss(proposal.id)}>
          {t(going ? 'help.proposal.dismissGo' : 'help.proposal.dismiss')}
        </Button>
      </div>
    </li>
  );
}
