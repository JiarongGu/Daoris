import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AccessRows, accessSummary } from '../../settings/repository/Access';
import type { RuleListName } from '../../settings/repository/AgentRules';
import { COMMIT_DEFAULT, type CommitEdit, CommitField, type CommitRule, commitRowSays, commitSummary } from '../../settings/repository/Commits';
import { HELPER_DEFAULT, type HelperEdit, HelperField, type HelperRule, helperRowSays, helperSummary } from '../../settings/repository/Helpers';
import {
  HAND_OVER_DEFAULT, type HandOverEdit, HandOverField, type HandOverRule, handOverRowSays, handOverSummary,
} from '../../settings/repository/HandOver';
import { LanguageChoice, type LanguageOption } from '../../settings/repository/Languages';
import { LineField } from '../../settings/repository/Lines';
import { type OpinionEdit, OpinionField, type OpinionRule, opinionRowSays, opinionSummary } from '../../settings/repository/Opinions';
import { type ReviewEdit, ReviewField, type ReviewRule, reviewRowSays, reviewSummary } from '../../settings/repository/Reviews';
import { shellWord } from '../../lib/shellWord';
import { Button, FormField, FormGrid, FormRow, Icon, Inline, PathText, Prose, Segmented, SettingRow, Tip } from '../../design/ui';
import { RepositoryRules, type RuleLists } from './RepositoryRules';
import type { AccessSetup } from './RepositorySetup';
import { Inheritable, SetupSection } from './SetupParts';
import type { SupervisorLine } from './SupervisorRow';
import { SupervisorDoor } from './SupervisorDoor';
import type { WorkspaceSection } from '../tabs';
import { hostOf } from '../workspace/workspace';
import type { GitIdentityShown } from '../../settings/git/Git';
import { GitHosts, type GitHostActs, type GitHostShown } from '../../settings/git/GitRemotes';
import { GitAccessRows, gitAccessSummary } from '../../settings/repository/GitAccess';
import type { GitAccessSetup } from './RepositorySetup';

/**
 * A workspace's defaults on this machine (§4.3), each what the workspace sets, absent where it sets none, and the press
 * its terminal twin makes. A part the driver did not answer is null, and its row is absent rather than a control whose
 * save would be refused.
 */
export type WorkspaceDefaults = {
  /** Its line (WSR2): `daoris place line --workspace`. */
  line?: { set?: string; onSet: (branch: string | undefined) => void } | null;
  /** The language its sessions write in (LANG1c), by the driver's table: `daoris place language --workspace`. */
  language?: { set?: string; table: LanguageOption[]; onSet: (code: string | undefined) => void } | null;
  /** Whether agents outside its repositories read their checkouts (READ1, D107): `daoris place across --workspace`. */
  read?: { set?: boolean; onSet: (read: boolean | undefined) => void } | null;
  /** Where its repositories' work is reviewed before it lands (REVIEWENV1a): `daoris place review --workspace`. */
  review?: { set?: ReviewRule; onChange: (edit: ReviewEdit) => void } | null;
  /** Which other agent reads its repositories' work before it lands (XAGENT1a): `daoris place opinion --workspace`. */
  opinion?: { set?: OpinionRule; onChange: (edit: OpinionEdit) => void } | null;
  /** How its repositories' work is committed (DOORS1b, D164's amendment point 1): `daoris place commits --workspace`. */
  commits?: { set?: CommitRule; onChange: (edit: CommitEdit) => void } | null;
  /** What a helper its repositories' agents ask for does (DOORS1b, D163 point 3): `daoris place helpers --workspace`. */
  helpers?: { set?: HelperRule; onChange: (edit: HelperEdit) => void } | null;
  /** What an agent's ask to hand its session over does in its repositories (MIXAGENT1c, D163 point 3): `daoris settings handover --workspace`. */
  handOver?: { set?: HandOverRule; onChange: (edit: HandOverEdit) => void } | null;
};

/** Where it syncs (D48 §5), and Claude Code's rules for it (PERM1): its remote and its reach. */
export type WorkspaceRemote = {
  /** This machine's wiring for it, over `remotes.json`: the remote it is wired to, or none. */
  wiring?: {
    remote: { url: string; key: string } | null;
    /** The environment names this machine's remote whole, and the file is not read (D48 §5). */
    fromEnvironment: boolean;
    /** Wire it; `done` once the key has landed, so the form lets the key go. */
    onWire: (url: string, key: string, done: () => void) => void;
    onUnwire: () => void;
  } | null;
  rules?: {
    lists: RuleLists;
    onAdd: (list: RuleListName, rule: string, added: () => void) => void;
    onRemove: (rule: string) => void;
  } | null;
  /**
   * The machines its remote last said had not declared they know a bound quest (FABRIC-CLOSE1i2), as the last pass heard them, each
   * with *Retire*; absent where no pass heard the handshake, or where it is not wired.
   */
  machines?: {
    undeclared: string[];
    /** The machine whose retirement is on its way, its press held meanwhile. */
    retiring?: string | null;
    onRetire: (machine: string) => void;
  } | null;
};

export type WorkspaceSetupProps = {
  workspace: string;
  defaults: WorkspaceDefaults;
  /**
   * The access setup its repositories' sessions start under where they set none of their own (AUTONOMY1d, D184 point 1): each
   * pause as it resolves from the workspace, this machine or Daoris's default; absent on a shell older than it.
   */
  access?: Omit<AccessSetup, 'workspace'> | null;
  /**
   * The hosts its repositories' git remotes reach and the identity this workspace chooses for each (GITAUTH1e, the git-auth design §7.3);
   * absent on a shell older than it, and the section is not drawn.
   */
  /** Its git identities by host (GITAUTH1e) and its git access (GITAUTH1f, §7.3). */
  git?: { hosts: GitHostShown[]; identities: GitIdentityShown[]; acts: GitHostActs; access?: GitAccessSetup | null } | null;
  /**
   * The supervisor's appointment for every repository of the workspace (SUPERVISOR1e, D208 §5), a repository's own winning over it;
   * absent where the shell says no standing confirmation.
   */
  supervisor?: SupervisorLine | null;
  /** The door from the supervisor's line to Agents → Roles → Appointed roles, where it is appointed (COMPANYVIEW1e). */
  onOpenAppointed?: () => void;
  remote: WorkspaceRemote;
  busy?: boolean;
  /** A section a door asked to see open, beside those that open on their own. */
  open?: WorkspaceSection;
  /** The header's *Wire to a remote…*: the wiring form starts open. */
  wiring?: boolean;
  /**
   * Which rows are drawn, by the home they have on the workspace place (AGENTSPLACE1f, SETUPUX1v): its Sessions hold the access, the session
   * language, helpers, hand-over and reading rows, and *Reach* (the rules and the machines its remote counts undeclared); its Workflow the line's
   * defaults (line, landing, commits, review, opinion); the remote's wiring is Repositories' Details and the supervisor the place's Roles.
   * `rest` is what the old tab keeps until it goes: everything but the session rows. Absent, all.
   */
  rows?: 'all' | 'sessions' | 'defaults' | 'rest';
};

const JOIN = ' · ';

/** A body read in a tip, which draws no code: its marks go, and its words stay. */
const plain = (text: string) => text.replace(/`/g, '');

/**
 * **A workspace's Setup** (UX6g, D150 §4.3): what it gives each of its repositories that sets none of its own, the access their
 * sessions start under (AUTONOMY1d), and where it syncs with Claude Code's rules for it, in sections that fold to the line naming
 * their values. It is where Settings → Workspace's defaults and wiring and Settings → Permissions' workspace rows went (§3.1).
 *
 * @remarks
 * **What Daoris decides folds; what the person decides shows** (§1 rule 4): a value the workspace does not set reads as
 * Daoris's default, marked, and offers *Set for this workspace*; one it sets carries *Clear*, and opens its section. Each
 * row's hint is its terminal twin (D41 §4, D50), the same file either door edits, so a refusal is the driver's sentence;
 * the workspace in it is spelled for whichever shell it is pasted into (`shellWord`, ACCTQUOTE1).
 * **A key goes in and never comes out**: the remote shows the audit prefix the wiring answered, and the form lets the key
 * go once it has landed.
 *
 * A molecule: every state is reached by its props, and every press goes out.
 */
export function WorkspaceSetup({
  workspace, defaults, access: accessAnswered, git, supervisor: supervisorLine, onOpenAppointed, remote, busy = false, open, wiring: startWiring = false,
  rows = 'all',
}: WorkspaceSetupProps) {
  const { t } = useTranslation();
  const daoris = (value: string) => t('projects.setup.marked.daoris', { value });
  const mine = rows === 'all' || rows === 'sessions';
  const ofLine = rows !== 'sessions';
  const rest = rows === 'all' || rows === 'rest';
  const { line, review, opinion, commits } = ofLine ? defaults : ({} as WorkspaceDefaults);
  const { language, read, helpers, handOver } = mine ? defaults : ({} as WorkspaceDefaults);
  const access = mine ? accessAnswered : null;
  const supervisor = rest ? supervisorLine : null;
  const gitShown = mine ? git : null;

  const defaultsParts = [
    ...(line ? [line.set ? t('projects.setup.summary.line', { branch: line.set }) : daoris(t('projects.workspace.summary.noLine'))] : []),
    ...(commits ? [commits.set ? commitSummary(t, commits.set) : daoris(commitSummary(t, COMMIT_DEFAULT))] : []),
    ...(language
      ? [language.set
          ? language.table.find((row) => row.code === language.set)?.name ?? language.set
          : daoris(t('projects.setup.summary.noLanguage'))]
      : []),
    ...(helpers ? [helpers.set ? helperSummary(t, helpers.set) : daoris(helperSummary(t, HELPER_DEFAULT))] : []),
    ...(handOver ? [handOver.set ? handOverSummary(t, handOver.set) : daoris(handOverSummary(t, HAND_OVER_DEFAULT))] : []),
    ...(read
      ? [read.set === undefined
          ? daoris(t('projects.setup.summary.read'))
          : t(read.set ? 'projects.setup.summary.read' : 'projects.setup.summary.unread')]
      : []),
    ...(review ? [review.set ? reviewSummary(t, review.set) : daoris(reviewSummary(t, null))] : []),
    ...(opinion ? [opinion.set ? opinionSummary(t, opinion.set) : daoris(opinionSummary(t, null))] : []),
  ];

  const wired = remote.wiring?.remote ?? null;
  const rules = remote.rules?.lists;
  const ruleCount = rules ? rules.allow.length + rules.ask.length + rules.deny.length : 0;
  const rulesSays = rules ? [ruleCount > 0 ? t('projects.setup.summary.rules', { count: ruleCount }) : t('projects.setup.summary.noRules')] : [];
  const remoteParts = !rest ? [] : [
    ...(remote.wiring
      ? [wired ? t('projects.workspace.page.syncs', { host: hostOf(wired.url) }) : t('projects.workspace.page.local')]
      : []),
    ...rulesSays,
  ];
  // The place's Sessions hold the rules and the retirement of an undeclared machine under Reach; the wiring is Details' (SETUPUX1v).
  const undeclared = remote.machines?.undeclared.length ?? 0;
  const reachParts = rows !== 'sessions' ? [] : [
    ...rulesSays,
    ...(undeclared > 0 ? [t('projects.workspace.machines.summary', { count: undeclared })] : []),
  ];
  const rulesRow = remote.rules && (
    <SettingRow
      label={t('projects.setup.rules')}
      hint={t('projects.workspace.twin.rules', { workspace: shellWord(workspace, '<workspace>') })}
    >
      <RepositoryRules
        rules={remote.rules.lists}
        busy={busy}
        none={t('projects.workspace.rulesNone')}
        onAdd={remote.rules.onAdd}
        onRemove={remote.rules.onRemove}
      />
    </SettingRow>
  );

  // Sessions' access (AUTONOMY1d): the same rows as a repository's, one level up.
  const accessShown = access && access.pauses.length > 0 ? access : null;

  const opensItself: Record<WorkspaceSection, boolean> = {
    defaults: Boolean(line?.set || language?.set || read?.set !== undefined || review?.set || opinion?.set
      || commits?.set || helpers?.set || handOver?.set),
    access: Boolean(accessShown?.pauses.some((each) => each.source === 'workspace')),
    reach: ruleCount > 0 || undeclared > 0,
    // A host its repositories reach and no identity serves opens it (GITAUTH1e); so does git access set here (GITAUTH1f).
    git: Boolean(gitShown?.hosts.some((each) => !each.chosen && !gitShown.identities.some((identity) => identity.host === each.host && identity.way === each.way))
      || gitShown?.access?.classes.some((each) => each.source === 'workspace')),
    // An appointment the person made opens it, as a value they set does.
    supervisor: Boolean(supervisor?.appointment),
    remote: Boolean(wired) || ruleCount > 0 || startWiring,
  };

  const sections: { id: WorkspaceSection; summary: string; body: ReactNode }[] = [];
  if (defaultsParts.length > 0) {
    sections.push({
      id: 'defaults',
      summary: defaultsParts.join(JOIN),
      body: (
        <>
          {line && (
            <Inheritable
              setHere={t('projects.workspace.setHere')}
              label={t('projects.line')}
              twin={t('projects.workspace.twin.line', { workspace: shellWord(workspace, '<workspace>') })}
              why={plain(t('settings.lines.body'))}
              says={line.set
                ? t('settings.lines.from.workspace', { branch: line.set, workspace })
                : t('projects.workspace.says.noLine')}
              own={line.set !== undefined}
              busy={busy}
              onClear={() => line.onSet(undefined)}
              editor={() => (
                <LineField
                  label={t('settings.lines.workspaceField', { workspace })}
                  set={line.set}
                  placeholder={t('settings.lines.eachCheckout')}
                  busy={busy}
                  clearable={false}
                  onSave={(branch) => line.onSet(branch)}
                />
              )}
            />
          )}
          {commits && (
            <Inheritable
              setHere={t('projects.workspace.setHere')}
              label={t('settings.commits.label')}
              twin={t('settings.commits.twin.workspace', { workspace: shellWord(workspace, '<workspace>') })}
              why={plain(t('settings.commits.body'))}
              says={commitRowSays(t, commits.set ?? COMMIT_DEFAULT, commits.set ? { source: 'workspace', workspace } : { source: 'default' })}
              own={commits.set !== undefined}
              busy={busy}
              wide
              onClear={() => commits.onChange({ clear: true })}
              editor={() => (
                <CommitField name={workspace} set={commits.set} inherited={COMMIT_DEFAULT} busy={busy} onChange={commits.onChange} />
              )}
            />
          )}
          {language && (
            <Inheritable
              setHere={t('projects.workspace.setHere')}
              label={t('projects.language.label')}
              twin={t('projects.workspace.twin.language', { workspace: shellWord(workspace, '<workspace>') })}
              why={plain(t('settings.sessionLanguage.body'))}
              says={language.set
                ? t('projects.workspace.says.language', {
                    name: language.table.find((row) => row.code === language.set)?.name ?? language.set,
                  })
                : t('settings.sessionLanguage.from.none')}
              own={language.set !== undefined}
              busy={busy}
              onClear={() => language.onSet(undefined)}
              editor={() => (
                <LanguageChoice
                  label={t('settings.sessionLanguage.field', { name: workspace })}
                  set={language.set}
                  table={language.table}
                  onChoose={(code) => language.onSet(code)}
                />
              )}
            />
          )}
          {helpers && (
            <Inheritable
              setHere={t('projects.workspace.setHere')}
              label={t('settings.helpers.label')}
              twin={t('settings.helpers.twin.workspace', { workspace: shellWord(workspace, '<workspace>') })}
              why={plain(t('settings.helpers.body'))}
              says={helperRowSays(t, helpers.set ?? HELPER_DEFAULT, helpers.set ? { source: 'workspace', workspace } : { source: 'default' })}
              own={helpers.set !== undefined}
              busy={busy}
              wide
              onClear={() => helpers.onChange({ clear: true })}
              editor={() => (
                <HelperField name={workspace} set={helpers.set} inherited={HELPER_DEFAULT} busy={busy} onChange={helpers.onChange} />
              )}
            />
          )}
          {handOver && (
            <Inheritable
              setHere={t('projects.workspace.setHere')}
              label={t('settings.handOver.label')}
              twin={t('settings.handOver.twin.workspace', { workspace: shellWord(workspace, '<workspace>') })}
              why={plain(t('settings.handOver.body'))}
              says={handOverRowSays(t, handOver.set ?? HAND_OVER_DEFAULT, handOver.set ? { source: 'workspace', workspace } : { source: 'default' })}
              own={handOver.set !== undefined}
              busy={busy}
              onClear={() => handOver.onChange({ clear: true })}
              editor={() => (
                <HandOverField name={workspace} set={handOver.set} inherited={HAND_OVER_DEFAULT} busy={busy} onChange={handOver.onChange} />
              )}
            />
          )}
          {read && (
            <Inheritable
              setHere={t('projects.workspace.setHere')}
              label={t('projects.workspace.read')}
              twin={t('projects.workspace.twin.read', { workspace: shellWord(workspace, '<workspace>') })}
              why={plain(t('settings.across.body'))}
              says={[
                (read.set ?? true) ? t('settings.across.readBy', { workspace }) : t('settings.across.readByNone'),
                read.set === undefined ? t('settings.across.from.default') : t('projects.workspace.says.read'),
              ].join(t('projects.setup.sentenceJoin'))}
              own={read.set !== undefined}
              busy={busy}
              onClear={() => read.onSet(undefined)}
              editor={() => (
                <Segmented<'on' | 'off'>
                  label={t('settings.across.workspaceField', { workspace })}
                  value={(read.set ?? true) ? 'on' : 'off'}
                  options={[
                    { value: 'on', label: t('settings.across.on') },
                    { value: 'off', label: t('settings.across.off') },
                  ]}
                  onChange={(choice) => !busy && read.onSet(choice === 'on')}
                />
              )}
            />
          )}
          {review && (
            <Inheritable
              setHere={t('projects.workspace.setHere')}
              label={t('settings.review.label')}
              twin={t('settings.review.twin.workspace', { workspace: shellWord(workspace, '<workspace>') })}
              why={plain(t('settings.review.body'))}
              says={reviewRowSays(t, review.set, review.set ? { source: 'workspace', workspace } : undefined)}
              own={review.set !== undefined}
              busy={busy}
              wide
              onClear={() => review.onChange({ clear: true })}
              editor={() => (
                <ReviewField name={workspace} owner="workspace" set={review.set} busy={busy} onChange={review.onChange} />
              )}
            />
          )}
          {opinion && (
            <Inheritable
              setHere={t('projects.workspace.setHere')}
              label={t('settings.opinion.label')}
              twin={t('settings.opinion.twin.workspace', { workspace: shellWord(workspace, '<workspace>') })}
              why={plain(t('settings.opinion.body'))}
              says={opinionRowSays(t, opinion.set, opinion.set ? { source: 'workspace', workspace } : undefined)}
              own={opinion.set !== undefined}
              busy={busy}
              wide
              onClear={() => opinion.onChange({ clear: true })}
              editor={() => (
                <OpinionField name={workspace} owner="workspace" set={opinion.set} busy={busy} onChange={opinion.onChange} />
              )}
            />
          )}
        </>
      ),
    });
  }
  if (accessShown) {
    sections.push({
      id: 'access',
      summary: accessSummary(t, accessShown.pauses, 'workspace'),
      body: (
        <AccessRows
          owner="workspace"
          name={workspace}
          pauses={accessShown.pauses}
          aboveFull={accessShown.aboveFull}
          busy={busy}
          setHere={t('projects.workspace.setHere')}
          onChange={accessShown.onChange}
        />
      ),
    });
  }
  if (gitShown && (gitShown.hosts.length > 0 || gitShown.access)) {
    const access = gitShown.access;
    sections.push({
      id: 'git',
      summary: [
        ...gitShown.hosts.map((each) => `${each.host}: ${each.chosen === 'own' ? t('settings.git.own')
          : gitShown.identities.find((identity) => identity.id === each.chosen)?.label ?? t('settings.git.workspace.default')}`),
        ...(access ? [gitAccessSummary(t, access.classes)] : []),
      ].join(JOIN),
      body: (
        <>
          {gitShown.hosts.length > 0 && <GitHosts hosts={gitShown.hosts} identities={gitShown.identities} acts={gitShown.acts} />}
          {access && (
            <GitAccessRows owner="workspace" name={workspace} classes={access.classes} above={access.above} busy={busy} onChange={access.onChange} />
          )}
        </>
      ),
    });
  }
  if (reachParts.length > 0) {
    sections.push({
      id: 'reach',
      summary: reachParts.join(JOIN),
      body: (
        <>
          {rulesRow}
          {remote.machines && <Undeclared machines={remote.machines} busy={busy} />}
        </>
      ),
    });
  }
  if (supervisor) {
    const appointed = supervisor.appointment;
    sections.push({
      id: 'supervisor',
      summary: !appointed
        ? t(supervisor.offered ? 'projects.supervisor.off' : 'projects.supervisor.notOffered')
        : appointed.dormant
          ? t('projects.supervisor.dormant')
          : t('projects.supervisor.window', { count: appointed.window }),
      body: <SupervisorDoor workspace={workspace} supervisor={supervisor} onOpen={onOpenAppointed} />,
    });
  }
  if (remoteParts.length > 0) {
    sections.push({
      id: 'remote',
      summary: remoteParts.join(JOIN),
      body: (
        <>
          {remote.wiring && (
            <Wiring workspace={workspace} wiring={remote.wiring} busy={busy} startOpen={startWiring} />
          )}
          {wired && remote.machines && <Undeclared machines={remote.machines} busy={busy} />}
          {rulesRow}
        </>
      ),
    });
  }

  return (
    <div className="grid">
      {sections.map((section) => (
        <SetupSection
          // Keyed by the workspace, so another workspace's page opens its own sections afresh.
          key={`${workspace}:${section.id}`}
          title={section.id === 'git' ? t('settings.git.workspace.title') : t(`projects.setup.section.${section.id}`)}
          summary={section.summary}
          startsOpen={opensItself[section.id] || open === section.id}
          // A place's page finds the row a door named by this (SETUPUX1v); Sessions' own Defaults is not Workflow's.
          row={rows === 'defaults' ? section.id : rows === 'sessions' ? (section.id === 'defaults' ? 'sessionDefaults' : section.id) : undefined}
        >
          {section.body}
        </SetupSection>
      ))}
    </div>
  );
}

/**
 * Where it syncs (D48 §5): the remote it is wired to, its key's audit prefix and *Unwire*, or *local only* and *Wire…*,
 * which opens the form one press away, since wiring is done once per machine per deployment. With the environment naming
 * the remote, the file is not read, and the row says which source decided.
 */
export function Wiring({ workspace, wiring, busy, startOpen }: {
  workspace: string;
  wiring: NonNullable<WorkspaceRemote['wiring']>;
  busy: boolean;
  startOpen: boolean;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(startOpen && !wiring.remote);
  const [url, setUrl] = useState('');
  const [key, setKey] = useState('');
  const close = () => { setUrl(''); setKey(''); setOpen(false); };
  const control = wiring.remote
    ? <Button variant="ghost" disabled={busy} onClick={wiring.onUnwire}>{t('settings.wiring.remove')}</Button>
    : !open && (
      <Button disabled={busy} onClick={() => setOpen(true)}>
        <Icon name="plus" size={13} />
        {t('projects.workspace.page.wire')}
      </Button>
    );

  return (
    <SettingRow
      label={t('projects.workspace.remote')}
      hint={t('projects.workspace.twin.remote', { workspace: shellWord(workspace, '<workspace>') })}
      why={plain(t('settings.wiring.body'))}
      control={control || undefined}
    >
      {wiring.remote
        ? (
          <p className="m-0 flex flex-wrap items-baseline gap-x-3 gap-y-1">
            <PathText path={wiring.remote.url} className="text-small" />
            <Tip content={t('settings.wiring.keyTip')}>
              <span className="font-mono text-small text-ink-faint">{wiring.remote.key}</span>
            </Tip>
          </p>
        )
        : <p className="m-0 text-small text-ink-soft">{t('projects.workspace.says.local')}</p>}
      {wiring.fromEnvironment && (
        <p className="m-0 mt-2 border-l-[3px] border-warn bg-raised px-3 py-1.5 text-small text-ink-soft">
          <Inline text={t('settings.wiring.fromEnvironment')} />
        </p>
      )}
      {open && (
        <form
          className="mt-2 grid gap-2"
          onSubmit={(event) => {
            event.preventDefault();
            if (url.trim() && key.trim()) wiring.onWire(url.trim(), key.trim(), close);
          }}
        >
          {/* Sized to what each holds, by the form's own width (R1, UIFIT1d): an address is long, a key is shorter. */}
          <FormGrid columns={['long', 'name']}>
            <FormRow>
              <FormField label={t('settings.wiring.url')}>
                <input
                  value={url}
                  onChange={(event) => setUrl(event.target.value)}
                  placeholder="https://…"
                  className="rounded-control border border-line-strong bg-raised px-2.5 py-1.5 font-mono text-body text-ink"
                />
              </FormField>
              <FormField label={t('settings.wiring.key')}>
                <input
                  value={key}
                  type="password"
                  onChange={(event) => setKey(event.target.value)}
                  placeholder="dk_…"
                  className="rounded-control border border-line-strong bg-raised px-2.5 py-1.5 font-mono text-body text-ink"
                />
              </FormField>
            </FormRow>
          </FormGrid>
          <Prose className="m-0 text-small">{t('settings.wiring.keyBody')}</Prose>
          <div className="flex gap-2">
            <Button type="submit" variant="primary" disabled={!url.trim() || !key.trim() || busy}>{t('settings.wiring.add')}</Button>
            <Button variant="ghost" onClick={close}>{t('settings.wiring.cancel')}</Button>
          </div>
        </form>
      )}
    </SettingRow>
  );
}

/**
 * The machines its remote counts that have not declared they know a bound quest (FABRIC-CLOSE1i2): while one is listed, no quest of the
 * workspace binds through the remote. Each carries *Retire*, for a machine whose store was deleted or that left the workspace; a push
 * from it counts it again, so the press is said as reversible and asks nothing more.
 */
function Undeclared({ machines, busy }: { machines: NonNullable<WorkspaceRemote['machines']>; busy: boolean }) {
  const { t } = useTranslation();
  return (
    <SettingRow label={t('projects.workspace.machines.label')} why={t('projects.workspace.machines.why')}>
      {machines.undeclared.length === 0
        ? <p className="m-0 text-small text-ink-soft">{t('projects.workspace.machines.none')}</p>
        : (
          <ul className="m-0 grid list-none gap-1 p-0">
            {machines.undeclared.map((machine) => (
              <li key={machine} className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
                <span className="font-mono text-small">{machine}</span>
                <Button variant="ghost" disabled={busy || Boolean(machines.retiring)} onClick={() => machines.onRetire(machine)}>
                  {t('projects.workspace.machines.retire')}
                </Button>
              </li>
            ))}
          </ul>
        )}
    </SettingRow>
  );
}
