import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from '../../locales/format';
import { InlineConfirm, Refused } from '../../work/InlineConfirm';
import { Button, Inline, Pill, SelectField } from '../../design/ui';
import type { GitIdentityShown } from './Git';

/** One git remote of a repository, with what serves it (GITAUTH1e, the git-auth design §7.3), as `GIT_BRANCHES`' `remotes` answers it. */
export type GitRemoteShown = {
  name: string;
  /** The effective address with no user-info (§4.4, §10). */
  address: string;
  /** Null for a local path, which needs no identity. */
  host?: string | null;
  way?: 'ssh' | 'https' | null;
  serves: 'identity' | 'own' | 'none' | 'local';
  identity?: string | null;
  label?: string | null;
  /** Where the answer came from: `repository`, `workspace`, `machine` or `default`. */
  source?: string | null;
  passed: string[];
  /** The repository's own pointer for this remote: an identity's id, `own`, or none. */
  chosen?: string | null;
};

/** The presses of a remote's row: choose an identity, `own` or none for it, and the door to Settings → Git. */
export type GitRemoteActs = {
  /** `value` an identity's id or `own`; null clears the repository's choice, so the workspace's or the host's default serves. */
  use: (remote: GitRemoteShown, value: string | null) => Promise<unknown>;
  /** The door to Settings → Git; absent where the page has none, and the press is not drawn. */
  onSetUp?: (host: string) => void;
};

const OWN = 'own';
const AS_ABOVE = '';

/** What serves a remote, as the row says it: the identity and its way, the person's own sign-ins, or nothing in the danger ink. */
function Serving({ remote, onSetUp }: { remote: GitRemoteShown; onSetUp?: (host: string) => void }) {
  const { t } = useTranslation();
  if (remote.serves === 'local') return <span className="text-small text-ink-faint">{t('settings.git.remotes.local')}</span>;
  if (remote.serves === 'none') {
    return (
      <span className="flex min-w-0 flex-wrap items-center gap-2">
        <span className="text-small text-ink-danger">{t('settings.git.none')}</span>
        {remote.host && onSetUp && <Button variant="ghost" onClick={() => onSetUp(remote.host!)}>{t('settings.git.setOneUp')}</Button>}
      </span>
    );
  }
  const who = remote.serves === 'own' ? t('settings.git.own') : remote.label ?? '';
  const source = remote.source ? t(`settings.git.remotes.from.${remote.source}`, { host: remote.host ?? '' }) : '';
  return (
    <span className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-small text-ink-soft">
      <span className="wrap-anywhere">
        {t('settings.git.remotes.via', { who, way: remote.way === 'https' ? 'HTTPS' : 'SSH', source })}
      </span>
      {remote.serves === 'own' && <Pill tone="taken">{t('settings.git.remotes.ownMark')}</Pill>}
    </span>
  );
}

/**
 * A repository's *Git remote* (§7.3), a row per remote: its address as the reader gives it, the identity it resolves to with its
 * source, and *Change…*. Choosing the person's own sign-ins is a widening, confirmed in place; a remote nothing serves says so in the
 * danger ink with the door to set one up.
 */
export function GitRemotes({ remotes, identities, acts }: { remotes: GitRemoteShown[]; identities: GitIdentityShown[]; acts: GitRemoteActs }) {
  const { t } = useTranslation();
  const [changing, setChanging] = useState<string | null>(null);
  const [choice, setChoice] = useState<string>(AS_ABOVE);
  const [asking, setAsking] = useState(false);
  const [busy, setBusy] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);

  if (remotes.length === 0) return <p className="m-0 text-small text-ink-faint">{t('settings.git.remotes.none')}</p>;

  const open = (remote: GitRemoteShown) => {
    setChanging(remote.name);
    setChoice(remote.chosen ?? AS_ABOVE);
    setAsking(false);
    setRefusal(null);
  };
  const close = () => { setChanging(null); setAsking(false); setRefusal(null); };
  const save = async (remote: GitRemoteShown, answered?: { done: () => void; refused: (said: string) => void }) => {
    setBusy(true);
    setRefusal(null);
    try {
      await acts.use(remote, choice === AS_ABOVE ? null : choice);
      close();
      answered?.done();
    } catch (error) {
      if (answered) answered.refused(sentence(error));
      else setRefusal(sentence(error));
    } finally {
      setBusy(false);
    }
  };

  return (
    <ul className="m-0 grid list-none gap-3 p-0" data-git-remotes="">
      {remotes.map((remote) => {
        const options = [
          { value: AS_ABOVE, label: t('settings.git.remotes.asWorkspace') },
          ...identities.filter((each) => each.host === remote.host && each.way === remote.way)
            .map((each) => ({ value: each.id, label: each.label })),
          { value: OWN, label: t('settings.git.remotes.ownOption') },
        ];
        return (
          <li key={remote.name} className="grid min-w-0 gap-1" data-git-remote={remote.name}>
            <div className="flex min-w-0 flex-wrap items-baseline gap-x-3 gap-y-0.5">
              <span className="text-body font-semibold text-ink">{remote.name}</span>
              <span className="min-w-0 wrap-anywhere font-mono text-small text-ink-soft">{remote.address}</span>
            </div>
            <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1">
              <Serving remote={remote} onSetUp={acts.onSetUp} />
              {remote.serves !== 'local' && changing !== remote.name && (
                <Button variant="ghost" onClick={() => open(remote)}>{t('settings.git.choose')}</Button>
              )}
            </div>
            {remote.passed.map((why) => (
              <p key={why} className="m-0 text-small text-ink-faint"><Inline text={t('settings.git.remotes.passed', { why })} /></p>
            ))}
            {changing === remote.name && (
              <div className="grid gap-1.5 border-l-[3px] border-line pl-2.5">
                <div className="flex min-w-0 flex-wrap items-center gap-2">
                  <SelectField
                    ariaLabel={t('settings.git.remotes.choose', { remote: remote.name })}
                    value={choice}
                    options={options}
                    onChange={(next) => { setChoice(next); setAsking(next === OWN); }}
                  />
                  <Button variant="primary" disabled={busy || asking || choice === (remote.chosen ?? AS_ABOVE)} onClick={() => { void save(remote); }}>
                    {t('settings.git.remotes.save')}
                  </Button>
                  <Button variant="ghost" onClick={close}>{t('settings.git.close')}</Button>
                </div>
                {refusal && <Refused sentence={refusal} />}
                {asking && (
                  <InlineConfirm
                    tone="warn"
                    label={t('settings.git.remotes.ownTitle', { remote: remote.name })}
                    says={t('settings.git.remotes.ownSays', { remote: remote.name })}
                    meanIt={t('settings.git.remotes.ownConfirm')}
                    busy={busy}
                    onConfirm={(answered) => { void save(remote, answered); }}
                    onClose={() => setAsking(false)}
                  />
                )}
              </div>
            )}
          </li>
        );
      })}
    </ul>
  );
}

/** A workspace's *Git* (§7.3): a row per host its repositories reach, with the identity its workspace chooses for it. */
export type GitHostShown = { host: string; way: 'ssh' | 'https'; chosen?: string | null; remotes: string[] };

/** The presses of a workspace's host row. */
export type GitHostActs = { use: (host: string, value: string | null) => Promise<unknown>; onSetUp?: (host: string) => void };

export function GitHosts({ hosts, identities, acts }: { hosts: GitHostShown[]; identities: GitIdentityShown[]; acts: GitHostActs }) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState<string | null>(null);
  const [refusal, setRefusal] = useState<string | null>(null);

  if (hosts.length === 0) return <p className="m-0 text-small text-ink-faint">{t('settings.git.workspace.none')}</p>;

  const choose = (each: GitHostShown, value: string) => {
    setRefusal(null);
    if (value === OWN) { setAsking(each.host); return; }
    acts.use(each.host, value === AS_ABOVE ? null : value).catch((error) => setRefusal(sentence(error)));
  };

  return (
    <div className="grid gap-3">
      <ul className="m-0 grid list-none gap-3 p-0" data-git-hosts="">
        {hosts.map((each) => {
          const options = [
            { value: AS_ABOVE, label: t('settings.git.workspace.default') },
            ...identities.filter((identity) => identity.host === each.host && identity.way === each.way)
              .map((identity) => ({ value: identity.id, label: identity.label })),
            { value: OWN, label: t('settings.git.remotes.ownOption') },
          ];
          return (
            <li key={`${each.way}:${each.host}`} className="grid min-w-0 gap-1" data-git-host={each.host}>
              <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1">
                <span className="text-body font-semibold text-ink">{each.host} · {each.way === 'https' ? 'HTTPS' : 'SSH'}</span>
                <SelectField
                  ariaLabel={t('settings.git.workspace.choose', { host: each.host })}
                  value={each.chosen ?? AS_ABOVE}
                  options={options}
                  onChange={(value) => choose(each, value)}
                />
                {acts.onSetUp && identities.every((identity) => identity.host !== each.host || identity.way !== each.way) && (
                  <Button variant="ghost" onClick={() => acts.onSetUp?.(each.host)}>{t('settings.git.setOneUp')}</Button>
                )}
              </div>
              <p className="m-0 min-w-0 wrap-anywhere text-small text-ink-faint">{each.remotes.join(', ')}</p>
            </li>
          );
        })}
      </ul>
      {refusal && <Refused sentence={refusal} />}
      {asking && (
        <InlineConfirm
          tone="warn"
          label={t('settings.git.ownTitle')}
          says={t('settings.git.workspace.ownSays', { host: asking })}
          meanIt={t('settings.git.ownConfirm')}
          onConfirm={(answered) => {
            acts.use(asking, OWN).then(() => { setAsking(null); answered.done(); }, (error) => answered.refused(sentence(error)));
          }}
          onClose={() => setAsking(null)}
        />
      )}
    </div>
  );
}
