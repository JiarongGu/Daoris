import { useTranslation } from 'react-i18next';
import { ago } from '../locales/format';
import type { PlaceAsk } from '../agents/setup/placePlan';
import { Button, Inline } from '../design/ui';
import { SessionHelperFacts } from './proposal/SessionHelperFacts';
import { sessionHelperDoor } from './proposal/sessionHelperDoor';
import { type HelpRulesShown, RulesChange } from './proposal/RulesChange';
import { type HelpSupervisorShown, SupervisorChange } from './proposal/SupervisorChange';
import type { StarterDoor } from './starters';

/**
 * What a plugin proposal's plugin runs, each command as its manifest writes it, `${plugin}`
 * included: the bridge leaves a null out, so a plugin that speaks on no point arrives with no `command`,
 * and a switch with no `copied`.
 */
export type HelpPluginShown = {
  id: string;
  name: string;
  version: string;
  command?: string[] | null;
  points: string[];
  harnesses: { name: string; command: string[] }[];
  servers: { name: string; command: string[] }[];
  copied?: boolean;
  problem?: string | null;
  /** What one of the install's own plugins needs, in its README's words. */
  needs?: string[];
  /** What an update changes, each side as the manifests write it. */
  changes?: { what: string; was: string; now: string }[];
  /** Whether Apply replaces an installed plugin's folder from where it came from (an update). */
  replaced?: boolean;
};

/**
 * A proposal of Ask Daoris's the route takes: what it changes, and the command that does the same.
 * A kind for each door: an agent's update or pin, an account's model and effort, a
 * delete of a record made by mistake, and a go to a screen, which has no command since it changes nothing.
 * A plugin that has landed, added from its folder, or one switched on or off, with what it runs.
 * A branch a landing made, handed to a landing plugin, which pushes it. Daoris's
 * browser's settings, a card as a setting's is, and bringing repositories up to date, which is looked at first.
 * The person's yes to a done's departure, with each departure shown before the press.
 */
export type HelpProposal = {
  id: string;
  /** A digest of the plan the card shows: the Apply sends it back, and the host applies only a plan still that one. */
  revision?: string;
  /** Archiving or unarchiving sessions (`session`) is the generic card, with its command. */
  /** A change of the role catalogue (`role`) is the generic card: its sentence, its `daoris role` command, Apply. */
  kind: 'setting' | 'ask' | 'agent' | 'account' | 'delete' | 'go' | 'plugin' | 'browser' | 'sync' | 'session' | 'pause' | 'clear' | 'workflow' | 'setup' | 'role';
  describe: string;
  terminal: string;
  why: string;
  plugin?: HelpPluginShown | null;
  /** The person's approval of what a plugin's server offers Ask Daoris, or its withdrawal (`plugin`'s `approve`). */
  grant?: HelpGrantShown | null;
  /** A place's supervisor rules, before and after (`role`); the card words them from the catalogue. */
  rules?: HelpRulesShown | null;
  /** A supervisor set (`role`): its kinds, acts, holder and window, before and after; the card words them from the catalogue. */
  supervisor?: HelpSupervisorShown | null;
  /** Bringing up to date: whether the person has looked, and every row the look listed. */
  sync?: HelpSyncShown | null;
  /** A workflow version (`workflow`): drawn as the design's card. */
  workflow?: HelpWorkflowShown | null;
  /** A place's quick set-up (`setup`'s `place` door): the ask and the plan's changes; Apply opens *Set up…* on the ask. */
  place?: HelpPlaceShown | null;
};

/**
 * What a place's set-up card shows: the ask, the workspace it is of, and each change the plan makes once with the repositories
 * it reaches. The page *Set up…* opens plans again; the digest only moves the card's revision when the plan moves.
 */
export type HelpPlaceShown = {
  workspace: string;
  digest: string;
  asked: PlaceAsk;
  changes: { key: string; part: string; said: string; before: string; after: string; repositories: string[] }[];
};

function PlaceChanges({ place }: { place: HelpPlaceShown }) {
  const { t } = useTranslation();
  return place.changes.length === 0
    ? <p className="m-0 mt-2 text-meta text-ink-soft">{t('help.proposal.place.nothing')}</p>
    : (
      <ul aria-label={t('help.proposal.place.changes')} className="m-0 mt-2 flex list-none flex-col gap-1 p-0">
        {place.changes.map((change) => (
          <li key={`${change.key}:${change.said}:${change.after}`} className="m-0 text-meta text-ink-soft">
            {/* The plan's words, as the driver says them: content, not chrome. */}
            <span className="text-ink">{change.said}</span>{': '}{change.before} → {change.after}
            {' '}<span className="text-ink-faint">({change.repositories.join(', ')})</span>
          </li>
        ))}
      </ul>
    );
}

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

/**
 * What a bring-up-to-date card shows: whether the person has looked — the press that fetches —
 * and each row the look listed, in the terminal's words, and whether the press moves it. Also what the rows do
 * not say, kept by the look: each repository whose line is not fetched, and those left apart. An older
 * host answers neither.
 */
export type HelpSyncShown = {
  looked: boolean;
  rows: { key: string; step: 'line' | 'replay' | 'delete'; moves: boolean; says: string }[];
  /** Each repository whose line the look did not fetch: git's reason, when it last heard from origin (never, as none), and how origin is reached. */
  notFetched?: { repository: string; fetch: string; lastFetch?: string | null; reach?: string | null; host?: string | null }[];
  /** The repositories with a checkout here that the look left apart, holding no branch of Daoris's. */
  apart?: string[];
};

/**
 * What an approval of a plugin's server for Ask Daoris names, as the driver's judge reads the
 * manifest: the plugin, the server, each tool it reads with and each it writes outside Daoris with, and the writes the approval
 * accepts; for a withdrawal (`withdraw`), those it had accepted. The bridge leaves a false out.
 */
export type HelpGrantShown = {
  plugin: string;
  name: string;
  server: string;
  reads: string[];
  writes: string[];
  accepted: string[];
  withdraw?: boolean;
};

/**
 * Every tool an approval hands Ask Daoris, or a withdrawal takes back: its reads, the writes accepted and those
 * not, each named as the plugin declares it, so the person sees exactly what their press widens or narrows.
 */
function GrantTools({ grant }: { grant: HelpGrantShown }) {
  const { t } = useTranslation();
  const tools = (names: string[]) => names.map((name) => `\`${name}\``).join(', ');
  const asked = grant.writes.filter((tool) => !grant.accepted.includes(tool));
  const said = (text: string, key: string) => (
    <li key={key} className="m-0 text-meta text-ink-soft"><Inline text={text} /></li>
  );
  return (
    <ul aria-label={t(grant.withdraw ? 'help.proposal.grant.loses' : 'help.proposal.grant.shows')} className="m-0 mt-1 flex list-none flex-col gap-0.5 p-0">
      {said(grant.name && grant.name !== grant.plugin
        ? t('help.proposal.grant.serverNamed', { server: grant.server, plugin: grant.plugin, name: grant.name })
        : t('help.proposal.grant.server', { server: grant.server, plugin: grant.plugin }), 'server')}
      {said(grant.reads.length > 0 ? t('help.proposal.grant.reads', { tools: tools(grant.reads) }) : t('help.proposal.grant.readsNone'), 'reads')}
      {grant.accepted.length > 0 && said(t('help.proposal.grant.writesAccepted', { tools: tools(grant.accepted) }), 'accepted')}
      {!grant.withdraw && asked.length > 0 && said(t('help.proposal.grant.writesAsked', { tools: tools(asked) }), 'asked')}
    </ul>
  );
}

/** A command as a terminal takes it: a word holding a space quoted, as the driver spells it. */
const line = (command: string[]) => command.map((word) => (word.includes(' ') ? `"${word}"` : word)).join(' ');

/**
 * What a plugin runs, before Apply: its id, the process it starts and the points it speaks on, the
 * harnesses it declares and the servers it hands every session. Chrome translates; every name and command
 * is the manifest's, as it writes it.
 */
function PluginRuns({ plugin }: { plugin: HelpPluginShown }) {
  const { t } = useTranslation();
  const said = (text: string, key: string) => (
    <li key={key} className="m-0 text-meta text-ink-soft"><Inline text={text} /></li>
  );
  return (
    <ul aria-label={t('help.proposal.plugin.shows')} className="m-0 mt-1 flex list-none flex-col gap-0.5 p-0">
      {said(t('help.proposal.plugin.id', { id: plugin.id }), 'id')}
      {said(plugin.command && plugin.command.length > 0
        ? t('help.proposal.plugin.runs', { command: line(plugin.command) })
        : t('help.proposal.plugin.runsNothing'), 'runs')}
      {plugin.points.length > 0
        && said(t('help.proposal.plugin.points', { points: plugin.points.map((point) => `\`${point}\``).join(', ') }), 'points')}
      {plugin.harnesses.map((harness) =>
        said(t('help.proposal.plugin.harness', { name: harness.name, command: line(harness.command) }), `harness-${harness.name}`))}
      {plugin.servers.map((server) =>
        said(t('help.proposal.plugin.server', { name: server.name, command: line(server.command) }), `server-${server.name}`))}
      {/* Each side as the manifests write it; a side that is empty is none. */}
      {(plugin.changes ?? []).map((change) =>
        said(t('help.proposal.plugin.change', {
          what: t(`plugin.update.what.${change.what}`, { defaultValue: change.what }),
          was: change.was ? `\`${change.was}\`` : t('plugin.update.none'),
          now: change.now ? `\`${change.now}\`` : t('plugin.update.none'),
        }), `change-${change.what}`))}
      {/* The README's own requirement lines, which the person sets up themselves. */}
      {(plugin.needs ?? []).map((need) => said(t('help.proposal.plugin.needs', { need }), `need-${need}`))}
    </ul>
  );
}

/**
 * What a workflow proposal changes, in the design's order: the version it makes, *Your part* first, the
 * steps' diff, the acts that become standing, what is needed first, and who it applies to. Headings are the page's words;
 * every line is the driver's English sentence, shown as it came.
 */
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
  // A route with no version saves nothing, a verification pipeline's lines are its graph's nodes, and what the
  // change asks less of the person leads, since only their Apply makes it.
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
 * What a sync card's look did not fetch, said once, after the rows, as the screen's note says it: how
 * many of the lines it looked at, grouped by git's reason, when each last heard from origin — which its row is judged
 * against — and the git identity an origin over SSH or HTTPS needs. Git's words are content.
 */
function SyncNotFetched({ failed, lines, onGo }: {
  failed: NonNullable<HelpSyncShown['notFetched']>;
  lines: number;
  onGo?: (door: StarterDoor) => void;
}) {
  const { t } = useTranslation();
  // The screen door: each host a fetch over SSH or HTTPS did not reach, once, opening Add a git identity with the host filled.
  const hosts = [...new Set(failed.filter((line) => line.reach === 'ssh' || line.reach === 'https').map((line) => line.host).filter((host): host is string => !!host))];
  const reasons = [...new Set(failed.map((line) => line.fetch))];
  const when = (line: (typeof failed)[number]) => (line.lastFetch
    ? t('settings.sync.notFetched.when', { repository: line.repository, when: ago(line.lastFetch) })
    : t('settings.sync.notFetched.never', { repository: line.repository }));
  return (
    <div role="note" aria-label={t('settings.sync.notFetched.label')} className="m-0 mt-1 text-meta text-ink-soft">
      <p className="m-0">{t('settings.sync.notFetched.head', { count: failed.length, total: Math.max(lines, failed.length) })}</p>
      <ul className="m-0 list-none p-0">
        {reasons.map((reason) => (
          <li key={reason} className="m-0 mt-0.5">
            <span className="break-words"><Inline text={reason} /></span>
            <span className="block">{failed.filter((line) => line.fetch === reason).map(when).join(' · ')}</span>
          </li>
        ))}
      </ul>
      {failed.some((line) => line.reach === 'ssh') && (
        <p className="m-0 mt-0.5"><Inline text={t('settings.sync.notFetched.ssh')} /></p>
      )}
      {failed.some((line) => line.reach === 'https' || line.reach === 'http') && (
        <p className="m-0 mt-0.5">{t('settings.sync.notFetched.https')}</p>
      )}
      {onGo && hosts.length > 0 && (
        <div className="mt-1 flex flex-wrap gap-1.5">
          {hosts.map((host) => (
            <Button key={host} onClick={() => onGo({ view: 'settings', section: 'git', gitHost: host })}>
              {t('help.proposal.syncAddIdentity', { host })}
            </Button>
          ))}
        </div>
      )}
    </div>
  );
}

/**
 * The repositories a sync card's look left apart, collapsed as the screen's list is: they hold no branch
 * of Daoris's, so nothing of theirs is fetched or moves, and a card cannot tick one, so it says how one is included.
 */
function SyncApart({ apart }: { apart: string[] }) {
  const { t } = useTranslation();
  return (
    <details role="group" aria-label={t('settings.sync.apart.label')} className="m-0 mt-1 text-meta text-ink-soft">
      <summary className="cursor-pointer">{t('settings.sync.apart.summary', { count: apart.length })}</summary>
      <p className="m-0 mt-0.5">
        <Inline text={t('help.proposal.syncApart', { names: apart.map((name) => `\`${name}\``).join(', ') })} />
      </p>
    </details>
  );
}

/**
 * One change Ask Daoris proposes: what it changes, the terminal command that does the same,
 * and why — with **apply** and **not now**, since every change is the person's press.
 *
 * @remarks
 * The description is the driver's sentence and renders verbatim, as every driver sentence does; only the
 * chrome around it translates. A molecule: the proposal arrives judged, a press goes out.
 *
 * **Each kind says what its press does**: a delete is titled as one, says it cannot be undone as
 * the drawer's own confirmation does, and its press is *delete*; a go is a suggestion, not a change, so
 * it carries no command and its press is *go there*, and its second press *dismiss* only takes the card away, told to nobody;
 * a go to a quest says, in the driver's sentence, whether it is taken. A plugin shows what will run before Apply,
 * and an add says its folder is copied in and that nothing starts at the press.
 *
 * **Bringing up to date has the screen's two presses**: looking fetches as the person, so the
 * card's first press is *look for updates*, and only once the driver has listed what the press would do does it
 * show those rows, each as the terminal says it, with Apply, which does only the rows that move. Beside them it says
 * what they do not: the lines the look could not fetch, and the repositories it left apart.
 *
 * **A workflow version reads in the design's order**: *Your part* first, then the steps' diff, the acts that
 * become standing, what is needed first and who it applies to. A draft (needs present) offers only *Not now*. A change to the draft open in
 * the editor lists its changes and what to put right, and its press, *Open in the editor*, saves and settles nothing.
 *
 * **A set-up is a place's**: its card lists the plan's changes and Apply opens the page's *Set up…* on the ask.
 */
export function ProposalCard({ proposal, pending = false, onApply, onDismiss, onGo }: {
  proposal: HelpProposal;
  pending?: boolean;
  onApply: (id: string) => void;
  onDismiss: (id: string) => void;
  /** Where a sync card's screen door leads: the window opens the door, as it does a published item's. */
  onGo?: (door: StarterDoor) => void;
}) {
  const { t } = useTranslation();
  const deleting = proposal.kind === 'delete';
  const going = proposal.kind === 'go';
  const plugin = proposal.kind === 'plugin' ? proposal.plugin ?? null : null;
  const grant = proposal.kind === 'plugin' ? proposal.grant ?? null : null;
  const syncing = proposal.kind === 'sync' ? proposal.sync ?? { looked: false, rows: [] } : null;
  const looking = syncing !== null && !syncing.looked;
  const workflow = proposal.kind === 'workflow' ? proposal.workflow ?? null : null;
  const place = proposal.kind === 'setup' ? proposal.place ?? null : null;
  const helper = proposal.kind === 'session' ? sessionHelperDoor(proposal.terminal) : null;
  // A draft: what is still needed leaves no Apply, only Not now.
  const draft = workflow !== null && workflow.needs.length > 0;
  // A change to the draft open in the editor: its press opens the editor, and settles nothing.
  const composing = workflow !== null && typeof workflow.draft === 'string';

  return (
    <li className={deleting
      ? 'rounded-control border border-st-declined/50 bg-raised px-3 py-2.5'
      : 'rounded-control border border-accent/50 bg-raised px-3 py-2.5'}
    >
      <p className={deleting ? 'm-0 text-small font-semibold text-ink-danger' : 'm-0 text-small font-semibold text-accent'}>
        {t(deleting ? 'help.proposal.titleDelete'
          : going ? 'help.proposal.titleGo'
            : grant ? (grant.withdraw ? 'help.proposal.titleWithdraw' : 'help.proposal.titleGrant')
            : proposal.kind === 'plugin' ? 'help.proposal.titlePlugin'
              : syncing ? 'help.proposal.titleSync'
                : composing ? 'help.proposal.titleDraft' : workflow ? 'help.proposal.titleWorkflow'
                  : proposal.kind === 'setup' ? 'help.proposal.titleSetup'
                    : helper ? (helper.door === 'pass' ? 'help.proposal.titlePass' : 'help.proposal.titleHelper') : 'help.proposal.title')}
      </p>
      {workflow
        ? composing ? <DraftChange workflow={workflow} /> : <WorkflowChange workflow={workflow} />
        : proposal.rules ? <RulesChange rules={proposal.rules} />
        : proposal.supervisor ? <SupervisorChange supervisor={proposal.supervisor} />
        : <p className="m-0 mt-1 text-small text-ink"><Inline text={proposal.describe} /></p>}
      {plugin && <PluginRuns plugin={plugin} />}
      {grant && <GrantTools grant={grant} />}
      {place && <PlaceChanges place={place} />}
      {helper && <SessionHelperFacts door={helper} />}
      {looking && <p className="m-0 mt-1 text-meta text-ink-soft">{t('help.proposal.syncLook')}</p>}
      {syncing?.looked && (
        <>
          <ul aria-label={t('help.proposal.syncRows')} className="m-0 mt-1 flex list-none flex-col gap-0.5 p-0">
            {syncing.rows.map((row) => (
              <li key={`${row.step}:${row.key}`} className="m-0 text-meta text-ink-soft">
                <span className={row.moves ? 'font-semibold text-accent' : 'text-ink-faint'}>
                  {t(row.moves ? 'settings.sync.moves' : 'settings.sync.stays')}
                </span>{' '}
                {/* The driver's sentence, the terminal's, carried as it said it. */}
                <Inline text={row.says} />
              </li>
            ))}
          </ul>
          {/* What the rows do not say, on the card itself, as the look's message says it too. */}
          {(syncing.notFetched ?? []).length > 0 && (
            <SyncNotFetched failed={syncing.notFetched!} lines={syncing.rows.filter((row) => row.step === 'line').length} onGo={onGo} />
          )}
          {(syncing.apart ?? []).length > 0 && <SyncApart apart={syncing.apart!} />}
          <p className="m-0 mt-1 text-meta text-ink-soft">{t('help.proposal.syncNote')}</p>
        </>
      )}
      {plugin?.copied && <p className="m-0 mt-1 text-meta text-ink-faint">{t('help.proposal.plugin.copied')}</p>}
      {plugin?.replaced && <p className="m-0 mt-1 text-meta text-ink-faint">{t('help.proposal.plugin.replaced')}</p>}
      {/* The catalogue's sentence, carried through as a value: one copy of it, the driver's. */}
      {plugin?.problem && (
        <p className="m-0 mt-1 text-meta text-ink-danger"><Inline text={t('help.proposal.plugin.problem', { problem: plugin.problem })} /></p>
      )}
      {deleting && <p className="m-0 mt-1 text-meta text-ink-danger">{t('help.proposal.deleteNote')}</p>}
      {going && <p className="m-0 mt-1 text-meta text-ink-faint">{t('help.proposal.goNote')}</p>}
      {place && <p className="m-0 mt-1 text-meta text-ink-soft">{t('help.proposal.place.note')}</p>}
      {proposal.terminal && (
        <p className="m-0 mt-1 text-meta text-ink-faint"><Inline text={t('help.proposal.command', { command: proposal.terminal })} /></p>
      )}
      {/* The agent's reason quotes names as code, as the description does, so it renders as it does. */}
      {proposal.why && <p className="m-0 mt-1 text-small text-ink-soft"><Inline text={t('help.proposal.why', { why: proposal.why })} /></p>}
      <div className="mt-2 flex flex-wrap gap-2">
        {!draft && (
          <Button variant={deleting ? 'danger' : 'primary'} disabled={pending} onClick={() => onApply(proposal.id)}>
            {t(deleting ? 'help.proposal.delete'
              : going ? 'help.proposal.go'
                : place ? 'help.proposal.place.open'
                : composing ? 'help.proposal.workflow.openEditor'
                : looking ? 'help.proposal.look' : 'help.proposal.apply')}
          </Button>
        )}
        <Button variant="ghost" disabled={pending} onClick={() => onDismiss(proposal.id)}>
          {t(going ? 'help.proposal.dismissGo' : 'help.proposal.dismiss')}
        </Button>
      </div>
    </li>
  );
}
