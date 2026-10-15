import type { TFunction } from 'i18next';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { Button, CheckField, Inline, Segmented, SelectField } from '../design/ui';

/**
 * How a session's work lands (WSR1, D87): merged into the line, or put on a branch the pattern names —
 * whether its tree and branch go once a press lands it (D88), for a branch the plugin that pushes
 * it and opens the pull request (WSR4, D100), whether a quest's done lands it with no press (LAND2a, D145), the
 * shape of the ticket key its pattern's `{ticket}` is found by (PROCESS1b-i), and whether Accept squashes the pull
 * request's branch to one commit (PROCESS1b-ii). The driver answers `squash` false, and `key` absent where none is set.
 */
export type LandingRule = {
  form: string; pattern?: string; tidy?: boolean; plugin?: string; autoAccept?: boolean; key?: string | null; squash?: boolean;
};

/** Where a repository's rule came from: set for it, set for its workspace, or the default merge. */
export type LandingSource = 'repository' | 'workspace' | 'default';

/** One repository's rule on this machine, as the driver chose it. */
export type RepositoryLanding = { repository: string; workspace: string; source: LandingSource } & LandingRule;

/**
 * A change to a rule: a repository's or a workspace's, cleared when it names no form. A `key` or a `squash` it does not name
 * is kept from the rule set there where the new one can hold it (DOORS1b); an empty key takes it away.
 */
export type LandingChange = {
  repository?: string; workspace?: string; form?: string; pattern?: string; tidy?: boolean; plugin?: string; autoAccept?: boolean;
  key?: string | null; squash?: boolean;
};

const MERGE: LandingRule = { form: 'merge' };

/** The example a pattern field shows, and what a new branch rule starts from. */
const EXAMPLE = 'feature/{quest}-{slug}';

/** The shape a ticket's key is found by where the rule names none: the driver's `LandingRules.DefaultKey`, the CLI's `DEFAULT_KEY`. */
export const DEFAULT_KEY = '[A-Z][A-Z0-9]+-\\d+';

/** Whether a branch pattern names its branch by a ticket, which only then reads a key (PROCESS1b-i). */
const ticketed = (pattern: string | undefined) => (pattern ?? '').includes('{ticket}');

/** The chooser's value for "no plugin": never a plugin's id, which starts with a letter or a digit. */
const NO_PLUGIN = '-';

const same = (a?: LandingRule, b?: LandingRule) =>
  a?.form === b?.form
  && (a?.form !== 'branch' || (a?.pattern === b?.pattern && a?.plugin === b?.plugin && Boolean(a?.autoAccept) === Boolean(b?.autoAccept)
    && (a?.key ?? '') === (b?.key ?? '') && Boolean(a?.squash) === Boolean(b?.squash)))
  && Boolean(a?.tidy) === Boolean(b?.tidy);

/**
 * What a branch rule's row says after its form (DOORS1b): what finds its pattern's `{ticket}`, in the terminal's words (the CLI's
 * `ticketSays`), and what Accept makes where it squashes. Nothing for a merge, which names no branch.
 */
export function landingProcessSays(t: TFunction, rule: LandingRule): string[] {
  if (rule.form !== 'branch') return [];
  return [
    ...(ticketed(rule.pattern)
      ? [rule.key ? t('settings.landing.byKey', { key: rule.key }) : t('settings.landing.byDefaultKey', { key: DEFAULT_KEY })]
      : []),
    ...(rule.squash ? [t('settings.landing.bySquash')] : []),
  ];
}

/** Where a repository's rule was set and what it lands as, the first of a Setup row's sentences. */
export function landingFrom(t: TFunction, landing: RepositoryLanding): string {
  return landing.source === 'default'
    ? t('settings.landing.from.default')
    : t(`settings.landing.from.${landing.source}.${landing.form === 'branch' ? 'branch' : 'merge'}`, {
        pattern: landing.pattern, workspace: landing.workspace,
      });
}

/**
 * A repository's rule in words alone, each sentence as its Setup row says it (PROCESS1e, a session's *Details*): where it was set
 * and what it lands as, who pushes a branch, whether its quest's done accepts it, what finds its ticket and what its squash makes.
 */
export function landingRuleSays(t: TFunction, landing: RepositoryLanding): string[] {
  const branch = landing.form === 'branch';
  return [
    landingFrom(t, landing),
    ...(branch && landing.plugin ? [t('settings.landing.byPlugin', { plugin: landing.plugin })] : []),
    ...(branch && landing.autoAccept ? [t('settings.landing.byAuto')] : []),
    ...landingProcessSays(t, landing),
  ];
}

/** What a draft that accepts automatically does: the plugin that pushes it, or none. Null where it does not. */
export type Accepting = { plugin?: string } | null;

type FieldProps = {
  name: string;
  set?: LandingRule;
  /** What stands without this row's own rule — what the control starts from when none is set. */
  inherited: LandingRule;
  landers: string[];
  busy?: boolean;
  /** Whether the control carries its own *Clear*: a repository's Setup gives each value one beside it (UX6f). */
  clearable?: boolean;
  /** Laid out from the row's start, as beneath a Setup row's label, where it has the row's width (UX6f). */
  alignStart?: boolean;
  onSave: (rule?: LandingRule) => void;
};

/**
 * While a rule's control accepts automatically, the sentence that says what that gives (LAND2a, D145 point 5): the
 * person's standing say-so for a push with no press is said where it is given, and with no plugin, that nothing leaves
 * this machine.
 */
export function AcceptingNote({ accepting }: { accepting: Accepting }) {
  const { t } = useTranslation();
  if (!accepting) return null;
  return (
    <p className={cn('m-0 text-small', accepting.plugin ? 'text-ink-soft' : 'text-warn')}>
      <Inline text={accepting.plugin
        ? t('settings.landing.autoAcceptSays', { plugin: accepting.plugin })
        : t('settings.landing.autoAcceptAlone')}
      />
    </p>
  );
}

/**
 * One rule's control: merge or branch, the pattern, who pushes it and whether it accepts automatically when it is a
 * branch, the ticket's key where the pattern says `{ticket}` and the squash on a branch (DOORS1b), each refused by the
 * driver in the terminal's words, and a clear only where a rule is set — the row keeps the clear's room either way, so
 * every row's control sits in one column.
 */
export function LandingField({ name, set, inherited, landers, busy, clearable = true, alignStart = false, onSave, onAccepting }: FieldProps & {
  /** What the draft accepts automatically through, said beneath the row by its owner. */
  onAccepting: (accepting: Accepting) => void;
}) {
  const { t } = useTranslation();
  const start = set ?? inherited;
  const [form, setForm] = useState(start.form);
  const [tidy, setTidy] = useState(Boolean(start.tidy));
  // A choice like the tidy, which starts from what stands, set here or above (LAND2a).
  const [auto, setAuto] = useState(Boolean(start.autoAccept));
  // Only a pattern SET on this row is a value; an inherited one is the placeholder, and must not read
  // as set — in dark the two looked alike on the lines card (seen on the window).
  const [pattern, setPattern] = useState(set?.pattern ?? '');
  // Who pushes is a choice like the form, so it starts from what stands, set here or above (D100).
  const [plugin, setPlugin] = useState(start.plugin ?? NO_PLUGIN);
  // The ticket's key and the squash (DOORS1b) start from what stands, as the plugin does: the field shows them, and a save
  // sends them as shown, so it never drops what the terminal or Ask Daoris set.
  const [key, setKey] = useState(start.key ?? '');
  const [squash, setSquash] = useState(Boolean(start.squash));
  // The answer moves when either door edits the file, and the control follows it.
  useEffect(() => {
    setForm(start.form);
    setTidy(Boolean(start.tidy));
    setPattern(set?.pattern ?? '');
    setPlugin(start.plugin ?? NO_PLUGIN);
    setAuto(Boolean(start.autoAccept));
    setKey(start.key ?? '');
    setSquash(Boolean(start.squash));
  }, [start.form, start.tidy, start.plugin, start.autoAccept, start.key, start.squash, set?.pattern]);

  // What an empty field means: the pattern this row inherits, else the example.
  const fallback = inherited.form === 'branch' && inherited.pattern ? inherited.pattern : EXAMPLE;
  // Only a branch accepts automatically (D145 point 1): a merge would write into the checkout with no press.
  const accepts = form === 'branch' && auto;
  const named = pattern.trim() || fallback;
  // A key is read only for `{ticket}` (PROCESS1b-i); an empty one is the default shape, and takes away one set here.
  const keyed = form === 'branch' && ticketed(named);
  const draft: LandingRule = {
    ...(form === 'branch' ? { form, pattern: named } : MERGE),
    ...(form === 'branch' && plugin !== NO_PLUGIN ? { plugin } : {}),
    ...(tidy ? { tidy: true } : {}),
    ...(accepts ? { autoAccept: true } : {}),
    ...(keyed ? { key: key.trim() } : {}),
    // A branch says its squash whole, off included: a press naming none keeps the one set there (DOORS1b).
    ...(form === 'branch' ? { squash } : {}),
  };
  const through = accepts ? (plugin !== NO_PLUGIN ? plugin : '') : null;
  useEffect(() => {
    onAccepting(through === null ? null : through ? { plugin: through } : {});
  }, [through, onAccepting]);
  const changed = !same(draft, set ?? inherited);
  // The plugins here that land work, and the one the rule names even where it no longer does — the
  // driver's sentence says why, and the chooser must still show what stands.
  const choices = [...new Set([...landers, ...(start.plugin ? [start.plugin] : [])])];

  return (
    <form
      className={cn('flex min-w-0 flex-wrap items-center gap-2', alignStart ? 'justify-start' : 'justify-end')}
      onSubmit={(event) => {
        event.preventDefault();
        if (changed) onSave(draft);
      }}
    >
      <Segmented
        label={t('settings.landing.field', { name })}
        value={form}
        options={[
          { value: 'merge', label: t('settings.landing.form.merge') },
          { value: 'branch', label: t('settings.landing.form.branch') },
        ]}
        onChange={setForm}
      />
      {form === 'branch' && (
        // Gives way to its column: with the side bar open the row's control column was 130px, and a
        // fixed 14rem field spilled left over the row's words (WSR4, seen on the window).
        <input
          aria-label={t('settings.landing.pattern', { name })}
          value={pattern}
          onChange={(event) => setPattern(event.target.value)}
          placeholder={fallback}
          spellCheck={false}
          className="w-56 min-w-0 max-w-full rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint"
        />
      )}
      {keyed && (
        // The ticket's key (PROCESS1b-i), only where the pattern says `{ticket}`; empty is the default shape, its placeholder.
        <input
          aria-label={t('settings.landing.key', { name })}
          value={key}
          onChange={(event) => setKey(event.target.value)}
          placeholder={DEFAULT_KEY}
          spellCheck={false}
          className="w-40 min-w-0 max-w-full rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint"
        />
      )}
      {form === 'branch' && choices.length > 0 && (
        <SelectField
          ariaLabel={t('settings.landing.plugin', { name })}
          value={plugin}
          onChange={setPlugin}
          disabled={busy}
          options={[
            { value: NO_PLUGIN, label: t('settings.landing.pluginNone') },
            ...choices.map((id) => ({ value: id, label: t('settings.landing.pluginNamed', { plugin: id }) })),
          ]}
        />
      )}
      {form === 'branch' && (
        <CheckField
          label={t('settings.landing.autoAccept')}
          checked={auto}
          onChange={setAuto}
          disabled={busy}
          className="text-small"
        />
      )}
      {form === 'branch' && (
        // The pull request's branch as one commit (PROCESS1b-ii): a branch's alone, as a merge makes no branch.
        <CheckField
          label={t('settings.landing.squash')}
          checked={squash}
          onChange={setSquash}
          disabled={busy}
          className="text-small"
        />
      )}
      <CheckField
        label={t('settings.landing.tidy')}
        checked={tidy}
        onChange={setTidy}
        disabled={busy}
        className="text-small"
      />
      <Button type="submit" disabled={busy || !changed}>{t('settings.landing.set')}</Button>
      {clearable && (
        <Button
          variant="ghost"
          disabled={busy || set === undefined}
          aria-hidden={set === undefined}
          tabIndex={set === undefined ? -1 : undefined}
          className={cn(set === undefined && 'invisible @max-[26rem]:hidden')}
          onClick={() => onSave(undefined)}
        >
          {t('settings.landing.clear')}
        </Button>
      )}
    </form>
  );
}
