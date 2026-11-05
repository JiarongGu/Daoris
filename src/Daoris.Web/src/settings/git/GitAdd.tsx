import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from '../../locales/format';
import { ExternalLink } from '../../design/links';
import { InlineConfirm, Refused } from '../../work/InlineConfirm';
import { Button, Card, CardHeader, Inline, Prose, Segmented, SettingList, SettingRow } from '../../design/ui';
import {
  CommitsFields, type GitActs, type GitAdded, type GitAddInput, type GitWay, PublicKey, SelectOrType,
} from './Git';

/** Azure DevOps takes RSA keys alone (§7.2 step 3), so its new key is RSA 4096 and the form says why. */
const RSA_ALONE = new Set(['dev.azure.com', 'ssh.dev.azure.com']);

const FIELD = 'min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 text-body text-ink outline-none placeholder:text-ink-faint';

/**
 * Add a git identity (GITAUTH1e, the git-auth design §7.2), a form in the pane, never a drawer: where, how, the key or the sign-in,
 * and who commits. A refusal is said under the press that started it, the form kept open with what was typed (R3). The identity is
 * saved at the press; its test is a press of its own, so a failed test leaves it with its mark.
 */
export function GitAdd({ acts, knownHosts, wayToday = 'ssh', host: initialHost = '', added = null, pick, onClose }: {
  acts: GitActs;
  knownHosts: string[];
  /** The way the repositories pointing at the host already use, first (§7.2 step 2). */
  wayToday?: GitWay;
  /** A host to open on: the sync card's door and the *What needs you* row fill it. */
  host?: string;
  /** The form already added (the story draws it). */
  added?: GitAdded | null;
  /** The system's file picker, where the window has one. */
  pick?: () => Promise<string | null>;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const [way, setWay] = useState<GitWay>(wayToday);
  const [host, setHost] = useState(initialHost);
  const [label, setLabel] = useState('');
  const [mode, setMode] = useState<'generate' | 'import'>('generate');
  const [file, setFile] = useState('');
  const [user, setUser] = useState('');
  const [token, setToken] = useState('');
  const [who, setWho] = useState({ name: '', email: '' });
  const [asking, setAsking] = useState(false);
  const [busy, setBusy] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const [done, setDone] = useState<GitAdded | null>(added);

  const bare = host.trim().toLowerCase();
  const rsa = RSA_ALONE.has(bare);
  const ssh = way === 'ssh';
  const importing = ssh && mode === 'import';
  const author = who.name.trim() || who.email.trim() ? { name: who.name.trim(), email: who.email.trim() } : null;
  const ready = bare.length > 0 && label.trim().length > 0
    && (ssh ? !importing || file.trim().length > 0 : token.length > 0 && user.trim().length > 0)
    && (!author || (author.name.length > 0 && author.email.includes('@')));

  const input = (): GitAddInput => ({
    way, host: bare, label: label.trim(),
    ...(author ? author : {}),
    ...(ssh
      ? { ...(importing ? { importFile: file.trim() } : {}), ...(rsa && !importing ? { rsa: true } : {}) }
      : { user: user.trim(), token }),
  });

  const send = async () => {
    setBusy(true);
    setRefusal(null);
    try {
      setDone(await acts.add(input()));
      setToken('');
      setAsking(false);
    } catch (error) {
      setRefusal(sentence(error));
      setAsking(false);
    } finally {
      setBusy(false);
    }
  };

  if (done) {
    return (
      <Card id="settings-git-add" className="scroll-mt-3">
        <CardHeader title={t('settings.git.addedTitle', { label: done.label })} />
        <div className="mt-2 grid gap-3">
          {done.way === 'ssh'
            ? (
              <>
                <PublicKey identity={done} acts={acts} />
                <p className="m-0 text-small text-ink-soft">
                  {done.host === 'github.com' || done.host === 'gitlab.com'
                    ? <ExternalLink href={`https://${done.host}/${done.host === 'github.com' ? 'settings/keys' : '-/user_settings/ssh_keys'}`} system>{t('settings.git.keyPage', { host: done.host })}</ExternalLink>
                    : <Inline text={t('settings.git.keyElsewhere', { host: done.host })} />}
                </p>
              </>
            )
            : <Prose className="text-small"><Inline text={t('settings.git.httpsAdded', { host: done.host })} /></Prose>}
          <p className="m-0 text-small text-ink-soft"><Inline text={t('settings.git.thenTest')} /></p>
          <div><Button variant="primary" onClick={onClose}>{t('settings.git.close')}</Button></div>
        </div>
      </Card>
    );
  }

  return (
    <Card id="settings-git-add" className="scroll-mt-3">
      <CardHeader title={t('settings.git.add')} />
      <form
        onSubmit={(event) => {
          event.preventDefault();
          if (!ready || busy) return;
          if (importing) setAsking(true);
          else void send();
        }}
      >
        <SettingList className="mt-2">
          <SettingRow
            label={t('settings.git.where')}
            hint={t('settings.git.whereHint')}
            control={(
              <>
                <SelectOrType value={host} onChange={setHost} options={knownHosts} label={t('settings.git.hostAria')} />
                <input
                  aria-label={t('settings.git.labelAria')}
                  value={label}
                  onChange={(event) => setLabel(event.target.value)}
                  placeholder={t('settings.git.labelPlaceholder')}
                  className={`${FIELD} basis-40`}
                />
              </>
            )}
          />

          <SettingRow
            label={t('settings.git.how')}
            control={(
              <Segmented
                label={t('settings.git.how')}
                value={way}
                options={[
                  { value: 'ssh', label: t('settings.git.way.ssh') },
                  { value: 'https', label: t('settings.git.way.https') },
                ]}
                onChange={setWay}
              />
            )}
          />

          {ssh && (
            <SettingRow
              label={t('settings.git.key')}
              hint={rsa ? t('settings.git.rsaWhy') : t('settings.git.keyHint')}
              control={(
                <Segmented
                  label={t('settings.git.key')}
                  value={mode}
                  options={[
                    { value: 'generate', label: t('settings.git.keyGenerate') },
                    { value: 'import', label: t('settings.git.keyImport') },
                  ]}
                  onChange={setMode}
                />
              )}
            >
              {importing && (
                <div className="mt-1 flex min-w-0 flex-wrap items-center gap-2">
                  <input
                    aria-label={t('settings.git.importAria')}
                    value={file}
                    onChange={(event) => setFile(event.target.value)}
                    placeholder={t('settings.git.importPlaceholder')}
                    spellCheck={false}
                    className={`${FIELD} flex-1 basis-64 font-mono text-small`}
                  />
                  {pick && (
                    <Button
                      variant="ghost"
                      onClick={() => { void pick().then((chosen) => { if (chosen) setFile(chosen); }); }}
                    >
                      {t('settings.git.browse')}
                    </Button>
                  )}
                  <p className="m-0 basis-full text-small text-ink-faint"><Inline text={t('settings.git.importWhy')} /></p>
                </div>
              )}
            </SettingRow>
          )}

          {!ssh && (
            <SettingRow label={t('settings.git.signIn')} hint={t('settings.git.signInHint')}>
              <div className="mt-1 flex min-w-0 flex-wrap items-center gap-2">
                <input
                  aria-label={t('settings.git.userAria')}
                  value={user}
                  onChange={(event) => setUser(event.target.value)}
                  placeholder={t('settings.git.userPlaceholder')}
                  autoComplete="off"
                  className={`${FIELD} basis-40`}
                />
                <input
                  type="password"
                  aria-label={t('settings.git.tokenAria')}
                  value={token}
                  onChange={(event) => setToken(event.target.value)}
                  placeholder={t('settings.git.tokenPlaceholder')}
                  autoComplete="off"
                  className={`${FIELD} flex-1 basis-56`}
                />
                <p className="m-0 basis-full text-small text-ink-faint"><Inline text={t('settings.git.tokenWhy')} /></p>
              </div>
            </SettingRow>
          )}

          <SettingRow label={t('settings.git.commitsAsRow')} hint={t('settings.git.commitsAsHint')}>
            <div className="flex min-w-0 flex-wrap items-end gap-2">
              <CommitsFields who={who} onChange={setWho} of={t('settings.git.add')} />
            </div>
          </SettingRow>
        </SettingList>

        {refusal && <Refused sentence={refusal} className="mt-2" />}
        {asking && (
          <InlineConfirm
            className="mt-2"
            tone="warn"
            label={t('settings.git.importTitle')}
            says={t('settings.git.importSays')}
            meanIt={t('settings.git.importConfirm')}
            busy={busy}
            onConfirm={() => { void send(); }}
            onClose={() => setAsking(false)}
          />
        )}
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <Button variant="primary" type="submit" disabled={!ready || busy || asking}>
            {t('settings.git.addPress')}
          </Button>
          <Button variant="ghost" onClick={onClose}>{t('settings.git.cancel')}</Button>
        </div>
      </form>
    </Card>
  );
}
