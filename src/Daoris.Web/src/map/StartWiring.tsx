import { useTranslation } from 'react-i18next';
import { Pill } from '../ui';
import type { ChoiceFrom, StartWiring } from './wiring';

/**
 * What a start in each workspace would run on, and where each part came from (MAP1b, D67 §3) — the
 * wiring half of the workflow map, drawn in the order the driver resolves it.
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
  /** What a person calls this account of this tool — who is signed in, a key's handle, or its name. */
  nameOf: (owner: string, profile: string) => string;
}) {
  const { t } = useTranslation();

  return (
    <ul className="m-0 grid list-none gap-3 p-0">
      {starts.map((start) => (
        <li
          key={`${start.job}-${start.workspace}`}
          aria-label={t('wiring.row', { workspace: start.workspace })}
          className="grid gap-1.5 rounded-control border border-line bg-raised px-3 py-2.5"
        >
          <p className="m-0 flex flex-wrap items-center gap-2">
            <span className="font-mono text-small font-semibold text-ink">{start.workspace}</span>
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
              <span className="break-all text-ink">
                {start.profile ? nameOf(start.owner, start.profile) : t('wiring.ownSignIn')}
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
            // A reading measure, not the card's width: across a wide window the sentence ran to 190
            // characters a line (seen on the window), the defect platform-ux names by that number.
            <p className="m-0 max-w-prose whitespace-pre-wrap border-l-[3px] border-warn pl-2.5 text-small text-ink-soft">
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
