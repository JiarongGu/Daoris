import { useTranslation } from 'react-i18next';
import { Button } from '../ui';

/** The person's word on a staged build (UPDATE1, D139 §3), as `DAORIS.UPDATE` · `SET` takes it. */
export type UpdateMode = 'when-idle' | 'now' | 'not-now';

/** How a swap ended (D139 §6): installed, rolled back or refused, the build it was, and the check's or the roll-back's reason. */
export type UpdateOutcome = {
  phase: string; build?: string | null; version?: string | null; commit?: string | null; reason?: string | null; detail?: string | null;
};

/** Where an install's update stands, as the shell's `InstallUpdater` answers it: no path of this machine's. */
export type UpdateState = {
  state: 'none' | 'waiting' | 'draining' | 'applying' | 'refused';
  staged: { id: string; version: string; commit?: string | null; at?: string | null } | null;
  mode: UpdateMode | null;
  /** Driven sessions still running, and conversations with a turn in flight: what the drain waits on. */
  driven: number;
  turns: number;
  /** Why the staged build is not being installed: the check's code, which this words, and the driver's sentence. */
  problem: { code: string; message: string } | null;
  /** How the last swap ended, said once at the start after it, until dismissed: what this banner shows. */
  outcome: UpdateOutcome | null;
  /**
   * The last swap as the install's journal records it, told or not, on every state (UPDATE1d): what Settings' row says
   * after a dismissal, as the terminal's plain `daoris-driver update` says it (D50). Null with no journal, and while a swap
   * is under way. The banner reads `outcome`, never this.
   */
  last: UpdateOutcome | null;
};

/**
 * The codes a refusal or a roll-back is said by; anything else keeps the driver's own sentence. `move` and `error` are
 * SWAP2's (D139's SWAP2 note): a move that failed other than by a hold, and the launcher's error with nothing under way.
 */
const WHY = new Set([
  'manifest', 'schema', 'path', 'missing', 'size', 'hash', 'unlisted', 'required', 'host', 'launcher', 'busy', 'previous',
  'exited', 'start', 'interrupted', 'move', 'error',
]);

/**
 * The install's update, as a quiet strip under the app strip (UPDATE1, D139 §2, §3): a build staged and waiting, the drain
 * counting down what runs, the swap under way, a refusal, and — once, at the start after — how the last swap ended. Its
 * words are the banner's half of the two doors; `daoris-driver update` is the terminal's.
 *
 * @remarks
 * A molecule: drawn from props, so every state is reachable without a shell (D52 as amended). Nothing when nothing is
 * staged and nothing is left to say. A version is named with its commit where the build carries one, since a build at
 * `0.0.x` keeps its version across many builds.
 */
export function UpdateBanner({ update, busy = false, onSay, onDismiss }: {
  update: UpdateState | undefined;
  busy?: boolean;
  onSay: (mode: UpdateMode) => void;
  onDismiss: () => void;
}) {
  const { t } = useTranslation();
  if (!update) return null;
  const { state, staged, outcome, problem } = update;
  if (state === 'none' && !outcome) return null;

  const named = (version?: string | null, commit?: string | null) =>
    `${version ?? ''}${commit ? ` (${commit})` : ''}`.trim();
  const why = (code?: string | null, fallback?: string | null) =>
    (code && WHY.has(code) ? t(`update.why.${code}`) : fallback ?? code ?? '');
  const version = named(staged?.version, staged?.commit);
  const running = update.driven + update.turns;

  return (
    <div role="status" aria-label={t('update.label')} className="grid border-b border-line bg-raised text-small text-ink-soft">
      {outcome && (
        <div className="flex flex-wrap items-center gap-2 px-3 py-1.5">
          <span className="min-w-0 flex-1">
            {outcome.phase === 'installed'
              ? t('update.installed', { version: named(outcome.version, outcome.commit) })
              : outcome.phase === 'rolled-back'
                ? t('update.rolledBack', { version: named(outcome.version, outcome.commit), why: why(outcome.reason, outcome.detail) })
                : t('update.refusedAtSwap', { version: named(outcome.version, outcome.commit), why: why(outcome.reason, outcome.detail) })}
          </span>
          <Button variant="ghost" onClick={onDismiss}>{t('common.dismiss')}</Button>
        </div>
      )}
      {state !== 'none' && (
        <div className="flex flex-wrap items-center gap-2 px-3 py-1.5">
          <span className="min-w-0 flex-1">
            {state === 'waiting' && t('update.waiting', { version })}
            {state === 'draining' && (
              <>
                {t('update.draining', { version })}
                {running > 0 && <> {t('update.running', { count: running })}</>}
              </>
            )}
            {state === 'applying' && t('update.applying', { version })}
            {state === 'refused' && t('update.refused', { version, why: why(problem?.code, problem?.message) })}
          </span>
          {state === 'waiting' && (
            <>
              <Button disabled={busy} onClick={() => onSay('when-idle')}>{t('update.whenIdle')}</Button>
              <Button variant="primary" disabled={busy} onClick={() => onSay('now')}>{t('update.now')}</Button>
            </>
          )}
          {state === 'draining' && (
            <>
              <Button variant="primary" disabled={busy} onClick={() => onSay('now')}>{t('update.now')}</Button>
              <Button variant="ghost" disabled={busy} onClick={() => onSay('not-now')}>{t('update.notNow')}</Button>
            </>
          )}
        </div>
      )}
    </div>
  );
}
