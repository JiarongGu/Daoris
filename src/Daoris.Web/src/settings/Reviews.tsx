import type { TFunction } from 'i18next';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, CheckField, Chip, Icon, Segmented } from '../ui';

/**
 * One review environment (REVIEWENV1a, D154 point 1): where a repository's work runs for the person to look at before it
 * lands. Its name, its kind (`local` or `deployed`), the repository's own procedure for reaching it, the address where the
 * app is used, and a local one's command for work needing a process of its own.
 */
export type ReviewEnvironment = { name: string; kind: string; procedure: string; address?: string | null; run?: string | null };

/** A review rule as the driver answers it: none here, or its environments, the first the default, and whether work waits. */
export type ReviewRule = { none?: boolean; required?: boolean; environments: ReviewEnvironment[] };

/** Where a repository's rule was set: for it, or for its workspace. */
export type ReviewSource = 'repository' | 'workspace';

/** One repository's rule as the driver resolves it (`LINES`' `reviews`); `rule` and `source` absent where nothing is set. */
export type RepositoryReview = { repository: string; workspace: string; rule?: ReviewRule | null; source?: ReviewSource | null };

/**
 * One change to a review rule, the twins' edit in the shared table's shape (design §1.7): add or replace an environment (with
 * whether work waits), say a repository has none, drop one, say whether it is required, or clear.
 */
export type ReviewEdit = { put?: ReviewEnvironment; required?: boolean; none?: true; drop?: string; clear?: true };

/** The edit for a repository or a workspace, as `SET_REVIEW` takes it. */
export type ReviewChange = { repository?: string; workspace?: string } & ReviewEdit;

/** What a put's procedure look found (`SET_REVIEW`'s `reviewed`): the workspace's repositories lacking it, and whether it was looked for. */
export type ReviewLook = { lacking: string[]; unchecked: boolean };

/** Names in backticks, the last after the catalogue's *or*: `a`, `a or b`, `a, b or c` — the twins' `either`. */
function either(t: TFunction, names: string[]): string {
  const ticked = names.map((name) => `\`${name}\``);
  return ticked.length <= 1
    ? ticked.join('')
    : t('settings.review.either', { rest: ticked.slice(0, -1).join(t('settings.review.listJoin')), last: ticked[ticked.length - 1] });
}

/**
 * What each door says as a rule is set (design §1.7), in the page's language: whether work waits for the person's look and
 * where, then what a set-up step may do in each environment, in order — the twins' sentences (`reviewSays`, `ReviewRules.Says`),
 * a catalogue key each. Nothing set says today's behaviour.
 */
export function reviewSays(t: TFunction, rule: ReviewRule | null | undefined): string[] {
  if (!rule) return [t('settings.review.says.noneSet')];
  if (rule.none) return [t('settings.review.says.noneHere')];
  const [first, ...others] = rule.environments;
  const said = rule.required
    ? [
        t('settings.review.says.required', { environment: first?.name }),
        ...(others.length > 0 ? [t('settings.review.says.instead', { environments: either(t, others.map((each) => each.name)) })] : []),
      ]
    : [t('settings.review.says.optional', { environments: either(t, rule.environments.map((each) => each.name)) })];
  for (const environment of rule.environments) {
    if (environment.kind === 'local') {
      said.push(t('settings.review.says.local', { address: environment.address }));
      if (environment.run) said.push(t('settings.review.says.run', { command: environment.run }));
    } else {
      said.push(t('settings.review.says.deployed', { procedure: environment.procedure, environment: environment.name }));
    }
  }

  return said;
}

/**
 * What a Setup row says of the rule standing (design §1.7–§1.8): the door's sentences, what the landing's gate does with it
 * (the twins' `reviewGate` and `ReviewRules.Gate`, REVIEWENV1c2), nothing after none here, and where it was set; nothing set
 * says today's behaviour and nothing more.
 */
export function reviewRowSays(t: TFunction, rule: ReviewRule | null | undefined, from?: { source?: ReviewSource | null; workspace?: string }): string {
  if (!rule) return t('settings.review.says.noneSet');
  return [
    ...reviewSays(t, rule),
    ...(rule.none ? [] : [t('settings.review.says.waiting')]),
    ...(from?.source === 'repository' ? [t('settings.review.from.repository')] : []),
    ...(from?.source === 'workspace' ? [t('settings.review.from.workspace', { workspace: from.workspace })] : []),
  ].join(t('projects.setup.sentenceJoin'));
}

/** What a change said once the driver took it: what it did, and for a put what its procedure look found. */
export function reviewToast(t: TFunction, name: string, edit: ReviewEdit, look?: ReviewLook | null): string {
  if (edit.clear) return t('settings.review.cleared', { name });
  if (edit.none) return t('settings.review.noneSaved', { name });
  if (edit.drop !== undefined) return t('settings.review.dropped', { name, environment: edit.drop });
  if (!edit.put) return t(edit.required ? 'settings.review.requiredSaved' : 'settings.review.notRequiredSaved', { name });
  return [
    t('settings.review.saved', { name, environment: edit.put.name }),
    ...(look && look.lacking.length > 0
      ? [t('settings.review.savedLacking', { repositories: look.lacking.join(t('settings.review.listJoin')), procedure: edit.put.procedure })]
      : []),
    ...(look?.unchecked ? [t('settings.review.unchecked', { procedure: edit.put.procedure })] : []),
  ].join(t('projects.setup.sentenceJoin'));
}

/** A rule in a folded section's line: where work is reviewed, and whether it waits; none here, or nothing set. */
export function reviewSummary(t: TFunction, rule: ReviewRule | null | undefined): string {
  if (!rule) return t('settings.review.summary.none');
  if (rule.none) return t('settings.review.summary.noneHere');
  const environments = either(t, rule.environments.map((each) => each.name));
  return t(rule.required ? 'settings.review.summary.required' : 'settings.review.summary.optional', { environments });
}

/**
 * **One review rule's control** (REVIEWENV1a, design §1.7): the environments set here, each with its drop; whether work waits
 * for the person's look; *None here* for a repository; and an environment's form, open where nothing of its own is set yet.
 * Every press goes out as the twins' edit, judged by the driver, whose refusal is said in its words. Props only (components §2).
 */
export function ReviewField({ name, owner, set, busy = false, onChange }: {
  /** The repository or workspace, for each control's accessible name. */
  name: string;
  owner: 'repository' | 'workspace';
  /** The rule set here, none included; undefined where nothing of its own is set. */
  set?: ReviewRule;
  busy?: boolean;
  onChange: (edit: ReviewEdit) => void;
}) {
  const { t } = useTranslation();
  const environments = set && !set.none ? set.environments : [];
  const [adding, setAdding] = useState(environments.length === 0);
  // Set or cleared by either door, the form starts again from what stands.
  useEffect(() => setAdding(environments.length === 0), [environments.length]);

  return (
    <div className="grid gap-2">
      {environments.length > 0 && (
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1.5">
          <ul aria-label={t('settings.review.environments', { name })} className="m-0 flex list-none flex-wrap gap-1.5 p-0">
            {environments.map((environment, index) => (
              <li key={environment.name} className="inline-flex items-center gap-0.5">
                <Chip accent={index === 0}>
                  {t(index === 0 ? 'settings.review.chipDefault' : 'settings.review.chip', {
                    environment: environment.name, kind: t(`settings.review.kind.${environment.kind === 'local' ? 'local' : 'deployed'}`),
                  })}
                </Chip>
                <Button
                  variant="ghost"
                  disabled={busy}
                  aria-label={t('settings.review.drop', { environment: environment.name, name })}
                  onClick={() => onChange({ drop: environment.name })}
                >
                  <Icon name="x" size={12} />
                </Button>
              </li>
            ))}
          </ul>
          <CheckField
            label={t('settings.review.required')}
            checked={Boolean(set?.required)}
            disabled={busy}
            onChange={(required) => onChange({ required })}
            className="text-small"
          />
        </div>
      )}
      {adding
        ? (
          <EnvironmentForm
            name={name}
            fresh={environments.length === 0}
            busy={busy}
            onSave={(put, required) => onChange(required === undefined ? { put } : { put, required })}
            onCancel={environments.length > 0 ? () => setAdding(false) : undefined}
          />
        )
        : (
          <div className="flex flex-wrap gap-2">
            <Button disabled={busy} onClick={() => setAdding(true)}>
              <Icon name="plus" size={13} />
              {t('settings.review.add')}
            </Button>
          </div>
        )}
      {owner === 'repository' && !set?.none && (
        <div className="flex flex-wrap gap-2">
          <Button variant="ghost" disabled={busy} onClick={() => onChange({ none: true })}>{t('settings.review.noneHere')}</Button>
        </div>
      )}
    </div>
  );
}

const FIELD = 'w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint';

/**
 * An environment's form (design §1.3): its name, its kind, the repository's procedure, where the app runs (required for a
 * local one, the only place a set-up step may show the work), a local one's command, and for a rule set afresh whether work
 * waits. Sent as typed: the driver's table judges it.
 */
function EnvironmentForm({ name, fresh, busy, onSave, onCancel }: {
  name: string;
  /** A rule set afresh, which says here whether work waits; an added environment leaves that as it stands. */
  fresh: boolean;
  busy: boolean;
  onSave: (put: ReviewEnvironment, required?: boolean) => void;
  onCancel?: () => void;
}) {
  const { t } = useTranslation();
  const [environment, setEnvironment] = useState('');
  const [kind, setKind] = useState<'local' | 'deployed'>('local');
  const [procedure, setProcedure] = useState('');
  const [address, setAddress] = useState('');
  const [run, setRun] = useState('');
  const [required, setRequired] = useState(false);
  const local = kind === 'local';
  const ready = environment.trim().length > 0 && procedure.trim().length > 0 && (!local || address.trim().length > 0);

  return (
    <form
      className="grid max-w-[40rem] gap-2"
      onSubmit={(event) => {
        event.preventDefault();
        if (!ready) return;
        const put: ReviewEnvironment = {
          name: environment.trim(), kind, procedure: procedure.trim(),
          ...(address.trim() ? { address: address.trim() } : {}),
          ...(local && run.trim() ? { run: run.trim() } : {}),
        };
        onSave(put, fresh ? required : undefined);
      }}
    >
      <div className="flex flex-wrap items-center gap-2">
        <input
          aria-label={t('settings.review.name', { name })}
          value={environment}
          onChange={(event) => setEnvironment(event.target.value)}
          placeholder={t('settings.review.namePlaceholder')}
          spellCheck={false}
          className={`${FIELD} max-w-[12rem]`}
        />
        <Segmented<'local' | 'deployed'>
          label={t('settings.review.kindField', { name })}
          value={kind}
          options={[
            { value: 'local', label: t('settings.review.kind.local') },
            { value: 'deployed', label: t('settings.review.kind.deployed') },
          ]}
          onChange={setKind}
        />
      </div>
      <input
        aria-label={t('settings.review.procedure', { name })}
        value={procedure}
        onChange={(event) => setProcedure(event.target.value)}
        placeholder={t('settings.review.procedurePlaceholder')}
        spellCheck={false}
        className={FIELD}
      />
      <input
        aria-label={t(local ? 'settings.review.address' : 'settings.review.addressOptional', { name })}
        value={address}
        onChange={(event) => setAddress(event.target.value)}
        placeholder={t('settings.review.addressPlaceholder')}
        spellCheck={false}
        className={FIELD}
      />
      {local && (
        <input
          aria-label={t('settings.review.run', { name })}
          value={run}
          onChange={(event) => setRun(event.target.value)}
          placeholder={t('settings.review.runPlaceholder')}
          spellCheck={false}
          className={FIELD}
        />
      )}
      <div className="flex flex-wrap items-center gap-2">
        {fresh && (
          <CheckField label={t('settings.review.required')} checked={required} disabled={busy} onChange={setRequired} className="text-small" />
        )}
        <Button type="submit" disabled={busy || !ready}>{t('settings.review.save')}</Button>
        {onCancel && <Button variant="ghost" onClick={onCancel}>{t('common.cancel')}</Button>}
      </div>
    </form>
  );
}
