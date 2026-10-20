import { type FormEvent, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { ExternalLink } from '../design/links';
import { PhaseList, type PhaseRow, Remaining } from '../design/phases';
import { Button, Icon } from '../design/ui';

/** A phase the driver wrote with its start (the relay's phase lines, SIGNIN1). */
export type SignInPhase = { phase: string; at: number };

/**
 * A sign-in's way (CODEXLOGIN1, D125's note): the tool's own, which for Codex opens the system's browser, offered first; or
 * its device code. `HARNESS_ACTION`'s `way`; the terminal's `--device-code`.
 */
export type SignInWay = 'browser' | 'device-code';

/** The driver's phases of a sign-in, in the order it runs them (the UI/UX design §3.2). */
const ORDER = ['start', 'browser', 'who', 'lists'] as const;

/** What the person can do with what the tool printed, held by the organism above. */
export type SignInActs = {
  copied: 'link' | 'code' | null;
  onCopy: (what: 'link' | 'code', text: string) => void;
  code: string;
  onCode: (code: string) => void;
  onSend: (event: FormEvent) => void;
  sending: boolean;
  sent: boolean;
  onRetry?: () => void;
  onClose?: () => void;
  /**
   * The other way this sign-in's agent has (CODEXLOGIN1), and the press that stops the one running and starts it; absent where
   * the agent has one way, or from a surface that holds no running sign-in.
   */
  other?: SignInWay | null;
  onSwitch?: () => void;
};

/**
 * A sign-in's steps (SIGNIN1, the UI/UX design §3.2): from the driver's phases, `PhaseList` standing open, or as the line
 * alone under a row of *What needs you*; from an older host that writes none, the steps as they were. The link, the code
 * and the ask are read from the tool's lines by their shape, the fallback R2 keeps, and never make a phase.
 *
 * @remarks
 * A molecule: every state is its props, so each has a story (`SignInSteps.stories.tsx`).
 */
export function SignInSteps({
  title, tool, action, phases, texts, refused = null, failed = null, failedAt = null, shown = 'list', acts,
}: {
  /** The panel's title, which names its list of phases. */
  title: string;
  /** What a person calls the tool. */
  tool: string;
  action: 'login' | 'login-new';
  /** The phases the driver wrote, oldest first: the latest run's are read, after its own start. */
  phases: SignInPhase[];
  /** The tool's own lines, the phases left out. */
  texts: string[];
  /** The driver's sentence where it refused to start (CODEXSIGNIN2): the first phase failed. */
  refused?: string | null;
  /** The sentence of a sign-in that ended without signing in, and when: the phase it was in failed. */
  failed?: string | null;
  failedAt?: number | null;
  shown?: 'list' | 'line';
  acts: SignInActs;
}) {
  const { t } = useTranslation();
  const run = latestRun(phases);
  const read = readTool(texts);
  const ended = refused || failed;
  // The other way (CODEXLOGIN1): offered while one waits, and first, focused, once one failed or was refused; never once the
  // tool has signed someone in and the driver's own phases run.
  const finishing = !ended && ['who', 'lists'].includes(run[run.length - 1]?.phase ?? '');
  const other = acts.other && acts.onSwitch && !finishing ? acts.other : null;
  const switchTo = other ? (
    <Button variant={ended ? 'primary' : 'ghost'} autoFocus={Boolean(ended)} onClick={acts.onSwitch}>
      {t(other === 'device-code' ? 'signin.switch.device' : 'signin.switch.browser')}
    </Button>
  ) : null;

  const ways = ended ? (
    <>
      {failed && read.said && <p className="m-0 mt-2 break-words text-small text-ink-soft">{t('signin.toolSaid', { tool, line: read.said })}</p>}
      <div className="mt-2 flex flex-wrap items-center gap-2">
        {switchTo}
        <Button variant={other ? 'ghost' : 'primary'} autoFocus={!other} onClick={acts.onRetry}>{t('signin.retry')}</Button>
        <Button variant="ghost" onClick={acts.onClose}>{t('signin.close')}</Button>
      </div>
    </>
  ) : switchTo ? (
    <div className="mt-2 flex flex-wrap items-center gap-2">
      {switchTo}
      {other === 'browser' && <span className="text-meta text-ink-faint">{t('signin.switch.deviceHint')}</span>}
    </div>
  ) : null;

  if (run.length === 0 && !refused) {
    return failed
      ? <><p role="alert" className="m-0 mt-2 text-small text-ink-danger">{failed}</p>{ways}</>
      : <><Steps action={action} read={read} acts={acts} />{ways}</>;
  }

  const labels: Record<(typeof ORDER)[number], string> = {
    start: t('signin.phase.start', { tool }),
    browser: t('signin.phase.browser'),
    who: t('signin.phase.who'),
    lists: t('signin.phase.lists'),
  };
  const at = (name: string) => run.find((each) => each.phase === name)?.at ?? null;
  const current = refused ? 0 : Math.max(0, ORDER.indexOf(run[run.length - 1]!.phase as (typeof ORDER)[number]));
  const rows: PhaseRow[] = ORDER.map((name, index) => {
    const from = at(name);
    if (index < current) return { key: name, label: labels[name], state: 'done', from, to: at(ORDER[index + 1]!) };
    if (index > current) return { key: name, label: labels[name], state: 'to-come' };
    if (ended) return { key: name, label: labels[name], state: 'failed', from, to: failedAt, sentence: refused ?? failed };
    return {
      key: name, label: labels[name], state: name === 'browser' ? 'waits' : 'now', from,
      body: name === 'browser' ? <Browser read={read} since={from} acts={acts} /> : undefined,
    };
  });

  return (
    <div className="mt-2">
      <PhaseList phases={rows} label={title} shown={shown} />
      {ways}
    </div>
  );
}

/** The latest run's phases: those from its last start on, since a panel that stayed up through *Try again* holds both. */
function latestRun(phases: SignInPhase[]): SignInPhase[] {
  const known = phases.filter((each) => (ORDER as readonly string[]).includes(each.phase));
  const start = known.map((each) => each.phase).lastIndexOf('start');
  return start < 0 ? known : known.slice(start);
}

/** What the tool has printed, read by shape for the person's steps. */
type ToolRead = {
  url: string | null;
  opened: boolean;
  asked: boolean;
  device: string | null;
  /** The minutes the tool gave its code, read from a line about it; null where it said none. */
  minutes: number | null;
  /** The tool's own line saying why it did not sign in (`Error logging in: …`), the last such; null where it said none. */
  said: string | null;
};

/**
 * A link on a line, never one to this machine's own address (CODEXLOGIN1): `codex login` says where its local server listens
 * (`http://localhost:1455`) before the page to open, and that address is the tool's, never a page for the person.
 */
function linkOf(text: string): string | null {
  return text.match(/https?:\/\/\S+/g)?.find((link) => !/^https?:\/\/(?:localhost|127\.0\.0\.1|\[::1\])(?:[:/?#.]|$)/i.test(link)) ?? null;
}

/**
 * What the tool's lines tell the steps: where to sign in, whether it opened a browser or asks for a code, a device code with
 * its bound, and its own line saying why it did not sign in. The tool's own words, matched loosely, since the exact sentence is
 * somebody else's to change: Codex 0.160.0 says *If your browser did not open* once it opened one, and ends a refused sign-in
 * with *Error logging in: …* or *Error logging in with device code: …* (D125's CODEXLOGIN1 note).
 */
function readTool(texts: readonly string[]): ToolRead {
  const url = texts.map(linkOf).find(Boolean) ?? null;
  const device = deviceCode(texts);
  const linked = texts.findIndex((text) => linkOf(text) !== null);
  const bound = device && linked >= 0
    ? texts.slice(linked).map((text) => text.match(/\b(\d{1,3})\s*min(?:ute)?s?\b/i)?.[1]).find(Boolean)
    : undefined;
  return {
    url,
    opened: texts.some((text) => /opening.*browser|browser did not open/i.test(text)),
    asked: texts.some((text) => /paste.*code/i.test(text)),
    device,
    minutes: bound ? Number(bound) : null,
    said: [...texts].reverse().find((text) => /^\s*error\b[^:]*:\s*\S/i.test(text))?.trim() ?? null,
  };
}

/** The link, offered to open in the system's browser and to copy (BRW7: a sign-in never opens in Daoris's own). */
function Link({ url, acts, hint = true }: { url: string; acts: SignInActs; hint?: boolean }) {
  const { t } = useTranslation();
  return (
    <div className="mt-1 flex min-w-0 flex-wrap items-center gap-2">
      <ExternalLink
        href={url}
        system
        className="inline-flex max-w-full items-center gap-1 truncate font-mono text-small text-accent underline-offset-2 hover:underline"
      >
        <Icon name="external" size={12} />
        <span className="truncate">{url}</span>
      </ExternalLink>
      <Button variant="ghost" onClick={() => acts.onCopy('link', url)}>
        <Icon name="copy" size={12} />
        {acts.copied === 'link' ? t('signin.copied') : t('signin.copy')}
      </Button>
      {hint && <span className="text-meta text-ink-faint">{t('signin.step.openHint')}</span>}
    </div>
  );
}

/** The box a tool's ask for a code is answered in, focused; then that it was sent. */
function CodeBox({ acts }: { acts: SignInActs }) {
  const { t } = useTranslation();
  if (acts.sent) return <p className="m-0 mt-0.5 text-small text-ink-soft">{t('signin.sent')}</p>;
  return (
    <form className="mt-1 flex flex-wrap items-center gap-2" onSubmit={acts.onSend}>
      <input
        autoFocus
        value={acts.code}
        onChange={(event) => acts.onCode(event.target.value)}
        aria-label={t('signin.codeLabel')}
        placeholder={t('signin.codePlaceholder')}
        spellCheck={false}
        className="min-w-56 flex-1 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink outline-none placeholder:text-ink-faint"
      />
      <Button type="submit" variant="primary" disabled={acts.sending || !acts.code.trim()}>
        {t('signin.continue')}
      </Button>
    </form>
  );
}

/** What the browser's phase says: waiting for the page, the link, a device code with its bound, or the box the tool asks for. */
function Browser({ read, since, acts }: { read: ToolRead; since: number | null; acts: SignInActs }) {
  const { t } = useTranslation();
  if (read.device && !read.asked) {
    return (
      <div className="min-w-0">
        <p className="m-0 text-small text-ink">{t('signin.device.enter')}</p>
        <div className="mt-1 flex flex-wrap items-center gap-2">
          <span className="select-all font-mono text-body font-semibold text-ink">{read.device}</span>
          <Button variant="ghost" onClick={() => acts.onCopy('code', read.device!)}>
            <Icon name="copy" size={12} />
            {acts.copied === 'code' ? t('signin.copied') : t('signin.copyCode')}
          </Button>
        </div>
        {read.minutes !== null && since !== null && (
          <Remaining
            until={since + read.minutes * 60_000}
            say={(time) => t('signin.device.expires', { time })}
            className="block text-meta text-ink-faint"
          />
        )}
        {read.url && <Link url={read.url} acts={acts} hint={false} />}
      </div>
    );
  }
  let shown: ReactNode = <p className="m-0 text-small text-ink-faint">{t(read.opened ? 'signin.opened' : 'signin.waiting')}</p>;
  if (read.url) shown = <>{read.opened && <p className="m-0 text-small text-ink-soft">{t('signin.opened')}</p>}<Link url={read.url} acts={acts} /></>;
  return <div className="min-w-0">{shown}{read.asked && <CodeBox acts={acts} />}</div>;
}

/**
 * The steps as they were, for an older host that writes no phases (R2: it draws as today): open the page, the code the page
 * shows or the device code to enter, and how it ends.
 */
function Steps({ action, read, acts }: { action: 'login' | 'login-new'; read: ToolRead; acts: SignInActs }) {
  const { t } = useTranslation();
  const { url, opened, asked, device } = read;
  return (
    <ol className="m-0 mt-2 grid list-none gap-2.5 p-0">
      <li className="grid grid-cols-[1.25rem_1fr] gap-x-2">
        <span className="text-small font-semibold text-ink-faint">1</span>
        <div className="min-w-0">
          <p className="m-0 text-body text-ink">{t(device ? 'signin.step.openLink' : 'signin.step.open')}</p>
          {url ? <Link url={url} acts={acts} /> : (
            <p className="m-0 mt-0.5 text-small text-ink-faint">{t(opened ? 'signin.opened' : 'signin.waiting')}</p>
          )}
        </div>
      </li>

      <li className="grid grid-cols-[1.25rem_1fr] gap-x-2">
        <span className="text-small font-semibold text-ink-faint">2</span>
        <div className="min-w-0">
          <p className="m-0 text-body text-ink">{t(device && !asked ? 'signin.step.enter' : 'signin.step.code')}</p>
          {asked ? <CodeBox acts={acts} /> : device ? (
            <div className="mt-1 flex flex-wrap items-center gap-2">
              <span className="select-all font-mono text-body font-semibold text-ink">{device}</span>
              <Button variant="ghost" onClick={() => acts.onCopy('code', device)}>
                <Icon name="copy" size={12} />
                {acts.copied === 'code' ? t('signin.copied') : t('signin.copyCode')}
              </Button>
            </div>
          ) : (
            <p className="m-0 mt-0.5 text-small text-ink-faint">{t('signin.codeLater')}</p>
          )}
        </div>
      </li>

      <li className="grid grid-cols-[1.25rem_1fr] gap-x-2">
        <span className="text-small font-semibold text-ink-faint">3</span>
        <p className="m-0 text-small text-ink-soft">
          {t(action === 'login-new' ? 'signin.readyNew' : 'signin.ready')}
        </p>
      </li>
    </ol>
  );
}

/**
 * The one-time code a tool shows for the person to enter on its page (CODEXACCT2, CODEXSIGNIN3): read by its own shape, not by
 * a version's words, since the sentence asking for it is the tool's to change. Codex prints a link to its device page and then
 * a code of capital letters and digits in groups joined by a hyphen (`ABCD-12345` in 0.160.0, on the line after
 * `2. Enter this one-time code (expires in 15 minutes)`), so the code is the first such group after the first line with a
 * link, or null where none is shown yet. A line with a link is never the code.
 */
function deviceCode(texts: readonly string[]): string | null {
  const linked = texts.findIndex((text) => linkOf(text) !== null);
  if (linked < 0) return null;
  for (const text of texts.slice(linked + 1)) {
    if (/https?:\/\/\S+/.test(text)) continue;
    const found = text.match(/(?<![\w-])[A-Z0-9]{3,}(?:-[A-Z0-9]{3,})+(?![\w-])/)?.[0];
    if (found) return found;
  }
  return null;
}
