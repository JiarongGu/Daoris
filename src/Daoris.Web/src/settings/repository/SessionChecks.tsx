import type { TFunction } from 'i18next';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Inline, Segmented, SettingRow } from '../../design/ui';
import { shellWord } from '../../lib/shellWord';
import { type Answered, Refused } from '../../work/InlineConfirm';

/**
 * Which checks a repository's sessions run under Current (FABRIC-PIPELINE1b, D192 point 4; `STATE`'s `sessionChecks`): `light`, the gates
 * it does not mark heavy and the checks a change reaches, its heavy gates left to the verification and refused to its sessions; or
 * `heavy-at-end`, its heavy gates once, at a session's end. None is today's.
 */
export type SessionChecksValue = 'light' | 'heavy-at-end';

/** The choices the control offers, as before first: the twins' `SessionChecks.Choices` after it. */
const CHOICES = ['today', 'light', 'heavy-at-end'] as const;
type Choice = (typeof CHOICES)[number];

/** A value the page does not know reads as none, never as a guess. */
const choiceOf = (checks: string | null | undefined): Choice => (checks === 'light' || checks === 'heavy-at-end' ? checks : 'today');

/** The rule in a folded section's line, or null where none is set, which says nothing there. */
export function sessionChecksSummary(t: TFunction, checks: string | null | undefined): string | null {
  const choice = choiceOf(checks);
  return choice === 'today' ? null : t(`settings.checks.summary.${choice}`);
}

/** What a press said once the driver took it. */
export function sessionChecksToast(t: TFunction, name: string, checks: string | null): string {
  const choice = choiceOf(checks);
  return choice === 'today' ? t('settings.checks.cleared', { name }) : t(`settings.checks.saved.${choice}`, { name });
}

/**
 * **A repository's sessions' checks** (FABRIC-PIPELINE1b, the pipeline design §3.1, §3.2): a row of its Setup's *Heavy gates*, the same
 * `sessionChecks` `daoris-driver checks` edits (D50). It says what the choice standing means; under light, its light gates and its heavy
 * gates by name as the driver read them, that an agent which takes no rules from Daoris is told them and refused `run-heavy` rather than
 * refused in its shell, and that nothing runs the heavy gates after a landing yet; and a repository that marks no heavy gate gives its
 * sessions every gate. A press goes out at once and the driver judges it; its refusal is said in its words. The person's choice alone:
 * light lowers their part, so no proposal names it.
 *
 * A molecule: every state is reached by its props, and every press goes out.
 */
export function SessionChecksRow({ repository, checks, light, heavy, known, busy = false, onChange }: {
  repository: string;
  /** The rule set for it; null where none is, which is today's. */
  checks: SessionChecksValue | null;
  /** The gates it declares and does not mark heavy, as the driver read them; none where it read none. */
  light?: string[] | null;
  /** Its heavy gates by name: those its own file marks, else this machine's list for it. */
  heavy: readonly string[];
  /** Whether the driver has read its gates yet; until it has, nothing is said of them. */
  known: boolean;
  busy?: boolean;
  /** The choice pressed, or null for as before; the answer says whether it landed or the driver's sentence. */
  onChange: (checks: SessionChecksValue | null, answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const [refused, setRefused] = useState<string | null>(null);
  const standing = choiceOf(checks);
  const join = t('projects.setup.summary.listJoin');
  const lines: string[] = [t(`settings.checks.says.${standing}`)];
  if (known && heavy.length === 0) lines.push(t('settings.checks.noneHeavy', { repository }));
  if (known && heavy.length > 0 && standing === 'light') {
    lines.push(
      (light ?? []).length > 0
        ? t('settings.checks.gates', { light: (light ?? []).join(join), heavy: heavy.join(join) })
        : t('settings.checks.gatesNoLight', { heavy: heavy.join(join) }),
      t('settings.checks.harness'),
      t('settings.checks.unverified'),
    );
  }

  return (
    <SettingRow
      label={t('settings.checks.label')}
      hint={t('settings.checks.twin', { repository: shellWord(repository, '<repository>') })}
      why={t('settings.checks.why')}
      control={(
        <Segmented
          label={t('settings.checks.mode', { name: repository })}
          value={standing}
          options={CHOICES.map((each) => ({ value: each, label: t(`settings.checks.value.${each}`) }))}
          onChange={(chosen) => {
            if (busy || chosen === standing) return;
            setRefused(null);
            onChange(chosen === 'today' ? null : chosen, { done: () => undefined, refused: setRefused });
          }}
        />
      )}
    >
      <div className="grid gap-2">
        {lines.map((line) => <p key={line} className="m-0 text-small text-ink-soft"><Inline text={line} /></p>)}
        {refused && <Refused sentence={refused} />}
      </div>
    </SettingRow>
  );
}
