import { type FormEvent, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useHarnessCancel, useHarnessInput, useSessionConsole } from './shell';
import { ExternalLink } from './links';
import { Button, Dot, Icon, MonoWell } from './ui';

/**
 * Signing an account in, on the row that asked for it (2026-09-23).
 *
 * @remarks
 * 🔴 The whole sign-in, before, during and after, written from measuring the real flow with no
 * console attached.
 * The harness prints a sign-in link wrapped in a terminal hyperlink escape, then `Paste code here
 * if prompted >` with no newline, and waits on stdin. Before this the output streamed under the
 * DOOR, a card and a half below the button pressed; the link arrived twice around a scatter of
 * brackets; the prompt never arrived at all, and nothing could have answered it — so the flow could
 * not complete, and every control on the page stayed disabled until the window closed.
 *
 * Three steps, in the order the person meets them, each stated once: open the page (the link is
 * offered here, because a windowless child's "open the browser" is a hope rather than a fact),
 * paste the code the page shows, and the result — which is the row's own pill, not a second
 * sentence here. The tool's own output is one disclosure away and never the surface, the same rule
 * the setting rows follow for their why.
 *
 * An organism: it holds the console hook ONCE — the steps read the stream and the disclosure shows
 * it, from one subscription rather than two — and the two mutations, and renders only where a shell
 * is attached, because only a driver has a process to answer.
 *
 * `login-new` is signing in to ANOTHER account (D66 §3): the same three steps, with no account to
 * name yet — who it is, is what the sign-in finds out.
 *
 * A device code (CODEXACCT2) runs the second step the other way: Codex signs in by `--device-auth`, since its browser
 * sign-in waits on a local port that Windows reserves on some machines. It opens no page and asks nothing on its
 * input; it prints a link and, on the line after the sentence asking for it, a one-time code the person enters on that
 * page. So the first step says to open the link, and the second shows the code to copy, as the link is copied.
 */
export function SignIn({
  id, harness, profile, action = 'login', tool,
}: {
  id: string;
  harness: string;
  profile?: string;
  action?: 'login' | 'login-new';
  /** What a person calls the tool (AGT1) — the id when it does not say. */
  tool?: string;
}) {
  const { t } = useTranslation();
  const { lines, live, dropped } = useSessionConsole(id);
  const input = useHarnessInput();
  const cancel = useHarnessCancel();
  const [code, setCode] = useState('');
  const [copied, setCopied] = useState<'link' | 'code' | null>(null);

  // What the tool has said so far, read for the two facts the steps need: where to sign in, and
  // whether it has asked for the code yet. Both are the harness's own words, matched loosely —
  // the exact sentence is somebody else's to change.
  // Measured twice on the real binary: with no stdin it prints the link and asks for the code;
  // with a stdin it says only "Opening browser…" and waits for the browser to call back. Both are
  // one flow to the person, so each step says what has actually happened.
  const url = lines.map((line) => line.text.match(/https?:\/\/\S+/)?.[0]).find(Boolean);
  const opened = lines.some((line) => /opening.*browser/i.test(line.text));
  const asked = lines.some((line) => /paste.*code/i.test(line.text));
  const device = deviceCode(lines.map((line) => line.text));
  const sent = input.isSuccess;

  const copy = async (what: 'link' | 'code', text: string | null | undefined) => {
    if (!text) return;
    try {
      await navigator.clipboard.writeText(text);
      setCopied(what);
      window.setTimeout(() => setCopied(null), 1500);
    } catch {
      // No clipboard here: the link and the code are on the page to select by hand.
    }
  };

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const text = code.trim();
    if (!text) return;
    input.mutate({ harness, action, text });
  };

  const title = profile ? t('signin.title', { profile }) : t('signin.titleNew', { harness: tool ?? harness });

  return (
    <section
      aria-label={title}
      className="mt-2 basis-full rounded-card border border-line bg-raised p-3"
    >
      <header className="flex items-center gap-2">
        <Dot tone="live" label={t('console.live')} />
        <span className="text-body font-semibold text-ink">{title}</span>
        <Button
          variant="ghost"
          className="ml-auto"
          disabled={cancel.isPending}
          onClick={() => cancel.mutate({ harness, action })}
        >
          <Icon name="x" size={13} />
          {t('signin.cancel')}
        </Button>
      </header>

      <ol className="m-0 mt-2 grid list-none gap-2.5 p-0">
        <li className="grid grid-cols-[1.25rem_1fr] gap-x-2">
          <span className="text-small font-semibold text-ink-faint">1</span>
          <div className="min-w-0">
            <p className="m-0 text-body text-ink">{t(device ? 'signin.step.openLink' : 'signin.step.open')}</p>
            {url ? (
              <div className="mt-1 flex flex-wrap items-center gap-2">
                {/* Always the system's browser, whatever links are set to (BRW7): an account's sign-in is
                    the person's own, and never belongs in a browser any process here can drive. */}
                <ExternalLink
                  href={url}
                  system
                  className="inline-flex max-w-full items-center gap-1 truncate font-mono text-small text-accent underline-offset-2 hover:underline"
                >
                  <Icon name="external" size={12} />
                  <span className="truncate">{url}</span>
                </ExternalLink>
                <Button variant="ghost" onClick={() => copy('link', url)}>
                  <Icon name="copy" size={12} />
                  {copied === 'link' ? t('signin.copied') : t('signin.copy')}
                </Button>
                <span className="text-meta text-ink-faint">{t('signin.step.openHint')}</span>
              </div>
            ) : (
              <p className="m-0 mt-0.5 text-small text-ink-faint">
                {t(opened ? 'signin.opened' : 'signin.waiting')}
              </p>
            )}
          </div>
        </li>

        <li className="grid grid-cols-[1.25rem_1fr] gap-x-2">
          <span className="text-small font-semibold text-ink-faint">2</span>
          <div className="min-w-0">
            <p className="m-0 text-body text-ink">{t(device && !asked ? 'signin.step.enter' : 'signin.step.code')}</p>
            {asked ? (
              sent ? (
                <p className="m-0 mt-0.5 text-small text-ink-soft">{t('signin.sent')}</p>
              ) : (
                <form className="mt-1 flex flex-wrap items-center gap-2" onSubmit={submit}>
                  <input
                    autoFocus
                    value={code}
                    onChange={(event) => setCode(event.target.value)}
                    aria-label={t('signin.codeLabel')}
                    placeholder={t('signin.codePlaceholder')}
                    spellCheck={false}
                    className="min-w-56 flex-1 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink outline-none placeholder:text-ink-faint"
                  />
                  <Button type="submit" variant="primary" disabled={input.isPending || !code.trim()}>
                    {t('signin.continue')}
                  </Button>
                </form>
              )
            ) : device ? (
              <div className="mt-1 flex flex-wrap items-center gap-2">
                <span className="select-all font-mono text-body font-semibold text-ink">{device}</span>
                <Button variant="ghost" onClick={() => copy('code', device)}>
                  <Icon name="copy" size={12} />
                  {copied === 'code' ? t('signin.copied') : t('signin.copyCode')}
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

      <details className="mt-2">
        <summary className="cursor-pointer text-meta text-ink-faint">{t('signin.output')}</summary>
        <div className="mt-2">
          <MonoWell
            label={t('console.label')}
            live={live}
            dropped={dropped}
            text={lines.map((line) => line.text).join('\n')}
          />
        </div>
      </details>
    </section>
  );
}

/**
 * The one-time code a tool shows for the person to enter on its page (CODEXACCT2): the word on the first line with words
 * after the one asking for it, or null where none is shown. Matched loosely, as the link and the prompt are, since the
 * sentence is the tool's to change; measured on Codex 0.160.0 as `2. Enter this one-time code (expires in 15 minutes)`
 * and the code indented on the next line.
 */
function deviceCode(texts: readonly string[]): string | null {
  const asking = texts.findIndex((text) => /one-time code/i.test(text));
  if (asking < 0) return null;
  const shown = texts.slice(asking + 1).find((text) => text.trim())?.trim();
  return shown && /^\S+$/.test(shown) ? shown : null;
}
