import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { RuleListName } from '../settings/AgentRules';
import { type Accepting, AcceptingNote, LandingField, type LandingRule } from '../settings/Landings';
import { LanguageChoice, type LanguageOption } from '../settings/Languages';
import { LineField } from '../settings/Lines';
import { shellWord } from '../shellWord';
import { Button, Icon, Inline, PathText, Prose, Segmented, SettingRow, Tip } from '../ui';
import { RepositoryRules, type RuleLists } from './RepositoryRules';
import { Inheritable, landingSays, SetupSection } from './SetupParts';
import type { WorkspaceSection } from './tabs';
import { hostOf } from './workspace';

/**
 * A workspace's defaults on this machine (§4.3), each what the workspace sets, absent where it sets none, and the press
 * its terminal twin makes. A part the driver did not answer is null, and its row is absent rather than a control whose
 * save would be refused.
 */
export type WorkspaceDefaults = {
  /** Its line (WSR2): `daoris driver line --workspace`. */
  line?: { set?: string; onSet: (branch: string | undefined) => void } | null;
  /** How its work lands (WSR1, D87): `daoris driver landing --workspace`; the plugins here that land work (D100). */
  landing?: { set?: LandingRule; landers: string[]; onSet: (rule: LandingRule | undefined) => void } | null;
  /** The language its sessions write in (LANG1c), by the driver's table: `daoris driver language --workspace`. */
  language?: { set?: string; table: LanguageOption[]; onSet: (code: string | undefined) => void } | null;
  /** Whether agents outside its repositories read their checkouts (READ1, D107): `daoris driver across --workspace`. */
  read?: { set?: boolean; onSet: (read: boolean | undefined) => void } | null;
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
};

export type WorkspaceSetupProps = {
  workspace: string;
  defaults: WorkspaceDefaults;
  remote: WorkspaceRemote;
  busy?: boolean;
  /** A section a door asked to see open, beside those that open on their own. */
  open?: WorkspaceSection;
  /** The header's *Wire to a remote…*: the wiring form starts open. */
  wiring?: boolean;
};

const JOIN = ' · ';
const MERGE: LandingRule = { form: 'merge' };

/** A body read in a tip, which draws no code: its marks go, and its words stay. */
const plain = (text: string) => text.replace(/`/g, '');

/**
 * **A workspace's Setup** (UX6g, D150 §4.3): what it gives each of its repositories that sets none of its own, and where it
 * syncs with Claude Code's rules for it, in two sections that fold to the line naming their values. It is where Settings →
 * Workspace's defaults and wiring and Settings → Permissions' workspace rows went (§3.1).
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
export function WorkspaceSetup({ workspace, defaults, remote, busy = false, open, wiring: startWiring = false }: WorkspaceSetupProps) {
  const { t } = useTranslation();
  const daoris = (value: string) => t('projects.setup.marked.daoris', { value });
  const { line, landing, language, read } = defaults;

  const defaultsParts = [
    ...(line ? [line.set ? t('projects.setup.summary.line', { branch: line.set }) : daoris(t('projects.workspace.summary.noLine'))] : []),
    ...(landing ? [landing.set ? landingSays(t, landing.set) : daoris(t('projects.setup.summary.landsMerge'))] : []),
    ...(language
      ? [language.set
          ? language.table.find((row) => row.code === language.set)?.name ?? language.set
          : daoris(t('projects.setup.summary.noLanguage'))]
      : []),
    ...(read
      ? [read.set === undefined
          ? daoris(t('projects.setup.summary.read'))
          : t(read.set ? 'projects.setup.summary.read' : 'projects.setup.summary.unread')]
      : []),
  ];

  const wired = remote.wiring?.remote ?? null;
  const rules = remote.rules?.lists;
  const ruleCount = rules ? rules.allow.length + rules.ask.length + rules.deny.length : 0;
  const remoteParts = [
    ...(remote.wiring
      ? [wired ? t('projects.workspace.page.syncs', { host: hostOf(wired.url) }) : t('projects.workspace.page.local')]
      : []),
    ...(rules ? [ruleCount > 0 ? t('projects.setup.summary.rules', { count: ruleCount }) : t('projects.setup.summary.noRules')] : []),
  ];

  const opensItself: Record<WorkspaceSection, boolean> = {
    defaults: Boolean(line?.set || landing?.set || language?.set || read?.set !== undefined),
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
          {landing && <LandingDefault workspace={workspace} landing={landing} busy={busy} />}
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
        </>
      ),
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
          {remote.rules && (
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
          )}
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
          title={t(`projects.setup.section.${section.id}`)}
          summary={section.summary}
          startsOpen={opensItself[section.id] || open === section.id}
        >
          {section.body}
        </SetupSection>
      ))}
    </div>
  );
}

/** How work lands by default: an `Inheritable` whose control is the rule's own, and what accepting automatically gives. */
function LandingDefault({ workspace, landing, busy }: {
  workspace: string;
  landing: NonNullable<WorkspaceDefaults['landing']>;
  busy: boolean;
}) {
  const { t } = useTranslation();
  const [accepting, setAccepting] = useState<Accepting>(null);
  const set = landing.set;
  const says = set
    ? [
        t(`settings.landing.from.workspace.${set.form === 'branch' ? 'branch' : 'merge'}`, { pattern: set.pattern, workspace }),
        ...(set.form === 'branch' && set.plugin ? [t('settings.landing.byPlugin', { plugin: set.plugin })] : []),
        ...(set.form === 'branch' && set.autoAccept ? [t('settings.landing.byAuto')] : []),
      ].join(t('projects.setup.sentenceJoin'))
    : t('settings.landing.from.default');
  return (
    <Inheritable
      setHere={t('projects.workspace.setHere')}
      label={t('projects.setup.landing')}
      twin={t('projects.workspace.twin.landing', { workspace: shellWord(workspace, '<workspace>') })}
      why={plain(t('settings.landing.body'))}
      says={says}
      own={set !== undefined}
      busy={busy}
      wide
      onClear={() => landing.onSet(undefined)}
      editor={() => (
        <LandingField
          name={workspace}
          set={set}
          inherited={MERGE}
          landers={landing.landers}
          busy={busy}
          clearable={false}
          alignStart
          onSave={(rule) => landing.onSet(rule)}
          onAccepting={setAccepting}
        />
      )}
    >
      <AcceptingNote accepting={accepting} />
    </Inheritable>
  );
}

/**
 * Where it syncs (D48 §5): the remote it is wired to, its key's audit prefix and *Unwire*, or *local only* and *Wire…*,
 * which opens the form one press away, since wiring is done once per machine per deployment. With the environment naming
 * the remote, the file is not read, and the row says which source decided.
 */
function Wiring({ workspace, wiring, busy, startOpen }: {
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
          {/* Sized to what each holds: an address is long, a key is shorter. */}
          <div className="grid max-w-[40rem] gap-2 md:grid-cols-[minmax(0,1fr)_12rem]">
            <label className="grid gap-1 text-small text-ink-faint">
              {t('settings.wiring.url')}
              <input
                value={url}
                onChange={(event) => setUrl(event.target.value)}
                placeholder="https://…"
                className="rounded-control border border-line-strong bg-raised px-2.5 py-1.5 font-mono text-body text-ink"
              />
            </label>
            <label className="grid gap-1 text-small text-ink-faint">
              {t('settings.wiring.key')}
              <input
                value={key}
                type="password"
                onChange={(event) => setKey(event.target.value)}
                placeholder="dk_…"
                className="rounded-control border border-line-strong bg-raised px-2.5 py-1.5 font-mono text-body text-ink"
              />
            </label>
          </div>
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
