import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { moment } from '../format';
import { Button, Card, MetaLine, Pill, SectionTitle, SettingRow } from '../ui';
import type { UpdateMode, UpdateState } from '../update/UpdateBanner';

/** How a swap ended, as `outcome` carries it. */
export type UpdateOutcome = NonNullable<UpdateState['outcome']>;

/**
 * Where the install's update stands, as Settings' row reads it: what `DAORIS.UPDATE` · `STATE` answers, and `last`.
 *
 * @remarks
 * `last` is how the last swap ended, kept after the banner's *Dismiss* puts `outcome` away, as the terminal's plain
 * `daoris-driver update` still says it from the swap's journal (D50). The modules' `STATE` does not answer it yet
 * (UPDATE1b), so until it does the row says the last swap while `outcome` stands, and nothing of it after a dismissal.
 */
export type UpdateStanding = UpdateState & { last?: UpdateOutcome | null };

/** The pill a drain's state wears: in progress in taken's hue, a refusal an outcome in declined's, a held word neutral. */
const STATE_TONE: Record<string, string> = { draining: 'taken', applying: 'taken', waiting: 'neutral', refused: 'declined' };

/** The pill a swap's ending wears: each an outcome (D41 §3), so installed is done's and the rest declined's. */
const OUTCOME_TONE: Record<string, string> = { installed: 'done', 'rolled-back': 'declined', refused: 'declined' };

/** The words each state offers, the move first (D41 §4), as the banner offers them. */
const WORDS: Record<string, readonly UpdateMode[]> = { waiting: ['when-idle', 'now'], draining: ['now', 'not-now'] };

const WORD_KEY: Record<UpdateMode, string> = { 'when-idle': 'update.whenIdle', now: 'update.now', 'not-now': 'update.notNow' };

const WORD_VARIANT = { 'when-idle': 'default', now: 'primary', 'not-now': 'ghost' } as const;

/**
 * The install's update in Settings (UPDATE1b, D139 §3, §6): what is staged beside it, the drain and what it waits on, how
 * the last swap ended, and the screen's three words for it — the row that stands where the banner, the screen's other
 * door, is gone once dismissed. `daoris-driver update` is the terminal's door to the same request.
 *
 * @remarks
 * A molecule: drawn from props, so every state is reachable without a shell (D52 as amended); the Driver domain holds
 * the hooks. It says the banner's sentences where they are the same fact — a swap's ending and each check's reason — so
 * the two doors never word one thing twice. Nothing until the shell answers.
 */
export function UpdateSection({ update, busy = false, onSay }: {
  update: UpdateStanding | undefined;
  busy?: boolean;
  onSay: (mode: UpdateMode) => void;
}) {
  const { t, i18n } = useTranslation();
  if (!update) return null;
  const { state, staged, problem } = update;
  const last = update.last ?? update.outcome;

  const named = (version?: string | null, commit?: string | null) =>
    `${version ?? ''}${commit ? ` (${commit})` : ''}`.trim();
  // A check's code the catalogue words; one it does not keeps the driver's own sentence (D139 §4).
  const why = (code?: string | null, fallback?: string | null) =>
    (code && i18n.exists(`update.why.${code}`) ? t(`update.why.${code}`) : fallback ?? code ?? '');

  const waits = () => {
    const driven = update.driven > 0 ? t('settings.update.driven', { count: update.driven }) : null;
    const turns = update.turns > 0 ? t('settings.update.turns', { count: update.turns }) : null;
    if (driven && turns) return t('settings.update.waitsOnBoth', { driven, turns });
    if (driven || turns) return t('settings.update.waitsOn', { what: driven ?? turns });
    return t('settings.update.nothingRuns');
  };

  const standing = (): string => {
    switch (state) {
      case 'draining': return t('settings.update.join', { first: t('settings.update.draining'), second: waits() });
      case 'waiting': return t('settings.update.waiting');
      case 'applying': return t('settings.update.applying');
      case 'refused': return t('settings.update.refused', { why: why(problem?.code, problem?.message) });
      default: return t('settings.update.none');
    }
  };

  const ending = (outcome: UpdateOutcome) => {
    const version = named(outcome.version, outcome.commit);
    if (outcome.phase === 'installed') return t('update.installed', { version });
    if (outcome.phase === 'rolled-back') return t('update.rolledBack', { version, why: why(outcome.reason, outcome.detail) });
    return t('update.refusedAtSwap', { version, why: why(outcome.reason, outcome.detail) });
  };

  const pillWord = (word: string) => (i18n.exists(`settings.update.pill.${word}`) ? t(`settings.update.pill.${word}`) : word);
  const words = WORDS[state] ?? [];

  return (
    <Card id="settings-update" className="scroll-mt-3">
      <section aria-label={t('settings.update.title')}>
        <SectionTitle>{t('settings.update.title')}</SectionTitle>
        {/* The rows apart from the title, so the rule between rows falls between them and never under the title. */}
        <div>
          <SettingRow
            label={t('settings.update.staged')}
            hint={t('settings.update.terminal')}
            why={t('settings.update.why')}
            control={words.length > 0 && words.map((word) => (
              <Button key={word} variant={WORD_VARIANT[word]} disabled={busy} onClick={() => onSay(word)}>
                {t(WORD_KEY[word])}
              </Button>
            ))}
          >
            <div className="grid gap-1.5">
              <Said pill={state in STATE_TONE ? { tone: STATE_TONE[state]!, word: pillWord(state) } : null}>{standing()}</Said>
              {staged && (
                <MetaLine
                  items={[
                    { label: t('settings.update.build'), value: staged.id, mono: true },
                    { label: t('settings.update.version'), value: staged.version },
                    { label: t('settings.update.commit'), value: staged.commit ?? null, mono: true },
                    { label: t('settings.update.at'), value: staged.at ? moment(staged.at) : null },
                  ]}
                />
              )}
            </div>
          </SettingRow>
          {last && (
            <SettingRow label={t('settings.update.last')}>
              <Said pill={{ tone: OUTCOME_TONE[last.phase] ?? 'declined', word: pillWord(last.phase) }}>{ending(last)}</Said>
            </SettingRow>
          )}
        </div>
      </section>
    </Card>
  );
}

/** A state's word on its pill, then its sentence: status leads (D41 §4). */
function Said({ pill, children }: { pill: { tone: string; word: string } | null; children: ReactNode }) {
  return (
    <p className="m-0 text-small text-ink-soft">
      {pill && <><Pill tone={pill.tone}>{pill.word}</Pill>{' '}</>}
      <span>{children}</span>
    </p>
  );
}
