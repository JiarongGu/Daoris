import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { figure } from '../format';
import { useHarnessRun } from '../harnessRuns';
import { useRegistry } from '../queries';
import { SessionConsole } from '../SessionConsole';
import { useHarnessAction, useHarnesses, useRefreshHarnesses, useSetAgentSettings, useUsage } from '../shell';
import { SignIn } from '../SignIn';
import { byTool } from '../tools';
import {
  Button, Card, Chip, failure, Icon, Inline, type Notify, PathText, Pill, Prose, SectionTitle, SelectField,
  SettingRow, Tip, useErrorNotify,
} from '../ui';
import { workspacesOf } from '../workspaces';
import { AccountSettingsForm, AccountSettingsSummary } from './AccountSettings';
import { namer } from './namer';

/** What a DOOR does, and so what streams under it. */
const DOOR_ACTIONS = ['install', 'update', 'pin', 'unpin'] as const;

/**
 * The toolchain roster (D49 §4): which harnesses this machine has, and which accounts they hold.
 *
 * **Detection is free; acting is the person's click.** The versions and login states below come from
 * asking each tool its own reporting commands — read-only, no account, no network of Daoris's own.
 * Installing, updating and logging in each run that harness's OWN mechanism, only when pressed, and
 * never mid-session: a tool that changed under a running loop is a moving target nobody diffed.
 *
 * **A sign-in stays the tool's.** A profile is an isolated configuration home whose LOCATION Daoris
 * owns; the credential inside it is put there by the harness's own login flow and stays in the
 * harness's own store, under the person's OS account. Nothing on this surface reads one, and there
 * is nowhere to type one. The single field for a secret is an API key's (D67 §1), behind a press,
 * sent once and shown back only as its last four characters.
 */
export function AgentsDomain({ notify }: { notify: Notify }) {
  // The account a Remove has been pressed on once, by its directory — the second press is what
  // deletes it (D66 §3), and only on the row that asked.
  const [removing, setRemoving] = useState<string | null>(null);
  // The account whose own model and effort are open to change (AGT6), by its directory — one at a
  // time, closed until asked for, like every rare form here.
  const [tuning, setTuning] = useState<string | null>(null);
  const { t } = useTranslation();
  const roster = useHarnesses();
  const refresh = useRefreshHarnesses();
  const act = useHarnessAction();
  const tune = useSetAgentSettings();
  // What each account has carried (TOOL3). Beside the roster because it is about the same accounts.
  const usage = useUsage();
  // The circles this machine has, so an account can be chosen for one (D49 §4) — the terminal
  // could already do it (`daoris agent profile default … --workspace`), and the screen could not.
  const registry = useRegistry('machine');
  // Every circle, the unnamed `default` included: the CLI sets that circle's account too.
  const workspaces = workspacesOf(registry.data ?? []);
  useErrorNotify(roster.error, notify);

  // Which action is running, so its console can be shown under the harness that is doing it — one at
  // a time by construction, two installers racing over one PATH being nothing to make easy. Which
  // ACCOUNT a sign-in is for, so it lands on that row (2026-09-23), and which tool a sign-in to
  // another account runs for (D66 §3). Held above every view (SIGNIN1), so a sign-in outlives
  // leaving this domain: its panel is here on the way back, and its end is said wherever you are.
  const { running, runningProfile, signingInNew, busy: acting, run } = useHarnessRun();
  const busy = acting || act.isPending;
  // Which tool has its API-key field open, and what is typed in it (AGT3, D67 §1). The draft lives
  // here only until it is sent, and is dropped the moment it is — sent or taken back.
  const [keying, setKeying] = useState<string | null>(null);
  const [keyDraft, setKeyDraft] = useState('');
  // What a person calls an account: who signed in, else the key's handle, else the directory's name.
  const named = (profile: { name: string; account?: string | null; key?: string | null }) =>
    profile.account ?? (profile.key ? t('harness.profile.keyName', { handle: profile.key }) : profile.name);
  const closeKey = () => {
    setKeying(null);
    setKeyDraft('');
  };
  const addKey = (harness: string) => {
    const key = keyDraft.trim();
    if (!key) return;
    closeKey();
    act.mutate({ harness, action: 'key-add', key }, {
      onSuccess: (result) => notify(t('harness.profile.keyAdded', { profile: result.profile, handle: result.key })),
      onError: failure(notify),
    });
  };
  // The version being typed per harness (TOOL2). Local to the form: a pin only exists once the
  // install behind it succeeded, so there is nothing to remember until then.
  const [pinning, setPinning] = useState<Record<string, string>>({});

  /**
   * Whether a harness has its version form open.
   *
   * @remarks
   * 🔴 Accounts were hard to read, and the screenshot said why: **the rare forms were
   * always open, on every harness** — five harnesses meant five empty boxes and five copies of the
   * same paragraph, and the accounts, the thing a person came for, were a thin row between them.
   */
  const [opened, setOpened] = useState<Record<string, 'pin' | null>>({});
  const open = (harness: string, which: 'pin') =>
    setOpened((held) => ({ ...held, [harness]: held[harness] === which ? null : which }));

  // Defensive about the shape, deliberately, and for the reason SES1 wrote down: a shell older than
  // this surface answers something else entirely to a request it has never heard of. A machine's
  // settings page must not go blank because one card asked a question the host cannot answer.
  const answered = roster.data;
  const harnesses = Array.isArray(answered?.harnesses) ? answered.harnesses : null;
  // Same defensiveness, and the same reason: an older shell has never heard of this question.
  const accounts = Array.isArray(usage.data?.accounts) ? usage.data.accounts : [];
  if (!answered || !harnesses) return null;
  const nameOf = namer(t, harnesses);

  return (
    <Card className="mt-3.5">
      <SectionTitle>{t('harness.title')}</SectionTitle>
      {/* One line each, and the rest on the glyph: what a tool is here, and what an account is —
          said ONCE, where the second used to be repeated under every harness's add form. */}
      <SettingRow
        label={t('harness.body')}
        hint={t('harness.secrets')}
        why={t('harness.profile.note')}
        control={<PathText path={answered.settingsPath} className="text-small text-ink-faint" />}
      />

      {/* 🔴 A card per TOOL, and the adapters are its ways in: "harness account" and `*-acp` hid
          what the page was for. The reference project is a reference — take its design and its
          logic, not its words.

          The surface had been listing four adapters as four things to have opinions about. A person
          has one Claude Code and one account for it; whether Daoris holds the session over a pipe or
          over the protocol is Daoris's business, not a second tool. `byTool` reads that off
          `accountOf` and `wire`, both of which have said it all along. */}
      {byTool(harnesses).map((tool) => (
        <div
          key={tool.name}
          className="mt-3 rounded-card border border-line bg-page/60 p-3 first:mt-3.5"
        >
          <header className="flex flex-wrap items-center gap-2">
            {/* What a person calls it, and whose it is (AGT1) — `dsh` meant nothing to the owner
                until it said DeepSeek. The id a terminal types is on each door below. */}
            <span className="text-body font-semibold text-ink">{tool.product ?? tool.name}</span>
            {tool.maker && <span className="text-small text-ink-faint">{tool.maker}</span>}
            {tool.present
              ? <Pill tone="done">{t('harness.installed')}</Pill>
              : <Pill tone="neutral">{t('harness.absent')}</Pill>}
            {tool.doors.some((door) => door.harness === answered.adapter)
              && <Chip accent>{t('harness.spawns')}</Chip>}
          </header>

          {/* The ACCOUNTS, at the tool where they belong. They are the reason a person opened this
              card, and they had been a flat baseline row per adapter — so one account read as two
              whenever a tool had two doors, and the widest thing on the row was a seventy-character
              directory. The name leads now, its state is beside it, where the sessions actually go
              is stated rather than implied, and the directory is one truncated line underneath. */}
          <p className="mt-3 text-small font-semibold text-ink-soft">{t('harness.accounts')}</p>
          <ul className="m-0 mt-1 list-none p-0">
            {/* 🔴 The account a person actually HAS leads: the tool's own configuration home. A
                machine with no named profile read "No accounts" while its owner was logged in —
                and nothing said that sessions were running as that login. The state is the
                tool's own answer about its own home, read-only; Daoris never logs into it, so
                there is no button for that here, and the row says whose business it is. */}
            <li className="flex flex-wrap items-center gap-x-3 gap-y-1 py-2">
              <span className="flex min-w-0 flex-1 basis-56 flex-col gap-0.5">
                <span className="flex flex-wrap items-center gap-2">
                  <Icon name="account" size={13} className="text-ink-faint" />
                  {/* Who, when the tool says (D66 §3) — a person knows an account by who it is. */}
                  <span className="text-body font-medium text-ink">{tool.ownAccount ?? t('harness.own')}</span>
                  {/* A name that repeats says which it is: signed in as the same person here and
                      in an account made in Daoris, the two rows read as one fact stated twice. */}
                  {tool.ownAccount && tool.accounts.some((profile) => named(profile) === tool.ownAccount) && (
                    <span className="text-meta text-ink-faint">{t('harness.own')}</span>
                  )}
                  {tool.present && tool.ownLogin !== 'unknown' && (
                    <Pill tone={tool.ownLogin === 'in' ? 'done' : 'neutral'}>
                      {t(`harness.login.${tool.ownLogin}`)}
                    </Pill>
                  )}
                  {tool.machineDefault === null && (
                    <Chip accent>{t('harness.profile.sessionsUse')}</Chip>
                  )}
                </span>
                <span className="text-meta text-ink-faint">{t('harness.ownHome')}</span>
              </span>
              <div className="ml-auto flex min-w-0 flex-wrap items-center justify-end gap-1">
                {/* Naming NO profile clears the default — "use the tool's own home again". */}
                {tool.machineDefault !== null && (
                  <Button
                    variant="ghost"
                    disabled={busy}
                    onClick={() => run(tool.doors[0]!.harness, 'profile-default')}
                  >
                    {t('harness.profile.use')}
                  </Button>
                )}
                {/* 🔴 Choosing the tool's own home for a workspace CLEARS that workspace's account, and with a machine
                    default set its sessions then run as that default (LOOK2c, found by LEFT3): no file can say "the
                    tool's own home here" over a machine default. So each choice says where it leads before the press,
                    and the press says what sessions there run as, as the terminal's verb does. */}
                {workspaces.length > 0 && (
                  <SelectField
                    value=""
                    onChange={(workspace) =>
                      run(tool.doors[0]!.harness, 'profile-default', undefined, undefined, workspace)}
                    options={workspaces.map((workspace) => ({
                      value: workspace,
                      label: tool.machineDefault !== null
                        ? t('harness.profile.useForFallsBack', {
                          workspace,
                          account: named(tool.accounts.find((profile) => profile.name === tool.machineDefault)
                            ?? { name: tool.machineDefault }),
                        })
                        : workspace,
                    }))}
                    placeholder={t('harness.profile.useForPlaceholder')}
                    ariaLabel={t('harness.profile.useFor', { profile: t('harness.own') })}
                  />
                )}
              </div>
            </li>
            {tool.accounts.map((profile) => (
                <li
                  key={profile.home}
                  className="flex flex-wrap items-center gap-x-3 gap-y-1 border-t border-line py-2"
                >
                  <span className="flex min-w-0 flex-1 basis-56 flex-col gap-0.5">
                    <span className="flex flex-wrap items-center gap-2">
                      <Icon name="account" size={13} className="text-ink-faint" />
                      {/* Who is signed in, when the tool says (D66 §3): an account made by signing
                          in is `account-2` on disk, and nobody knows it by that. The directory's
                          name is still on the line below, inside its path, for a terminal. */}
                      <span className="text-body font-medium text-ink">{named(profile)}</span>
                      {/* 🔴 A key account is never shown as "logged in" (AGT3). Measured: the tool
                          says logged in for ANY key, a wrong one included, and the first request is
                          where a bad key is refused. The pill says what is known. */}
                      {profile.key ? (
                        <Tip content={t('harness.login.keyedTip')}>
                          <Pill tone="neutral">{t('harness.login.keyed')}</Pill>
                        </Tip>
                      ) : (
                        <Pill tone={profile.login === 'in' ? 'done' : 'neutral'}>
                          {t(`harness.login.${profile.login}`)}
                        </Pill>
                      )}
                      {/* States the CONSEQUENCE, not the setting: "this machine's default" is a fact
                          about a config file, and what a person wants is which account the next
                          session runs as — the same fact worded as an answer. */}
                      {tool.machineDefault === profile.name && (
                        <Chip accent>{t('harness.profile.sessionsUse')}</Chip>
                      )}
                      {/* And which circles run as it (D49 §4) — one chip per circle, the same fact
                          worded the same way. */}
                      {tool.workspaceDefaults
                        .filter((circle) => circle.profile === profile.name)
                        .map((circle) => (
                          <Chip accent key={circle.workspace}>
                            {t('harness.profile.workspaceUses', { workspace: circle.workspace })}
                          </Chip>
                        ))}
                    </span>
                    <Tip content={t('harness.homeTip')}>
                      <span className="truncate font-mono text-meta text-ink-faint">{profile.home}</span>
                    </Tip>
                    {/* What the account runs on, by the tool's own file under it (AGT6), where Daoris
                        knows that file. The owner could see a session's model only inside the session. */}
                    {profile.settings && <AccountSettingsSummary settings={profile.settings} />}
                    {/* 🔴 What the next step IS and what it will do, on the row that needs it. After
                        "Add" there was a name, a "not logged in" pill and a button, and nothing
                        about the browser window about to open or where the output would go. */}
                    {profile.login !== 'in' && (
                      <span className="text-meta text-ink-faint">{t('harness.login.hint')}</span>
                    )}
                  </span>
                  <div className="ml-auto flex min-w-0 flex-wrap items-center justify-end gap-1">
                    {/* Logging in is the one thing here that is a step in a task rather than a
                        preference, so it is the one that looks like a button. It runs against the
                        account-owning door, because that is the tool that HAS the login flow. */}
                    {/* A key account is signed in by its key (AGT3): there is no sign-in to offer. */}
                    {tool.doors[0]!.signsIn !== false && !profile.key && (
                      <Button
                        variant={profile.login === 'in' ? 'ghost' : 'default'}
                        disabled={busy || !tool.present}
                        onClick={() => run(tool.doors[0]!.harness, 'login', profile.name)}
                      >
                        {t(profile.login === 'in' ? 'harness.login.again' : 'harness.login.action')}
                      </Button>
                    )}
                    {tool.machineDefault !== profile.name && (
                      <Button
                        variant="ghost"
                        disabled={busy}
                        onClick={() => run(tool.doors[0]!.harness, 'profile-default', profile.name)}
                      >
                        {t('harness.profile.use')}
                      </Button>
                    )}
                    {/* The account's own model and effort (AGT6, D98) — offered only where the tool's
                        settings are known, which is the rule every control on this row follows. */}
                    {tool.settingsChoices && profile.settings && tuning !== profile.home && (
                      <Button variant="ghost" disabled={busy} onClick={() => setTuning(profile.home)}>
                        {t('harness.settings.open')}
                      </Button>
                    )}
                    {/* 🔴 Remove REMOVES (D66 §3). "Forget" un-pointed the account and kept any
                        directory the tool would not call signed out, so a removed account stayed
                        listed and signed in — the owner's report. It deletes the account's
                        directory, sign-in and all, so the first press only asks. */}
                    {removing !== profile.home && (
                      <Button
                        variant="ghost"
                        disabled={busy}
                        onClick={() => setRemoving(profile.home)}
                      >
                        <Icon name="remove" size={13} />
                        {t('harness.profile.remove')}
                      </Button>
                    )}
                    {workspaces.length > 0 && (
                      <SelectField
                        value=""
                        onChange={(workspace) =>
                          run(tool.doors[0]!.harness, 'profile-default', profile.name, undefined, workspace)}
                        options={workspaces.map((workspace) => ({ value: workspace, label: workspace }))}
                        placeholder={t('harness.profile.useForPlaceholder')}
                        ariaLabel={t('harness.profile.useFor', { profile: named(profile) })}
                      />
                    )}
                  </div>
                  {removing === profile.home && (
                    <div
                      role="group"
                      aria-label={t('harness.profile.removeTitle', { account: named(profile) })}
                      className="flex basis-full flex-wrap items-center gap-2 rounded-control border border-line bg-sunken px-2.5 py-2"
                    >
                      <span className="min-w-0 flex-1 basis-64 text-small text-ink-soft">
                        {t('harness.profile.removeConfirm')}
                      </span>
                      <Button
                        variant="danger"
                        disabled={busy}
                        onClick={() => {
                          setRemoving(null);
                          run(tool.doors[0]!.harness, 'profile-remove', profile.name);
                        }}
                      >
                        {t('harness.profile.removeMeanIt')}
                      </Button>
                      <Button variant="ghost" onClick={() => setRemoving(null)}>
                        {t('common.cancel')}
                      </Button>
                    </div>
                  )}
                  {tuning === profile.home && tool.settingsChoices && profile.settings && (
                    <AccountSettingsForm
                      harness={tool.doors[0]!.harness}
                      account={profile.name}
                      accountLabel={named(profile)}
                      settings={profile.settings}
                      choices={tool.settingsChoices}
                      busy={tune.isPending}
                      onSave={(change) => tune.mutate({ harness: tool.doors[0]!.harness, profile: profile.name, ...change }, {
                        onSuccess: () => {
                          setTuning(null);
                          notify(t('harness.settings.saved', { account: named(profile) }));
                        },
                        onError: failure(notify),
                      })}
                      onCancel={() => setTuning(null)}
                    />
                  )}
                  {/* Signing in happens HERE, on the account it is for. */}
                  {running === `${tool.doors[0]!.harness}:login` && runningProfile === profile.name && (
                    <SignIn id={running} harness={tool.doors[0]!.harness} profile={named(profile)} />
                  )}
                </li>
              ))}
            </ul>
          {/* A tool whose own settings Daoris does not know is offered none, and says so once (AGT6):
              inventing its keys would be a guess written into somebody else's file. */}
          {tool.settingsChoices === null && (
            <p className="m-0 mt-1 text-meta text-ink-faint">
              {t('harness.settings.unknown', { tool: tool.product ?? tool.name })}
            </p>
          )}

          {/* 🔴 An account is made by SIGNING IN (D66 §3). There was a name box first — a name
              typed before anyone knew whose account it was — then a login as a second step. Now
              one press runs the tool's own sign-in into a fresh account, which is kept only if the
              sign-in finishes and is listed by who signed in. */}
          {signingInNew === tool.doors[0]!.harness && (
            <SignIn
              id={`${tool.doors[0]!.harness}:login-new`}
              harness={tool.doors[0]!.harness}
              action="login-new"
              tool={tool.product ?? tool.name}
            />
          )}

          {/* 🔴 An account that is an API key (AGT3, D67 §1). One field,
              behind a press, only on an agent that takes a key. The draft is dropped the moment it
              is sent, and the page is told back only the key's last four characters. */}
          {keying === tool.name && (
            <form
              className="mt-2 flex flex-wrap items-center gap-2"
              onSubmit={(event) => {
                event.preventDefault();
                addKey(tool.doors[0]!.harness);
              }}
            >
              <input
                autoFocus
                type="password"
                autoComplete="off"
                spellCheck={false}
                value={keyDraft}
                onChange={(event) => setKeyDraft(event.target.value)}
                aria-label={t('harness.profile.keyLabel', { tool: tool.product ?? tool.name })}
                placeholder={t('harness.profile.keyPlaceholder')}
                className="min-w-72 flex-1 rounded-control border border-line-strong bg-sunken px-2.5 py-1 font-mono text-small text-ink outline-none placeholder:text-ink-faint"
              />
              <Button type="submit" variant="primary" disabled={busy || !keyDraft.trim()}>
                {t('harness.profile.keySave')}
              </Button>
              <Button variant="ghost" onClick={closeKey}>{t('common.cancel')}</Button>
              <span className="basis-full text-meta text-ink-faint">{t('harness.profile.keyHint')}</span>
            </form>
          )}

          {signingInNew !== tool.doors[0]!.harness && keying !== tool.name && (
            <div className="mt-2 flex flex-wrap items-center gap-1">
              {tool.doors[0]!.signsIn !== false && (
                <Button
                  variant="ghost"
                  disabled={busy || !tool.present}
                  onClick={() => run(tool.doors[0]!.harness, 'login-new')}
                >
                  <Icon name="plus" size={13} />
                  {t('harness.profile.signInNew')}
                </Button>
              )}
              {tool.doors[0]!.takesKey && (
                <Button
                  variant="ghost"
                  disabled={busy || !tool.present}
                  onClick={() => setKeying(tool.name)}
                >
                  <Icon name="account" size={13} />
                  {t('harness.profile.addKey')}
                </Button>
              )}
            </div>
          )}

          {/* 🔴 The ways in, beneath the tool rather than beside it. Each is installed, versioned
              and pinned separately — they are different packages — which is exactly why they had
              looked like different tools. Named `harness` below because that is what `driver.json`
              calls this and what `daoris driver adapter` takes. */}
          <p className="mt-3 text-small font-semibold text-ink-soft">{t('harness.doors')}</p>
          {tool.doors.map((harness) => (
            <div key={harness.harness} className="mt-1 border-t border-line pt-2">
              <header className="flex flex-wrap items-center gap-2">
                <Pill tone="neutral">
                  {t(harness.wire === 'acp' ? 'harness.wire.acp' : 'harness.wire.pipe')}
                </Pill>
                <span className="font-mono text-small text-ink">{harness.harness}</span>
                {harness.present
                  ? <span className="font-mono text-small text-ink-faint">{harness.version}</span>
                  : <span className="text-small text-ink-faint">{t('harness.absent')}</span>}
                {/* Where a declared door came from (D64): the plugin's folder is where its command
                    and its posture live, and a person asking "why is this here" is asking that. */}
                {harness.plugin && <Chip>{t('harness.declaredBy', { plugin: harness.plugin })}</Chip>}

                <span className="ml-auto flex gap-2">
                  {!harness.present && (
                    <Button disabled={busy} onClick={() => run(harness.harness, 'install')}>
                      {t('harness.install')}
                    </Button>
                  )}
                  {/* 🔴 USE1a: offered only where it does something, and saying which it does.
                      It was offered on every door, and on a pinned one it could only be refused. */}
                  {harness.present && (harness.updates === 'pin' || harness.updates === 'tool') && (
                    <Tip content={t(harness.updates === 'pin' ? 'harness.update.pinTip' : 'harness.update.toolTip')}>
                      <Button variant="ghost" disabled={busy} onClick={() => run(harness.harness, 'update')}>
                        {t('harness.update')}
                      </Button>
                    </Tip>
                  )}
                </span>
              </header>

          {/* The absence names what it is, rather than leaving a person to guess at a blank row.
              🔴 CLAMPED, with the whole of it one hover away. What the host hands over here ends in
              the platform's own exception text and a machine path — true, occasionally the thing you
              need, and four lines of a five-line card when it is not. The useful sentence is the
              first one, and clamping keeps it first without parsing somebody else's wording. */}
          {harness.problem && (
            <Tip content={harness.problem}>
              <p className="mt-1.5 line-clamp-2 text-small text-ink-soft"><Inline text={harness.problem} /></p>
            </Tip>
          )}

          {/* The managed toolchain (TOOL2/D57). Absent means PATH, which is the usual case and is
              stated rather than left blank — "Daoris manages this" and "the machine happens to have
              one" are different facts about the same working session.

              Absent entirely where the harness declares no package: a control whose only outcome is
              a refusal is worse than none, and the WHY lives once in the card's body rather than
              beside every row (measured — repeated per harness it was two long lines each). */}
          {harness.pinnable && (
          <div className="mt-2 flex flex-wrap items-center gap-2 text-small">
            {harness.pinned ? (
              <>
                <Pill tone={harness.managed ? 'done' : 'declined'}>
                  {t(harness.managed ? 'harness.pin.pinned' : 'harness.pin.missing',
                    { version: harness.pinned })}
                </Pill>
                {harness.managed && (
                  <Tip content={t('harness.pin.managedTip')}>
                    <span className="min-w-0 flex-1 truncate font-mono text-meta text-ink-faint">
                      {harness.managed}
                    </span>
                  </Tip>
                )}
                <Button
                  variant="ghost"
                  className="ml-auto"
                  disabled={busy}
                  onClick={() => run(harness.harness, 'unpin')}
                >
                  {t('harness.pin.unpin')}
                </Button>
              </>
            ) : (
              <>
                {/* A way in that is not there does not run from PATH yet: it read "runs from PATH"
                    under "not on this machine's PATH" (UX5 U56). */}
                <span className="text-ink-faint">{t(harness.present ? 'harness.pin.fromPath' : 'harness.pin.fromPathAbsent')}</span>
                {/* 🔴 Behind a press, not always open. An always-open `1.2.3` box on every harness
                    is five inputs offering an action almost nobody takes, and they were the widest
                    thing on the surface. */}
                <Button
                  variant="ghost"
                  className="ml-auto"
                  aria-expanded={opened[harness.harness] === 'pin'}
                  onClick={() => open(harness.harness, 'pin')}
                >
                  {t('harness.pin.open')}
                </Button>
              </>
            )}
          </div>
          )}

          {harness.pinnable && !harness.pinned && opened[harness.harness] === 'pin' && (
            <form
              className="mt-2 flex flex-wrap items-center gap-2"
              onSubmit={(event) => {
                event.preventDefault();
                const version = (pinning[harness.harness] ?? '').trim();
                if (!version) return;
                run(harness.harness, 'pin', undefined, version);
              }}
            >
              <input
                autoFocus
                aria-label={t('harness.pin.version', { harness: harness.harness })}
                value={pinning[harness.harness] ?? ''}
                onChange={(event) => setPinning(
                  (held) => ({ ...held, [harness.harness]: event.target.value }))}
                placeholder={t('harness.pin.placeholder')}
                className="w-32 rounded-control border border-line-strong bg-sunken px-2.5 py-1 font-mono text-small text-ink outline-none placeholder:text-ink-faint"
              />
              <Button
                type="submit"
                disabled={busy || !(pinning[harness.harness] ?? '').trim()}
              >
                {t('harness.pin.action')}
              </Button>
            </form>
          )}

              {/* A tool action is a process like any other, so it streams through the same console
                  (D49 §2). An install that printed nothing until it finished is indistinguishable
                  from one that hung. It belongs to the DOOR that is doing it — and only what a door
                  does: a sign-in streams inside its own panel above, and an account edit ends in
                  its sentence, so neither is shown here under "Ways in" (D66). */}
              {DOOR_ACTIONS.some((action) => running === `${harness.harness}:${action}`)
                && <SessionConsole id={running!} />}
            </div>
          ))}
        </div>
      ))}

      {/* What each account has carried (TOOL3/D57 §4) — the question "multiple accounts with usage
          management" actually asks. Derived from the sessions, so the two can never disagree, and
          absent entirely on a machine that has measured nothing rather than a row of zeroes. */}
      {accounts.length > 0 && (
        <div id="settings-usage" className="mt-4 scroll-mt-3 border-t border-line pt-3.5">
          <SectionTitle>{t('usage.title')}</SectionTitle>
          <Prose className="mt-1.5 text-small">{t('usage.body')}</Prose>
          <ul className="m-0 mt-2 list-none p-0">
            {accounts.map((account) => {
              // Named as the list above names it: who signed in, a key's handle, else the directory,
              // and the tool's own home as its row says it (UX5 U53: "its own home", and a named
              // account by its directory in the accent). An own home whose name another account on
              // this door also carries says which it is, as the list does.
              const called = nameOf(account.harness, account.profile);
              const repeated = !account.profile && accounts.some((other) =>
                other.harness === account.harness && other.profile && nameOf(other.harness, other.profile) === called);
              return (
                <li
                  key={`${account.harness}:${account.profile ?? ''}`}
                  className="flex flex-wrap items-baseline gap-3 border-t border-line py-1.5 first:border-t-0"
                >
                  <span className="font-mono text-small">{account.harness}</span>
                  <Chip>{called}</Chip>
                  {repeated && <span className="text-meta text-ink-faint">{t('harness.own')}</span>}
                  <span className="text-small text-ink-soft">
                    {t('usage.sessions', { count: account.sessions })}
                  </span>
                  {/* The unit is named, and it is "context" rather than "tokens": the number is in
                      the harness's own units, and calling them tokens would be a claim Daoris cannot
                      make. A bare figure in a column is unreadable without it (measured by looking). */}
                  <Tip content={t('usage.contextTip')}>
                    <span className="ml-auto font-mono text-small text-ink-faint">
                      {t('usage.context', { used: figure(account.used) })}
                    </span>
                  </Tip>
                </li>
              );
            })}
          </ul>
          <p className="mt-2 max-w-prose text-meta text-ink-faint">{t('usage.note')}</p>
        </div>
      )}

      <Button className="mt-4" disabled={refresh.isPending} onClick={() => refresh.mutate()}>
        {t('harness.refresh')}
      </Button>
    </Card>
  );
}
