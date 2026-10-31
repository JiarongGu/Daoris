import type { TFunction } from 'i18next';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import { Button, CheckField, Inline, Segmented, SelectField } from '../../design/ui';
import { Refused } from '../../work/InlineConfirm';

/**
 * How a session's work lands (WSR1, D87): merged into the line, or put on a branch the pattern names —
 * whether its tree and branch go once a press lands it (D88), for a branch the plugin that pushes
 * it and opens the pull request (WSR4, D100), whether a quest's done lands it with no press (LAND2a, D145), the
 * shape of the ticket key its pattern's `{ticket}` is found by (PROCESS1b-i), and whether Accept squashes the pull
 * request's branch to one commit (PROCESS1b-ii). The driver answers `squash` false, and `key` absent where none is set.
 */
export type LandingRule = {
  form: string; pattern?: string; tidy?: boolean; plugin?: string; autoAccept?: boolean; key?: string | null; squash?: boolean;
  integrate?: LandingIntegrate | null;
};

/**
 * A branch rule's `integrate` (AGENTSPLACE1d; the integration design §4.1): each key as the file writes it, null or absent where unset
 * (the driver reads its defaults). Its values are `LandingKey`'s to judge, never the page's: a refusal is shown under *Save*.
 */
export type LandingIntegrate = {
  request?: string | null; commit?: string | null; batch?: number | string | null; order?: string | null; gates?: string | null;
  unconfirmed?: { unreached?: string | null; reached?: string | null } | null;
  conflicts?: string | null; overlap?: string | null; line?: string | null;
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
  /** An object sets it, null withdraws it, and a press naming none keeps the rule's. */
  integrate?: LandingIntegrate | null;
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

/** The integrate keys in the order the file writes them, set ones only, so two spellings of one policy compare equal. */
const integrateKey = (integrate?: LandingIntegrate | null) => {
  if (!integrate) return '';
  const { unreached, reached } = integrate.unconfirmed ?? {};
  const keys = [integrate.request, integrate.commit, integrate.batch, integrate.order, integrate.gates, unreached, reached,
    integrate.conflicts, integrate.overlap, integrate.line];
  return JSON.stringify(keys.map((value) => (value === null || value === undefined || value === '' ? null : String(value))));
};

const same = (a?: LandingRule, b?: LandingRule) =>
  a?.form === b?.form
  && (a?.form !== 'branch' || (a?.pattern === b?.pattern && a?.plugin === b?.plugin && Boolean(a?.autoAccept) === Boolean(b?.autoAccept)
    && (a?.key ?? '') === (b?.key ?? '') && Boolean(a?.squash) === Boolean(b?.squash) && integrateKey(a?.integrate) === integrateKey(b?.integrate)))
  && Boolean(a?.tidy) === Boolean(b?.tidy);

/** The values each integrate choice offers, in the order `LandingKey` lists them; the first option of each is "the default". */
const INTEGRATE_CHOICES = {
  request: ['owner', 'done', 'steward'],
  commit: ['merge', 'rebase', 'squash'],
  order: ['arrival', 'apart'],
  gates: ['declared', 'none'],
  unreached: ['split', 'hold', 'land'],
  reached: ['split', 'hold', 'land'],
  conflicts: ['session', 'hold'],
  overlap: ['warn', 'wait'],
} as const;

type IntegrateChoice = keyof typeof INTEGRATE_CHOICES;

/** The integrate choices as the draft holds them: text, empty where unset. */
type IntegrateDraft = Record<IntegrateChoice | 'batch' | 'line', string>;

const integrateDraft = (integrate?: LandingIntegrate | null): IntegrateDraft => ({
  request: integrate?.request ?? '', commit: integrate?.commit ?? '', batch: integrate?.batch == null ? '' : String(integrate.batch),
  order: integrate?.order ?? '', gates: integrate?.gates ?? '',
  unreached: integrate?.unconfirmed?.unreached ?? '', reached: integrate?.unconfirmed?.reached ?? '',
  conflicts: integrate?.conflicts ?? '', overlap: integrate?.overlap ?? '', line: integrate?.line ?? '',
});

/** The draft as a press sends it: only the keys set, the batch a number where it reads as one, anything else left for the driver to refuse. */
const integrateOf = (draft: IntegrateDraft): LandingIntegrate => {
  const batch = draft.batch.trim();
  return {
    ...(draft.request ? { request: draft.request } : {}),
    ...(draft.commit ? { commit: draft.commit } : {}),
    ...(batch ? { batch: Number.isNaN(Number(batch)) ? batch : Number(batch) } : {}),
    ...(draft.order ? { order: draft.order } : {}),
    ...(draft.gates ? { gates: draft.gates } : {}),
    ...(draft.unreached || draft.reached
      ? { unconfirmed: { ...(draft.unreached ? { unreached: draft.unreached } : {}), ...(draft.reached ? { reached: draft.reached } : {}) } }
      : {}),
    ...(draft.conflicts ? { conflicts: draft.conflicts } : {}),
    ...(draft.overlap ? { overlap: draft.overlap } : {}),
    ...(draft.line.trim() ? { line: draft.line.trim() } : {}),
  };
};

/** What a rule's integrate says in a line: its batch, order and gates as the design's §3 shows, "batch 4, oldest first, gates declared". */
export function landingIntegrateSays(t: TFunction, rule: LandingRule): string[] {
  if (rule.form !== 'branch' || !rule.integrate) return [];
  const { batch, order, gates } = rule.integrate;
  const parts = [
    ...(batch == null || batch === '' ? [] : [t('settings.landing.integrate.says.batch', { count: Number(batch) })]),
    ...(order ? [t(`settings.landing.integrate.says.order.${order}`, { defaultValue: order })] : []),
    ...(gates ? [t(`settings.landing.integrate.says.gates.${gates}`, { defaultValue: gates })] : []),
  ];
  return [parts.length > 0 ? t('settings.landing.integrate.says', { parts: parts.join(t('settings.landing.integrate.join')) }) : t('settings.landing.integrate.saysPlain')];
}

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
    ...landingIntegrateSays(t, rule),
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
  onSave: (rule?: LandingRule) => void;
  /** The driver's sentence for the last save it refused, shown under *Save* (AGENTSPLACE1d): the page judges nothing of its own. */
  refusal?: string | null;
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
 * every row's control sits in one column. Laid out from the row's start, where the value column begins (R1, UIFIT1).
 */
export function LandingField({ name, set, inherited, landers, busy, clearable = true, onSave, onAccepting, refusal }: FieldProps & {
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
  // The integrate policy (AGENTSPLACE1d) starts from what stands: on when the rule carries one, each choice as it is set.
  const [integrating, setIntegrating] = useState(Boolean(start.integrate));
  const [integrate, setIntegrate] = useState(() => integrateDraft(start.integrate));
  const standing = integrateKey(start.integrate);
  // The answer moves when either door edits the file, and the control follows it.
  useEffect(() => {
    setForm(start.form);
    setTidy(Boolean(start.tidy));
    setPattern(set?.pattern ?? '');
    setPlugin(start.plugin ?? NO_PLUGIN);
    setAuto(Boolean(start.autoAccept));
    setKey(start.key ?? '');
    setSquash(Boolean(start.squash));
    setIntegrating(Boolean(start.integrate));
    setIntegrate(integrateDraft(start.integrate));
    // eslint-disable-next-line react-hooks/exhaustive-deps -- `standing` stands for start.integrate by value
  }, [start.form, start.tidy, start.plugin, start.autoAccept, start.key, start.squash, set?.pattern, standing]);

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
    // Set where ticked; withdrawn (null) where it stood and is unticked; a press naming none keeps the rule's.
    ...(form === 'branch' && integrating ? { integrate: integrateOf(integrate) } : {}),
    ...(form === 'branch' && !integrating && start.integrate ? { integrate: null } : {}),
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
      className="flex min-w-0 flex-wrap items-center justify-start gap-2"
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
      {form === 'branch' && (
        // The line takes the branch through the integration queue (AGENTSPLACE1d): a branch's alone, as a merge makes no candidate.
        <CheckField
          label={t('settings.landing.integrate')}
          checked={integrating}
          onChange={setIntegrating}
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
      {form === 'branch' && integrating && (
        <div role="group" aria-label={t('settings.landing.integrate.group', { name })} className="flex min-w-0 basis-full flex-wrap items-center gap-2">
          {(Object.keys(INTEGRATE_CHOICES) as IntegrateChoice[]).map((choice) => (
            <SelectField
              key={choice}
              ariaLabel={t(`settings.landing.integrate.${choice}`, { name })}
              value={integrate[choice] || NO_PLUGIN}
              onChange={(value) => setIntegrate({ ...integrate, [choice]: value === NO_PLUGIN ? '' : value })}
              disabled={busy}
              options={[
                { value: NO_PLUGIN, label: t('settings.landing.integrate.choice.default') },
                ...INTEGRATE_CHOICES[choice].map((value) => ({ value, label: t(`settings.landing.integrate.choice.${choice}.${value}`) })),
              ]}
            />
          ))}
          <input
            aria-label={t('settings.landing.integrate.batch', { name })}
            value={integrate.batch}
            onChange={(event) => setIntegrate({ ...integrate, batch: event.target.value })}
            placeholder={t('settings.landing.integrate.batchHint')}
            inputMode="numeric"
            spellCheck={false}
            className="w-24 min-w-0 max-w-full rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint"
          />
          <input
            aria-label={t('settings.landing.integrate.line', { name })}
            value={integrate.line}
            onChange={(event) => setIntegrate({ ...integrate, line: event.target.value })}
            placeholder={t('settings.landing.integrate.lineHint')}
            spellCheck={false}
            className="w-40 min-w-0 max-w-full rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint"
          />
        </div>
      )}
      <Button type="submit" disabled={busy || !changed}>{t('settings.landing.set')}</Button>
      {clearable && (
        <Button
          variant="ghost"
          disabled={busy || set === undefined}
          aria-hidden={set === undefined}
          tabIndex={set === undefined ? -1 : undefined}
          className={cn(set === undefined && 'invisible @max-[32rem]:hidden')}
          onClick={() => onSave(undefined)}
        >
          {t('settings.landing.clear')}
        </Button>
      )}
      {refusal && <Refused sentence={refusal} />}
    </form>
  );
}
