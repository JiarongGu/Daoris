import { type FormEvent, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { phaseOf, useHarnessCancel, useHarnessInput, useSessionConsole } from '../bridge/shell';
import { Elapsed } from '../design/phases';
import { Button, Icon, MonoWell } from '../design/ui';
import { type SignInPhase, SignInSteps } from './SignInSteps';

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
 * The steps are `SignInSteps`' (SIGNIN1, the UI/UX design §3.2): the driver's phases, each written with its start into the
 * relay the tool's output takes, drawn as a list standing open, or as the line alone under a row of *What needs you*; the
 * link, the code and the ask are read from the tool's lines by their shape, and an older host that writes no phases draws
 * the steps as before, with the time since the press. The tool's own output is one disclosure away and never the surface,
 * the same rule the setting rows follow for their why; a phase line is the driver's and never shown there.
 *
 * An organism: it holds the console hook ONCE — the steps read the stream and the disclosure shows
 * it, from one subscription rather than two — and the two mutations, and renders only where a shell
 * is attached, because only a driver has a process to answer.
 *
 * `login-new` is signing in to ANOTHER account (D66 §3): the same steps, with no account to name yet — who it is, is what
 * the sign-in finds out. A device code (CODEXACCT2) is Codex's: it opens no page and asks nothing on its input, and prints a
 * link and a one-time code the person enters on that page, with the code's own bound.
 */
export function SignIn({
  id, harness, profile, action = 'login', tool, refused, failed, failedAt, pressedAt, shown = 'list', onRetry, onClose,
}: {
  id: string;
  harness: string;
  profile?: string;
  action?: 'login' | 'login-new';
  /** What a person calls the tool (AGT1) — the id when it does not say. */
  tool?: string;
  /**
   * The driver's sentence where it refused to start this sign-in (CODEXSIGNIN2): said here, whole, where the press was, with
   * *Try again* and *Close*, rather than as a toast with the panel gone. Nothing runs then, so no cancel.
   */
  refused?: string | null;
  /** The sentence of a sign-in that ended without signing in, and when (SIGNIN1): the phase it was in failed. */
  failed?: string | null;
  failedAt?: number | null;
  /** When the sign-in was pressed: the time an older host's panel says, since it writes no phases. */
  pressedAt?: number | null;
  /** The list standing open, its own surface; or the line alone, under a row of *What needs you* (§3.2). */
  shown?: 'list' | 'line';
  onRetry?: () => void;
  onClose?: () => void;
}) {
  const { t } = useTranslation();
  const { lines, live, dropped } = useSessionConsole(id);
  const input = useHarnessInput();
  const cancel = useHarnessCancel();
  const [code, setCode] = useState('');
  const [copied, setCopied] = useState<'link' | 'code' | null>(null);

  // The driver's phases and the tool's own lines, apart: a phase line is never the tool's words.
  const phases: SignInPhase[] = [];
  const texts: string[] = [];
  for (const line of lines) {
    const phase = phaseOf(line.text);
    if (phase) phases.push(phase);
    else texts.push(line.text);
  }
  const ended = Boolean(refused || failed);
  const starts = phases.filter((each) => each.phase === 'start');
  const since = starts[starts.length - 1]?.at ?? pressedAt ?? null;

  const copy = async (what: 'link' | 'code', text: string) => {
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
    <section aria-label={title} className="mt-2 basis-full rounded-card border border-line bg-raised p-3">
      <header className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
        <span className="text-body font-semibold text-ink">{title}</span>
        {since !== null && !refused && (
          <Elapsed from={since} to={failed ? failedAt ?? null : null} className="text-meta text-ink-faint" />
        )}
        {!ended && (
          <Button
            variant="ghost"
            className="ml-auto"
            disabled={cancel.isPending}
            onClick={() => cancel.mutate({ harness, action })}
          >
            <Icon name="x" size={13} />
            {t('signin.cancel')}
          </Button>
        )}
      </header>

      <SignInSteps
        title={title}
        tool={tool ?? harness}
        action={action}
        phases={phases}
        texts={texts}
        refused={refused}
        failed={failed}
        failedAt={failedAt}
        shown={shown}
        acts={{
          copied, onCopy: (what, text) => { void copy(what, text); }, code, onCode: setCode, onSend: submit,
          sending: input.isPending, sent: input.isSuccess, onRetry, onClose,
        }}
      />

      {!refused && (
        <details className="mt-2">
          <summary className="cursor-pointer text-meta text-ink-faint">{t('signin.output')}</summary>
          <div className="mt-2">
            <MonoWell label={t('console.label')} live={live} dropped={dropped} text={texts.join('\n')} />
          </div>
        </details>
      )}
    </section>
  );
}
