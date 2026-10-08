import type { TFunction } from 'i18next';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, CheckField } from '../ui';

/**
 * A second-opinion rule as the driver answers it (XAGENT1a, D155 point 3): none here, or its occasions (`landing`, `steps`),
 * its reviewers in the order they are tried, its switches, the bound of one pass, and the reviewers of the working agent's own
 * family, which the driver judges and the page only says.
 */
export type OpinionRule = {
  none?: boolean;
  on: string[];
  reviewers: string[];
  required?: boolean;
  verify?: boolean;
  minutes?: number | null;
  recheck?: boolean;
  sameAgent?: string[];
};

/** Where a repository's rule was set: for it, or for its workspace. */
export type OpinionSource = 'repository' | 'workspace';

/** One repository's rule as the driver resolves it (`LINES`' `opinions`); `rule` and `source` absent where nothing is set. */
export type RepositoryOpinion = { repository: string; workspace: string; rule?: OpinionRule | null; source?: OpinionSource | null };

/** What a set changes, each part only where it is said: the twins' edit in the shared table's shape (design §2.5). */
export type OpinionSet = { reviewers?: string[]; on?: string[]; required?: boolean; verify?: boolean; minutes?: number | string; recheck?: boolean };

/** One change to a second-opinion rule: set what it names over the rule set there, say a repository has none, or clear. */
export type OpinionEdit = { set?: OpinionSet; none?: true; clear?: true };

/** The edit for a repository or a workspace, as `SET_OPINION` takes it. */
export type OpinionChange = { repository?: string; workspace?: string } & OpinionEdit;

/** One pass's bound where the rule names none — the twins' `OPINION_DEFAULT_MINUTES`, which the driver answers in its stead. */
const DEFAULT_MINUTES = 20;

const ticked = (names: readonly string[]) => names.map((name) => `\`${name}\``);

/** Names in backticks, in the order they are tried — the twins' `orElse`. */
const orElse = (t: TFunction, names: readonly string[]) => ticked(names).join(t('settings.opinion.elseJoin'));

/** Names in backticks, the last after the catalogue's *and* — the twins' `and`. */
function and(t: TFunction, names: readonly string[]): string {
  const all = ticked(names);
  return all.length <= 1
    ? all.join('')
    : t('settings.opinion.and', { rest: all.slice(0, -1).join(t('settings.opinion.listJoin')), last: all[all.length - 1] });
}

/**
 * What each door says as a rule is set (design §2.5), in the page's language: when its reviewers read and where their
 * findings go, what they may run, what happens when none can read, the bound of a pass, a recheck turned off, and which of
 * them are the working agent's own family — the twins' sentences (`opinionSays`, `OpinionRules.Says`), a catalogue key each.
 */
export function opinionSays(t: TFunction, rule: OpinionRule | null | undefined): string[] {
  if (!rule) return [t('settings.opinion.says.noneSet')];
  if (rule.none) return [t('settings.opinion.says.noneHere')];
  const named = orElse(t, rule.reviewers);
  // Several reviewers are set apart as the language sets an aside: "`codex-acp`, else `dsh`, reads it".
  const reviewers = rule.reviewers.length > 1 ? t('settings.opinion.several', { names: named }) : named;
  const said = rule.on.includes('landing')
    ? [
        t('settings.opinion.says.landing', { reviewers }),
        ...(rule.on.includes('steps') ? [t('settings.opinion.says.stepsToo')] : []),
      ]
    : [t('settings.opinion.says.steps', { reviewers })];
  if (rule.verify) said.push(t('settings.opinion.says.verify'));
  said.push(t(rule.required ? 'settings.opinion.says.required' : 'settings.opinion.says.optional'));
  said.push(t('settings.opinion.says.minutes', { minutes: rule.minutes ?? DEFAULT_MINUTES }));
  if (rule.recheck === false) said.push(t('settings.opinion.says.noRecheck'));
  const same = rule.reviewers.filter((reviewer) => (rule.sameAgent ?? []).includes(reviewer));
  if (same.length > 0) {
    said.push(t(same.length === 1 ? 'settings.opinion.says.sameAgentOne' : 'settings.opinion.says.sameAgentMany', { names: and(t, same) }));
  }

  return said;
}

/**
 * What a Setup row says of the rule standing (design §2.5–§2.6): the door's sentences, that nothing reads it yet, and where it
 * was set; nothing set says today's behaviour and nothing more.
 */
export function opinionRowSays(t: TFunction, rule: OpinionRule | null | undefined, from?: { source?: OpinionSource | null; workspace?: string }): string {
  if (!rule) return t('settings.opinion.says.noneSet');
  return [
    ...opinionSays(t, rule),
    t('settings.opinion.says.declaredOnly'),
    ...(from?.source === 'repository' ? [t('settings.opinion.from.repository')] : []),
    ...(from?.source === 'workspace' ? [t('settings.opinion.from.workspace', { workspace: from.workspace })] : []),
  ].join(t('projects.setup.sentenceJoin'));
}

/** What a change said once the driver took it. */
export function opinionToast(t: TFunction, name: string, edit: OpinionEdit): string {
  if (edit.clear) return t('settings.opinion.cleared', { name });
  if (edit.none) return t('settings.opinion.noneSaved', { name });
  return t('settings.opinion.saved', { name, reviewers: (edit.set?.reviewers ?? []).join(t('settings.opinion.listJoin')) });
}

/** A rule in a folded section's line: who reads, and whether it is required; none here, or nothing set. */
export function opinionSummary(t: TFunction, rule: OpinionRule | null | undefined): string {
  if (!rule) return t('settings.opinion.summary.none');
  if (rule.none) return t('settings.opinion.summary.noneHere');
  return t(rule.required ? 'settings.opinion.summary.required' : 'settings.opinion.summary.optional', { reviewers: orElse(t, rule.reviewers) });
}

const FIELD = 'w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint';

/**
 * **One second-opinion rule's control** (XAGENT1a, design §2.3, §2.5): its reviewers in the order they are tried, when they
 * read by themselves, whether work waits when none can, whether a reviewer may run what is declared safe, the bound of a pass,
 * and whether the answers are read again — the rule set here, or a fresh one; and *None here* for a repository. Every press
 * goes out as the twins' edit, judged by the driver, whose refusal is said in its words. Props only (components §2).
 */
export function OpinionField({ name, owner, set, busy = false, onChange }: {
  /** The repository or workspace, for each control's accessible name. */
  name: string;
  owner: 'repository' | 'workspace';
  /** The rule set here, none included; undefined where nothing of its own is set. */
  set?: OpinionRule;
  busy?: boolean;
  onChange: (edit: OpinionEdit) => void;
}) {
  const { t } = useTranslation();
  const own = set && !set.none ? set : undefined;
  const [reviewers, setReviewers] = useState(own?.reviewers.join(', ') ?? '');
  const [landing, setLanding] = useState(own ? own.on.includes('landing') : true);
  const [steps, setSteps] = useState(own ? own.on.includes('steps') : false);
  const [required, setRequired] = useState(Boolean(own?.required));
  const [verify, setVerify] = useState(Boolean(own?.verify));
  const [minutes, setMinutes] = useState(String(own?.minutes ?? DEFAULT_MINUTES));
  const [recheck, setRecheck] = useState(own?.recheck !== false);
  // Set or cleared by either door, the form starts again from what stands.
  const standing = own ? JSON.stringify(own) : '';
  useEffect(() => {
    setReviewers(own?.reviewers.join(', ') ?? '');
    setLanding(own ? own.on.includes('landing') : true);
    setSteps(own ? own.on.includes('steps') : false);
    setRequired(Boolean(own?.required));
    setVerify(Boolean(own?.verify));
    setMinutes(String(own?.minutes ?? DEFAULT_MINUTES));
    setRecheck(own?.recheck !== false);
    // Keyed by what stands, as text: only a change in it resets the form, not each draw.
  }, [standing]);

  const named = reviewers.split(',').map((each) => each.trim());
  const ready = named.some((each) => each.length > 0) && (landing || steps);

  return (
    <div className="grid gap-2">
      <form
        className="grid max-w-[40rem] gap-2"
        onSubmit={(event) => {
          event.preventDefault();
          if (!ready) return;
          // Sent as typed: the driver's table judges it, a blank between commas and minutes that are no number included.
          const bound = minutes.trim();
          onChange({
            set: {
              reviewers: named,
              on: [...(landing ? ['landing'] : []), ...(steps ? ['steps'] : [])],
              required,
              verify,
              minutes: /^[0-9]+$/.test(bound) ? Number(bound) : bound,
              recheck,
            },
          });
        }}
      >
        <input
          aria-label={t('settings.opinion.reviewers', { name })}
          value={reviewers}
          onChange={(event) => setReviewers(event.target.value)}
          placeholder={t('settings.opinion.reviewersPlaceholder')}
          spellCheck={false}
          className={FIELD}
        />
        <fieldset className="m-0 flex flex-wrap items-center gap-x-3 gap-y-1.5 border-0 p-0" aria-label={t('settings.opinion.occasions', { name })}>
          <CheckField label={t('settings.opinion.on.landing')} checked={landing} disabled={busy} onChange={setLanding} className="text-small" />
          <CheckField label={t('settings.opinion.on.steps')} checked={steps} disabled={busy} onChange={setSteps} className="text-small" />
        </fieldset>
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1.5">
          <CheckField label={t('settings.opinion.required')} checked={required} disabled={busy} onChange={setRequired} className="text-small" />
          <CheckField label={t('settings.opinion.verify')} checked={verify} disabled={busy} onChange={setVerify} className="text-small" />
          <CheckField label={t('settings.opinion.recheck')} checked={recheck} disabled={busy} onChange={setRecheck} className="text-small" />
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <input
            type="number"
            min={5}
            max={120}
            aria-label={t('settings.opinion.minutes', { name })}
            value={minutes}
            onChange={(event) => setMinutes(event.target.value)}
            className={`${FIELD} max-w-[6rem]`}
          />
          <Button type="submit" disabled={busy || !ready}>{t('settings.opinion.save')}</Button>
        </div>
      </form>
      {owner === 'repository' && !set?.none && (
        <div className="flex flex-wrap gap-2">
          <Button variant="ghost" disabled={busy} onClick={() => onChange({ none: true })}>{t('settings.opinion.noneHere')}</Button>
        </div>
      )}
    </div>
  );
}
