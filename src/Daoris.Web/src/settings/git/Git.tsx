import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence, ago } from '../../locales/format';
import { ExternalLink } from '../../design/links';
import { type Answered, InlineConfirm, Refused } from '../../work/InlineConfirm';
import {
  Button, Card, CardHeader, Dot, EmptyState, Inline, MonoWell, Pill, Prose, SelectField, SettingList, SettingRow,
} from '../../design/ui';

/** The way an identity signs in: an SSH key of Daoris's own, or an HTTPS sign-in kept by Git Credential Manager (D198). */
export type GitWay = 'ssh' | 'https';

/** An identity's last test as `TOOLS_LIST`'s `identities` answers it (GITAUTH1e, the git-auth design §3.1). */
export type GitTestShown = { result: string; user?: string | null; at: string };

/** One git identity: who Daoris's git acts as on a host. Holds no secret; the public key and the fingerprint are the most. */
export type GitIdentityShown = {
  id: string;
  /** The person's name for it: authored text, never translated. */
  label: string;
  host: string;
  way: GitWay;
  user?: string | null;
  keyType?: string | null;
  fingerprint?: string | null;
  readOnly: boolean;
  default: boolean;
  commitsName?: string | null;
  commitsEmail?: string | null;
  test?: GitTestShown | null;
  lastUsed?: string | null;
  publicKey?: string | null;
  keyPage?: string | null;
  repositories: number;
  workspaces: number;
  /** Every scope pointing at it, as the driver writes it (`engine's origin`, `workspace games's github.com`). */
  pointedAt: string[];
};

/** The page: the identities and the hosts the person's own sign-ins are chosen for. */
export type GitPageShown = {
  problem?: string | null;
  identities: GitIdentityShown[];
  own: string[];
  hosts: string[];
  /** Each workspace's choice for a host: an identity's id, or `own`. */
  choices?: { workspace: string; host: string; value: string }[];
};

/** A test's answer (§7.2 step 6): the host's keys are listed where it is not yet trusted, each against the host's published one. */
export type GitTestAnswer = {
  result: string;
  user?: string | null;
  words?: string | null;
  hostKeys: { algorithm: string; fingerprint: string; published?: boolean | null }[];
};

/** What the add form sends (§7.2). */
export type GitAddInput = {
  way: GitWay;
  host: string;
  label: string;
  name?: string;
  email?: string;
  /** SSH: a key file the person names, imported; absent generates a new key. */
  importFile?: string;
  rsa?: boolean;
  /** HTTPS: the account, and a token pasted; absent signs in through the browser. */
  user?: string;
  token?: string;
};

/** What a new identity answers: its id, and the public key to give the host (SSH). */
export type GitAdded = { identity: string; label: string; host: string; way: GitWay; publicKey?: string | null };

/** The page's presses. Each answers or rejects; a rejection is said where it was pressed (R3). */
export type GitActs = {
  add: (input: GitAddInput) => Promise<GitAdded>;
  test: (identity: GitIdentityShown, options: { trust?: string; repository?: string }) => Promise<GitTestAnswer>;
  rename: (identity: GitIdentityShown, label: string) => Promise<unknown>;
  commitsAs: (identity: GitIdentityShown, who: { name: string; email: string } | null) => Promise<unknown>;
  readOnly: (identity: GitIdentityShown, on: boolean) => Promise<unknown>;
  remove: (identity: GitIdentityShown) => Promise<unknown>;
  makeDefault: (identity: GitIdentityShown) => Promise<unknown>;
  /** Choose the person's own sign-ins for a host at this machine, or stop (§3.3); choosing is a widening. */
  own: (host: string, on: boolean) => Promise<unknown>;
  copy: (text: string) => void;
};

const TEST_TONE: Record<string, 'done' | 'failed' | 'waits' | 'idle'> = {
  reached: 'done', refused: 'failed', 'host-unknown': 'failed', 'host-changed': 'failed', unreachable: 'waits',
};

/** The mark and words of an identity's last test (§7.1): reached as, refused, host not yet trusted, host key changed, not tested. */
export function TestState({ identity }: { identity: GitIdentityShown }) {
  const { t } = useTranslation();
  const { test } = identity;
  if (!test) return <Dot tone="idle" label={t('settings.git.test.never')} />;
  const word = test.result === 'reached'
    ? test.user ? t('settings.git.test.reachedAs', { user: test.user, time: ago(test.at) }) : t('settings.git.test.reached', { time: ago(test.at) })
    : t(`settings.git.test.${test.result}`, { defaultValue: t('settings.git.test.unreachable') });
  return <Dot tone={TEST_TONE[test.result] ?? 'waits'} label={word} />;
}

/** The first panel an identity's row opens under it; one at a time. */
type Panel = 'rename' | 'commits' | 'remove' | 'readOnlyOff' | 'trust' | null;

/** One identity (§7.1): its facts, its test, and the acts its way and state offer. */
export function GitIdentityRow({ identity, acts, testing = false, answer = null }: {
  identity: GitIdentityShown;
  acts: GitActs;
  /** A test is under way (the story draws it). */
  testing?: boolean;
  /** A test's answer the row opens on (the story draws it). */
  answer?: GitTestAnswer | null;
}) {
  const { t } = useTranslation();
  const [panel, setPanel] = useState<Panel>(answer && answer.result.startsWith('host-') ? 'trust' : null);
  const [busy, setBusy] = useState(testing);
  const [refusal, setRefusal] = useState<string | null>(null);
  const [tested, setTested] = useState<GitTestAnswer | null>(answer);
  const [label, setLabel] = useState(identity.label);
  const [who, setWho] = useState({ name: identity.commitsName ?? '', email: identity.commitsEmail ?? '' });
  const [repository, setRepository] = useState('');

  const press = async <T,>(act: () => Promise<T>, then?: (value: T) => void) => {
    setBusy(true);
    setRefusal(null);
    try {
      const value = await act();
      then?.(value);
    } catch (error) {
      setRefusal(sentence(error));
    } finally {
      setBusy(false);
    }
  };
  const confirmed = (act: () => Promise<unknown>) => (answered: Answered) => {
    act().then(() => { setPanel(null); answered.done(); }, (error) => answered.refused(sentence(error)));
  };

  const ssh = identity.way === 'ssh';
  const failed = identity.test && identity.test.result !== 'reached';
  const needsRepository = !ssh;
  const test = (trust?: string) => press(
    () => acts.test(identity, { ...(trust ? { trust } : {}), ...(needsRepository ? { repository: repository.trim() } : {}) }),
    (value) => { setTested(value); setPanel(value.result.startsWith('host-') ? 'trust' : null); },
  );

  return (
    <li className="grid gap-1 border-t border-line py-2.5 first:border-t-0 first:pt-0" data-git-identity={identity.id}>
      <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
        <span className="min-w-0 wrap-anywhere text-body font-semibold text-ink">{identity.label}</span>
        {identity.default && <Pill tone="done">{t('settings.git.defaultFor', { host: identity.host })}</Pill>}
        {identity.readOnly && <Pill>{t('settings.git.readOnly')}</Pill>}
      </div>
      <div className="min-w-0 wrap-anywhere text-small text-ink-soft">
        {identity.host} · {ssh
          ? t('settings.git.sshFact', { type: identity.keyType ?? '', fingerprint: identity.fingerprint ?? '' })
          : t('settings.git.httpsFact', { user: identity.user ?? '' })}
      </div>
      {(identity.commitsName || identity.commitsEmail) && (
        <div className="min-w-0 wrap-anywhere text-small text-ink-faint">
          {t('settings.git.commitsAs', { who: `${identity.commitsName ?? ''} <${identity.commitsEmail ?? ''}>` })}
        </div>
      )}
      <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1">
        <TestState identity={identity} />
        <span className="text-meta text-ink-faint">
          {identity.lastUsed ? t('settings.git.lastUsed', { time: ago(identity.lastUsed) }) : t('settings.git.neverUsed')}
          {' · '}{t('settings.git.usedBy', { repositories: identity.repositories, workspaces: identity.workspaces })}
        </span>
      </div>
      {identity.test?.result === 'refused' && <p className="m-0 text-small text-ink-danger">{t('settings.git.refused')}</p>}

      <div className="mt-1 flex min-w-0 flex-wrap items-center gap-1.5">
        <Button disabled={busy || (needsRepository && !repository.trim())} onClick={() => test()}>
          {failed ? t('settings.git.testAgain') : t('settings.git.test')}
        </Button>
        {ssh && identity.publicKey && (
          <Button variant="ghost" onClick={() => acts.copy(identity.publicKey!)}>{t('settings.git.copyKey')}</Button>
        )}
        {ssh && identity.keyPage && (
          <ExternalLink href={identity.keyPage} system className="text-small">{t('settings.git.keyPage', { host: identity.host })}</ExternalLink>
        )}
        {!identity.default && (
          <Button variant="ghost" disabled={busy} onClick={() => press(() => acts.makeDefault(identity))}>
            {t('settings.git.makeDefault', { host: identity.host })}
          </Button>
        )}
        <Button variant="ghost" disabled={busy} onClick={() => setPanel(panel === 'rename' ? null : 'rename')}>{t('settings.git.rename')}</Button>
        <Button variant="ghost" disabled={busy} onClick={() => setPanel(panel === 'commits' ? null : 'commits')}>{t('settings.git.commitsAsAct')}</Button>
        <Button
          variant="ghost"
          disabled={busy}
          onClick={() => (identity.readOnly ? setPanel('readOnlyOff') : press(() => acts.readOnly(identity, true)))}
        >
          {identity.readOnly ? t('settings.git.readOnlyOff') : t('settings.git.readOnlyOn')}
        </Button>
        <Button variant="danger" disabled={busy} onClick={() => setPanel('remove')}>{t('settings.git.remove')}</Button>
      </div>

      {needsRepository && (
        <label className="mt-1 grid gap-1 text-small text-ink-faint">
          {t('settings.git.testRepository')}
          <input
            value={repository}
            onChange={(event) => setRepository(event.target.value)}
            placeholder={`https://${identity.host}/owner/repository`}
            spellCheck={false}
            aria-label={t('settings.git.testRepositoryOf', { label: identity.label })}
            className="min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink outline-none placeholder:text-ink-faint"
          />
        </label>
      )}
      {refusal && <Refused sentence={refusal} />}

      {tested && panel === 'trust' && (
        <div className="mt-1 grid gap-1.5 border-l-[3px] border-warn pl-2.5" role="group" aria-label={t('settings.git.trustTitle', { host: identity.host })}>
          <p className="m-0 text-small text-ink-soft">
            <Inline text={tested.result === 'host-changed'
              ? t('settings.git.trustChanged', { host: identity.host })
              : t('settings.git.trustUnknown', { host: identity.host })}
            />
          </p>
          {tested.hostKeys.map((key) => (
            <div key={key.fingerprint} className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-small">
              <span className="font-mono text-meta text-ink-soft">{key.algorithm} {key.fingerprint}</span>
              <span className={key.published === false ? 'text-ink-danger' : 'text-ink-faint'}>
                {key.published === true ? t('settings.git.keyPublished') : key.published === false ? t('settings.git.keyNotPublished') : t('settings.git.keyNoneKnown')}
              </span>
              <Button disabled={busy} onClick={() => test(key.fingerprint)}>{t('settings.git.trust')}</Button>
            </div>
          ))}
        </div>
      )}

      {panel === 'rename' && (
        <form
          className="mt-1 flex min-w-0 flex-wrap items-center gap-2"
          onSubmit={(event) => {
            event.preventDefault();
            if (label.trim()) press(() => acts.rename(identity, label.trim()), () => setPanel(null));
          }}
        >
          <input
            aria-label={t('settings.git.renameOf', { label: identity.label })}
            value={label}
            onChange={(event) => setLabel(event.target.value)}
            className="min-w-0 flex-1 basis-48 rounded-control border border-line-strong bg-raised px-2.5 py-1 text-body text-ink outline-none"
          />
          <Button variant="primary" type="submit" disabled={busy || !label.trim() || label.trim() === identity.label}>{t('settings.git.renameSave')}</Button>
          <Button variant="ghost" onClick={() => setPanel(null)}>{t('settings.git.close')}</Button>
        </form>
      )}

      {panel === 'commits' && (
        <form
          className="mt-1 flex min-w-0 flex-wrap items-end gap-2"
          onSubmit={(event) => {
            event.preventDefault();
            press(() => acts.commitsAs(identity, who), () => setPanel(null));
          }}
        >
          <CommitsFields who={who} onChange={setWho} of={identity.label} />
          <Button variant="primary" type="submit" disabled={busy || !who.name.trim() || !who.email.includes('@')}>{t('settings.git.commitsSave')}</Button>
          {(identity.commitsName || identity.commitsEmail) && (
            <Button variant="ghost" disabled={busy} onClick={() => press(() => acts.commitsAs(identity, null), () => setPanel(null))}>
              {t('settings.git.commitsClear')}
            </Button>
          )}
          <Button variant="ghost" onClick={() => setPanel(null)}>{t('settings.git.close')}</Button>
        </form>
      )}

      {panel === 'readOnlyOff' && (
        <InlineConfirm
          className="mt-1"
          tone="warn"
          label={t('settings.git.readOnlyOffTitle', { label: identity.label })}
          says={t('settings.git.readOnlyOffSays', { label: identity.label, host: identity.host })}
          meanIt={t('settings.git.readOnlyOffConfirm')}
          busy={busy}
          onConfirm={confirmed(() => acts.readOnly(identity, false))}
          onClose={() => setPanel(null)}
        />
      )}

      {panel === 'remove' && (
        <InlineConfirm
          className="mt-1"
          block
          label={t('settings.git.removeTitle', { label: identity.label })}
          says={(
            <>
              <span className="block">
                {ssh ? t('settings.git.removeSaysSsh', { label: identity.label }) : t('settings.git.removeSaysHttps', { label: identity.label })}
              </span>
              {identity.pointedAt.length > 0 && (
                <span className="mt-1 block">
                  {t('settings.git.removePointing')}
                  <ul className="m-0 mt-0.5 list-disc pl-5">
                    {identity.pointedAt.map((scope) => <li key={scope} className="wrap-anywhere">{scope}</li>)}
                  </ul>
                </span>
              )}
            </>
          )}
          meanIt={t('settings.git.removeConfirm')}
          busy={busy}
          onConfirm={confirmed(() => acts.remove(identity))}
          onClose={() => setPanel(null)}
        />
      )}
    </li>
  );
}

/** A commit author's two fields (§3.6): shown on the add form and on an identity's *Commits as…*. */
export function CommitsFields({ who, onChange, of }: {
  who: { name: string; email: string }; onChange: (who: { name: string; email: string }) => void; of: string;
}) {
  const { t } = useTranslation();
  const field = 'min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 text-body text-ink outline-none placeholder:text-ink-faint';
  return (
    <>
      <label className="grid min-w-0 flex-1 basis-40 gap-1 text-small text-ink-faint">
        {t('settings.git.authorName')}
        <input aria-label={t('settings.git.authorNameOf', { label: of })} value={who.name} className={field} onChange={(event) => onChange({ ...who, name: event.target.value })} />
      </label>
      <label className="grid min-w-0 flex-1 basis-48 gap-1 text-small text-ink-faint">
        {t('settings.git.authorEmail')}
        <input aria-label={t('settings.git.authorEmailOf', { label: of })} value={who.email} className={field} onChange={(event) => onChange({ ...who, email: event.target.value })} />
      </label>
    </>
  );
}

/** Settings → Git's first card (§7.1): the identities and the press that adds one. */
export function GitIdentities({ identities, acts, adding, onAdd }: {
  identities: GitIdentityShown[];
  acts: GitActs;
  adding: boolean;
  onAdd: () => void;
}) {
  const { t } = useTranslation();
  return (
    <Card id="settings-git-identities" className="scroll-mt-3">
      <CardHeader
        title={t('settings.git.identities')}
        aside={!adding && <Button variant="primary" onClick={onAdd}>{t('settings.git.add')}</Button>}
      />
      {identities.length === 0
        ? (
          <EmptyState
            icon="account"
            headline={t('settings.git.empty.headline')}
            body={t('settings.git.empty.body')}
            action={!adding && <Button variant="primary" onClick={onAdd}>{t('settings.git.add')}</Button>}
          />
        )
        : (
          <ul className="m-0 mt-2 grid list-none gap-0 p-0">
            {identities.map((identity) => <GitIdentityRow key={identity.id} identity={identity} acts={acts} />)}
          </ul>
        )}
    </Card>
  );
}

/** A host's choice of default, per way (§7.1): the identities of that host and way, or none. */
export function GitDefaults({ identities, acts, onError }: {
  identities: GitIdentityShown[]; acts: GitActs; onError: (sentence: string) => void;
}) {
  const { t } = useTranslation();
  const rows = (['ssh', 'https'] as const).flatMap((way) => [...new Set(identities.filter((each) => each.way === way).map((each) => each.host))]
    .sort().map((host) => ({ way, host, ofHost: identities.filter((each) => each.host === host && each.way === way) })));
  if (rows.length === 0) return null;
  return (
    <Card id="settings-git-defaults" className="scroll-mt-3">
      <CardHeader title={t('settings.git.defaults')} />
      <Prose className="mt-1 text-small">{t('settings.git.defaultsWhy')}</Prose>
      <SettingList className="mt-2">
        {rows.map(({ way, host, ofHost }) => {
          const current = ofHost.find((each) => each.default)?.id ?? '';
          return (
            <SettingRow
              key={`${way}:${host}`}
              label={t(`settings.git.defaultOf.${way}`, { host })}
              hint={`\`daoris-driver git default ${host} <identity>\``}
              control={(
                <SelectField
                  ariaLabel={t('settings.git.defaultAria', { host, way: t(`settings.git.way.${way}`) })}
                  value={current}
                  placeholder={t('settings.git.defaultNone')}
                  options={ofHost.map((each) => ({ value: each.id, label: each.label }))}
                  onChange={(id) => {
                    const chosen = ofHost.find((each) => each.id === id);
                    if (chosen && !chosen.default) acts.makeDefault(chosen).catch((error) => onError(sentence(error)));
                  }}
                />
              )}
            />
          );
        })}
      </SettingList>
    </Card>
  );
}

/** *Your own git sign-ins* (§3.3, §7.1): where Daoris's git may use the person's own keys, configuration and store; choosing is a widening. */
export function GitOwn({ hosts, acts, knownHosts }: { hosts: string[]; acts: GitActs; knownHosts: string[] }) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(false);
  const [host, setHost] = useState('');
  const [refusal, setRefusal] = useState<string | null>(null);
  const stop = (each: string) => acts.own(each, false).catch((error) => setRefusal(sentence(error)));
  return (
    <Card id="settings-git-own" className="scroll-mt-3">
      <CardHeader
        title={t('settings.git.own')}
        aside={hosts.length === 0 ? <Pill>{t('settings.git.ownNotUsed')}</Pill> : <Pill tone="taken">{t('settings.git.ownUsed')}</Pill>}
      />
      <Prose className="mt-1 text-small">{t('settings.git.ownWhy')}</Prose>
      {hosts.length > 0 && (
        <ul className="m-0 mt-2 grid list-none gap-1 p-0">
          {hosts.map((each) => (
            <li key={each} className="flex min-w-0 flex-wrap items-center gap-2">
              <span className="wrap-anywhere text-body text-ink">{each}</span>
              <Button variant="ghost" onClick={() => stop(each)}>{t('settings.git.ownStop')}</Button>
            </li>
          ))}
        </ul>
      )}
      {refusal && <Refused sentence={refusal} className="mt-2" />}
      {!asking
        ? <div className="mt-2"><Button onClick={() => setAsking(true)}>{t('settings.git.ownUse')}</Button></div>
        : (
          <InlineConfirm
            className="mt-2"
            tone="warn"
            label={t('settings.git.ownTitle')}
            says={t('settings.git.ownSays', { host: host.trim() || t('settings.git.ownThisHost') })}
            meanIt={t('settings.git.ownConfirm')}
            ready={host.trim().length > 0}
            onConfirm={(answered) => {
              acts.own(host.trim(), true).then(() => { setAsking(false); setHost(''); answered.done(); }, (error) => answered.refused(sentence(error)));
            }}
            onClose={() => setAsking(false)}
          >
            <SelectOrType value={host} onChange={setHost} options={knownHosts.filter((each) => !hosts.includes(each))} />
          </InlineConfirm>
        )}
    </Card>
  );
}

/** A host: one the machine knows, or any typed (*Another host…*). */
export function SelectOrType({ value, onChange, options, label }: {
  value: string; onChange: (host: string) => void; options: string[]; label?: string;
}) {
  const { t } = useTranslation();
  const list = `git-hosts-${label ?? 'host'}`;
  return (
    <>
      <input
        list={list}
        aria-label={label ?? t('settings.git.hostAria')}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={t('settings.git.hostPlaceholder')}
        spellCheck={false}
        className="min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink outline-none placeholder:text-ink-faint"
      />
      <datalist id={list}>
        {[...new Set(['github.com', 'gitlab.com', 'dev.azure.com', ...options])].map((each) => <option key={each} value={each} />)}
      </datalist>
    </>
  );
}

/** The public key the host is given (§7.2 step 3), with its copy and its page. */
export function PublicKey({ identity, acts }: { identity: GitAdded; acts: Pick<GitActs, 'copy'> }) {
  const { t } = useTranslation();
  if (!identity.publicKey) return null;
  return (
    <div className="grid gap-1.5">
      <p className="m-0 text-small text-ink-soft"><Inline text={t('settings.git.keyGive', { host: identity.host })} /></p>
      <MonoWell text={identity.publicKey} label={t('settings.git.publicKey')} />
      <div><Button onClick={() => acts.copy(identity.publicKey!)}>{t('settings.git.copyKey')}</Button></div>
    </div>
  );
}
