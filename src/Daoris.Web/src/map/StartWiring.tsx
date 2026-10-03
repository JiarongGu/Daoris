import { useTranslation } from 'react-i18next';
import { Pill } from '../ui';
import type { ChoiceFrom, StartWiring } from './wiring';

/**
 * What a start in each workspace would run on, and where each part came from (MAP1b, D67 §3) — the
 * wiring half of the workflow map, drawn in the order the driver resolves it. Each job a circle has
 * is a row: the work, and the intake once an agent is named for it (AGT6).
 *
 * @remarks
 * A molecule: the answer arrives as props, and what a person calls an account arrives as a function,
 * because the name (who signed in, a key's handle) is the roster's to know.
 *
 * **The refusal is the driver's sentence, verbatim** (D46 §3). It names what to run, and a
 * paraphrase of an instruction is a different instruction.
 */
export function StartWiringList({ starts, nameOf }: {
  starts: StartWiring[];
  /**
   * What a person calls this account of this tool — who is signed in, a key's handle, or its name.
   * No profile is the tool's own home, which the roster names as its row does (UX5 U53).
   */
  nameOf: (owner: string, profile?: string | null) => string;
}) {
  const { t } = useTranslation();
  // One job is no job to tell apart. Once the intake is one (AGT6), every row says which it is:
  // two rows called "a start in aurora" naming different agents would read as one fact twice, wrong.
  const jobs = starts.some((start) => start.job !== 'work');

  return (
    <ul className="m-0 grid list-none gap-3 p-0">
      {starts.map((start) => (
        <li
          key={`${start.job}-${start.workspace}`}
          aria-label={t(start.job === 'intake' ? 'wiring.rowIntake' : 'wiring.row', { workspace: start.workspace })}
          className="grid gap-1.5 rounded-control border border-line bg-raised px-3 py-2.5"
        >
          <p className="m-0 flex flex-wrap items-center gap-2">
            <span className="font-mono text-small font-semibold text-ink">{start.workspace}</span>
            {jobs && <span className="text-small text-ink-soft">{t(`wiring.job.${start.job}`)}</span>}
            {/* Held is not a quest state, so it borrows none of their hues: a neutral word, and the
                driver's sentence below it on the warning rule the page uses for standing notices. */}
            {start.refusal
              ? <Pill>{t('wiring.held')}</Pill>
              : <Pill tone="done">{t('wiring.ready')}</Pill>}
          </p>
          <dl className="m-0 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-small">
            <dt className="text-ink-faint">{t('wiring.agent')}</dt>
            <dd className="m-0 min-w-0 text-ink">
              {start.product ?? start.adapter}
              {start.product && <span className="ml-2 font-mono text-meta text-ink-faint">{start.adapter}</span>}
            </dd>
            <dt className="text-ink-faint">{t('wiring.account')}</dt>
            <dd className="m-0 min-w-0">
              <span className="text-ink wrap-anywhere">
                {nameOf(start.owner, start.profile)}
              </span>
              <span className="ml-2 text-meta text-ink-faint">{accountFrom(t, start.profileFrom)}</span>
            </dd>
            <dt className="text-ink-faint">{t('wiring.version')}</dt>
            <dd className="m-0 min-w-0">
              <span className="font-mono text-ink">{start.version ?? t('wiring.versionUnknown')}</span>
              <span className="ml-2 text-meta text-ink-faint">{versionFrom(t, start)}</span>
            </dd>
          </dl>
          {start.refusal && (
            // The driver's sentence, whole, at the card's width with every block of the page (D141): it
            // wore a 65ch measure once, which gave the page two edges.
            <p className="m-0 whitespace-pre-wrap border-l-[3px] border-warn pl-2.5 text-small text-ink-soft">
              {start.refusal}
            </p>
          )}
        </li>
      ))}
    </ul>
  );
}

type T = ReturnType<typeof useTranslation>['t'];

function accountFrom(t: T, from: ChoiceFrom): string {
  return t(`wiring.accountFrom.${from}`);
}

function versionFrom(t: T, start: StartWiring): string {
  return start.commanded ? t('wiring.versionFrom.commanded') : t(`wiring.versionFrom.${start.versionFrom}`);
}
