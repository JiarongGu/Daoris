import { useTranslation } from 'react-i18next';
import { moment } from '../../locales/format';
import { Button, Icon, Inline, Pill, Segmented, SelectField, SettingList, SettingRow, Tip } from '../../design/ui';
import {
  type AccountCooling, type AccountFacts, type AccountScope, type AccountUseChange, type AgentAccounts, coolingLine, heldLine,
  moved, nextLine, nothingSaid, offeredLine, saidLine, switchOf,
} from './accounts';
import { type CurveAccount, CurveChart, CurvePoints } from './UsageCurve';
import { CURVE_WINDOWS, goneOf, ownPoints, type Switch } from './usageCurves';

// How each agent's accounts are used, as its page shows it (TOOL4g; D125 §2.4, §3.7, §6; D130 §9, §16.6; POOL1c, D197 and the
// account-pool design §4.2): the one set's order and its one entry of settings, every workspace's. Molecules: every state
// arrives as props, so a cooling account, a set of six accounts and an agent whose sessions say nothing are each a story. The
// Agents place holds the hooks.

/** An account as a list row names it: its directory, what a person calls it, and whether it is signed in. */
export type AccountChoice = { name: string; label: string; login?: 'in' | 'out' | 'unknown'; keyed?: boolean };

/**
 * One account's facts, on its row (D125 §2.4, D130 §3.2): Daoris's sessions running on it, since when it is offered again
 * where its cool-off ended within the day (TOOL6e), what its agent last said with how long ago (or that it has said nothing
 * yet), the week a limit taught it, and its cool-off with *Try now*. Nothing here counts accounts, and an absent count is
 * unknown, never zero.
 */
export function AccountFactsLines({ facts, label, busy, onTryNow, now }: {
  facts: AccountFacts;
  /** What a person calls the account, for *Try now*'s name. */
  label: string;
  busy?: boolean;
  onTryNow: () => void;
  /** The moment the spans are measured to; now, unless a story's. */
  now?: Date;
}) {
  const { t } = useTranslation();
  const weekly = facts.said?.windows.some((window) => window.window === 'weekly');
  return (
    <span className="flex min-w-0 flex-col gap-0.5">
      {facts.cooling && <CoolingLine cooling={facts.cooling} label={label} busy={busy} onTryNow={onTryNow} now={now} />}
      <span className="text-meta text-ink-faint [overflow-wrap:anywhere]">
        {[
          typeof facts.running === 'number' ? t('harness.running', { count: facts.running }) : null,
          facts.offered && !facts.cooling ? offeredLine(facts.offered) : null,
          saidLine(facts.said),
          // The week a limit taught it, where its agent has not said its week itself (TOOL6b).
          facts.week && !weekly ? t('harness.week', { when: moment(facts.week) }) : null,
        ].filter(Boolean).join(' · ')}
      </span>
    </span>
  );
}

/**
 * An account cooling: until when, how long, why, and *Try now*, which ends it early (D125 §2.3, §6). The tool's own
 * sign-in's row wears it too, where its cool-off lasts.
 */
export function CoolingLine({ cooling, label, busy, onTryNow, now }: {
  cooling: AccountCooling; label: string; busy?: boolean; onTryNow: () => void; now?: Date;
}) {
  const { t } = useTranslation();
  return (
    <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
      <Pill tone="neutral">{t('harness.cooling.pill')}</Pill>
      <span className="text-small text-ink-soft">{coolingLine(cooling, now)}</span>
      <Button
        variant="ghost"
        disabled={busy}
        aria-label={t('harness.cooling.tryNowFor', { account: label })}
        onClick={onTryNow}
      >
        {t('harness.cooling.tryNow')}
      </Button>
    </span>
  );
}

/** Each account's own plan and terms apply (D130 point 12): said once, under the agent's accounts. */
export function TermsLine() {
  const { t } = useTranslation();
  return <p className="m-0 mt-2 text-meta text-ink-faint">{t('harness.terms')}</p>;
}

/**
 * Which account the set's next start takes, in any workspace, and why (TOOL6e; D130 §3–§4, D125 §3.7): the walk's step, since
 * when that account is offered again where its cool-off just ended, what holds every other account, and that the tool's own
 * sign-in carries none of these starts, which is the switch a person makes at their terminal and expects Daoris to follow.
 * Drawn only where the answer carries it, so a shell older than TOOL6e shows nothing rather than a guess.
 */
export function NextStartRow({ agent, product, scope, labelOf, isKey }: {
  agent: AgentAccounts;
  product: string;
  scope: AccountScope;
  labelOf: (name: string) => string;
  /** Whether an account is an API key: a key held signed out is refused, repaired by a new key (ACCTUX1b). */
  isKey?: (name: string) => boolean;
}) {
  const { t } = useTranslation();
  const next = scope.next;
  // The tool's own sign-in is said already, by the list's empty line and D125 §3.7's line beneath the accounts; a third
  // sentence would say it again. Its wait is not said there, so `waits` is drawn.
  if (!next || next.reason === 'own') return null;
  const listed = scope.list.length > 0;
  const facts = next.account ? agent.accounts.find((account) => account.name === next.account) : null;
  const offered = facts?.offered && !facts.cooling ? t('harness.next.offeredSentence', { when: moment(facts.offered) }) : null;
  const held = heldLine(next, labelOf, isKey, scope.near);
  // The goal's step 5 is the most room under the curve, and the week's pace with the switch off (SWITCH1b-iii).
  const walk = scope.use.use === 'order'
    ? 'harness.next.whyOrder'
    : switchOf(scope.use) !== 'off' ? 'harness.next.whyGoal' : 'harness.next.whyGoalOff';
  return (
    <SettingRow
      label={t('harness.next.label')}
      hint={listed ? t('harness.next.hint', { agent: agent.agent }) : undefined}
      why={listed ? t(walk) : undefined}
    >
      <p className="m-0 text-small text-ink">
        {[nextLine(next, scope, labelOf, isKey), offered].filter(Boolean).join(t('harness.said.sentences'))}
      </p>
      {held && <p className="m-0 mt-0.5 text-small text-ink-soft">{held}</p>}
      {/* Wherever the scope names an account, waiting included: a person who switched the tool's own sign-in expects the
          starts to follow it, and none of these does (D130 §3.1). */}
      {scope.begins && <p className="m-0 mt-0.5 text-meta text-ink-faint">{t('harness.next.ownSignIn', { product })}</p>}
    </SettingRow>
  );
}

/** What the set's editor is handed to change it: each press is the terminal's door's twin (D50). */
export type ScopeActs = {
  /** The set's order written whole (`daoris agent profile order`): every account, the first where *One by one, in order* begins. */
  onOrder: (list: string[]) => void;
  /**
   * How the set is used: one setting at a time, a field left out unchanged. `refused`, where handed, is told the door's refusal in
   * the reader's language in place of the toast: the curve's editor says it under the rows it keeps (PUSH1e: the rules are .NET's).
   */
  onUse: (change: AccountUseChange, refused?: (problem: string) => void) => void;
};

/**
 * The set's order and how it is used (D130 §16.6 as D165 §6 amends it; POOL1c, D197 point 2 and the account-pool design §4.2):
 * every account of Daoris's in the order they are used, each with up and down and no *Use* switch, since every account serves
 * every workspace; then *Use accounts*, *Keep for conversations* and *Switch accounts* with each window's curve (SWITCH1d), the
 * one entry of settings every workspace's starts read. Any number of accounts: the editor holds as many rows as the agent has.
 */
export function ScopeEditor({ agent, product, scope, accounts, busy, acts }: {
  agent: AgentAccounts;
  /** What a person calls the tool, for the sentence an agent whose sessions say nothing reads. */
  product: string;
  scope: AccountScope;
  accounts: AccountChoice[];
  busy?: boolean;
  acts: ScopeActs;
}) {
  const { t } = useTranslation();
  const labelOf = (name: string) => accounts.find((account) => account.name === name)?.label ?? name;
  const isKey = (name: string) => accounts.find((account) => account.name === name)?.keyed === true;
  // The order, then any account it does not name yet, as the agent has them: `Change` writes the order whole at every change,
  // so a move writes them all, the one it does not name yet placed where the person sees it.
  const order = [...scope.list, ...accounts.filter((account) => !scope.list.includes(account.name)).map((account) => account.name)];
  const nearOf = (name: string) => scope.near.find((each) => each.account === name);
  const coolingOf = (name: string) => agent.accounts.find((account) => account.name === name)?.cooling;
  // Each hint is the terminal's twin, which names no workspace: one set serves them all.
  const hinted = { agent: agent.agent };
  const listName = t('harness.use.listLabel');

  return (
    <SettingList className="min-w-0">
      {/* First: the question a person comes with is where the next start goes, and why (TOOL6e). */}
      <NextStartRow agent={agent} product={product} scope={scope} labelOf={labelOf} isKey={isKey} />
      <SettingRow
        label={t('harness.use.list')}
        hint={t('harness.use.orderHint', hinted)}
        why={t('harness.use.listWhy')}
      >
        <ol aria-label={listName} className="m-0 mt-1 list-none p-0">
          {order.map((name, index) => {
            const account = accounts.find((each) => each.name === name);
            const near = nearOf(name);
            return (
              <li key={name} className="flex flex-wrap items-center gap-x-2 gap-y-1 border-t border-line py-1.5 first:border-t-0">
                <span className="text-body text-ink [overflow-wrap:anywhere]">{labelOf(name)}</span>
                {scope.begins === name && <Pill tone="neutral">{t('harness.use.begins')}</Pill>}
                {scope.use.keep === name && <Pill tone="neutral">{t('harness.use.kept')}</Pill>}
                {/* What passes it at a start, in the curve's words (SWITCH1d): over its curve, its limit reached, or credits. */}
                {near && (
                  <Tip
                    content={saidLine(agent.accounts.find((each) => each.name === name)?.said, {
                      over: scope.overCurve?.find((each) => each.account === name) ?? null,
                    })}
                  >
                    <span><Pill tone="neutral">{t(`harness.use.passed.${near.by}`)}</Pill></span>
                  </Tip>
                )}
                {coolingOf(name) && <Pill tone="neutral">{t('harness.cooling.pill')}</Pill>}
                {account?.login === 'out' && !account.keyed && <Pill tone="neutral">{t('harness.login.out')}</Pill>}
                {/* Each arrow a target of 28 px at least (the platform language §6, ACCTUX2): its glyph and padding were 25. */}
                <span className="ml-auto flex gap-0.5">
                  <Button
                    variant="ghost"
                    className="min-w-7 justify-center px-1.5"
                    disabled={busy || index === 0}
                    aria-label={`${t('harness.use.up')} · ${labelOf(name)}`}
                    onClick={() => acts.onOrder(moved(order, name, -1))}
                  >
                    <Icon name="chevronUp" size={13} />
                  </Button>
                  <Button
                    variant="ghost"
                    className="min-w-7 justify-center px-1.5"
                    disabled={busy || index === order.length - 1}
                    aria-label={`${t('harness.use.down')} · ${labelOf(name)}`}
                    onClick={() => acts.onOrder(moved(order, name, 1))}
                  >
                    <Icon name="chevronDown" size={13} />
                  </Button>
                </span>
              </li>
            );
          })}
        </ol>
        {/* A hand-edited file read with the order winning (D130 §3.1): a kept account the order does not hold, or held alone. */}
        {scope.problem && scope.problem.kind !== 'default' && (
          <p className="m-0 mt-1.5 border-l-[3px] border-warn bg-page/60 px-3 py-1.5 text-small text-ink-soft">
            {t(`harness.use.problem.${scope.problem.kind}`, { account: labelOf(scope.problem.account) })}
          </p>
        )}
        {scope.unknown.length > 0 && (
          <p className="m-0 mt-1 text-meta text-ink-faint">{t('harness.use.unknown', { settings: scope.unknown.join(', ') })}</p>
        )}
      </SettingRow>

      <SettingRow
        label={t('harness.use.mode.label')}
        hint={t('harness.use.mode.hint', hinted)}
        why={t('harness.use.mode.why')}
        control={(
          <Segmented
            label={t('harness.use.mode.label')}
            value={scope.use.use}
            options={[
              { value: 'goal', label: t('harness.use.mode.goal') },
              { value: 'order', label: t('harness.use.mode.order') },
            ]}
            onChange={(use) => { if (!busy && use !== scope.use.use) acts.onUse({ use }); }}
          />
        )}
      >
        <p className="m-0 text-small text-ink-soft">
          {t(scope.use.use === 'order' ? 'harness.use.mode.orderSays' : 'harness.use.mode.goalSays')}
        </p>
        {nothingSaid(agent, scope) && <p className="m-0 mt-1 text-small text-ink-faint">{t('harness.use.unsaid')}</p>}
      </SettingRow>

      <SettingRow
        label={t('harness.use.keep.label')}
        hint={t('harness.use.keep.hint', hinted)}
        why={t('harness.use.keep.why')}
        control={order.length > 1 ? (
          <SelectField
            value={scope.use.keep ?? ''}
            disabled={busy}
            ariaLabel={t('harness.use.keep.label')}
            placeholder={t('harness.use.keep.none')}
            options={[
              { value: '-', label: t('harness.use.keep.none') },
              ...order.map((name) => ({ value: name, label: labelOf(name) })),
            ]}
            onChange={(value) => acts.onUse(value === '-' ? { noKeep: true } : { keep: value })}
          />
        ) : undefined}
      >
        {order.length < 2 && <p className="m-0 text-small text-ink-faint">{t('harness.use.keep.needsTwo')}</p>}
      </SettingRow>

      <SwitchRow agent={agent} product={product} scope={scope} labelOf={labelOf} busy={busy} acts={acts} />
    </SettingList>
  );
}

/**
 * *Switch accounts* (SWITCH1d; D165 point 2; the switch design §5, §6), the scope's `switch` as `profile use --switch` writes
 * it: *Smart*, Daoris's curve; *My own curve*, the person's points; *Off*, no switch while a session runs. Under it each window's
 * curve in effect, drawn with each account of the list where it stands now; under *My own curve*, each window's points to edit,
 * saved as `--curve` saves them, and *Clear my own curve* as `--no-curve`. The person's curve is kept whatever the switch says,
 * and read only under *My own curve*.
 */
function SwitchRow({ agent, product, scope, labelOf, busy, acts, now = new Date() }: {
  agent: AgentAccounts;
  product: string;
  scope: AccountScope;
  labelOf: (name: string) => string;
  busy?: boolean;
  acts: Pick<ScopeActs, 'onUse'>;
  /** The moment the accounts are placed at; now, unless a story's. */
  now?: Date;
}) {
  const { t } = useTranslation();
  const hinted = { agent: agent.agent };
  const switched = switchOf(scope.use);
  const own = (window: string) => ownPoints(scope.use, window);
  // Each account of the list on a window now, where .NET measured its agent's share against the scope's curve (PUSH1e): placed by
  // when the window began and when it resets, as answered, over it as judged.
  const placed = (window: string): CurveAccount[] => (scope.measured ?? []).flatMap((measured) => {
    if (measured.window !== window || !scope.list.includes(measured.account)) return [];
    const said = agent.accounts.find((account) => account.name === measured.account)?.said?.windows.find((each) => each.window === window);
    const gone = said ? goneOf(said.from, said.reset, now) : null;
    if (!said || typeof said.used !== 'number' || gone === null) return [];
    return [{ name: measured.account, label: labelOf(measured.account), gone, used: said.used * 100, over: measured.over }];
  });
  return (
    <SettingRow
      label={t('harness.use.switch.label')}
      hint={t('harness.use.switch.hint', hinted)}
      why={t('harness.use.switch.why')}
      control={(
        <Segmented
          label={t('harness.use.switch.label')}
          value={switched}
          options={[
            { value: 'smart', label: t('harness.use.switch.smart') },
            { value: 'curve', label: t('harness.use.switch.curve') },
            { value: 'off', label: t('harness.use.switch.off') },
          ]}
          onChange={(to: Switch) => { if (!busy && to !== switched) acts.onUse({ switch: to }); }}
        />
      )}
    >
      <p className="m-0 text-small text-ink-soft">{t(`harness.use.switch.${switched}Says`)}</p>
      {!agent.speaks && <p className="m-0 mt-1 text-small text-ink-faint">{t('harness.use.switch.silent', { product })}</p>}
      <div className="mt-2 grid grid-cols-[repeat(auto-fit,minmax(14rem,1fr))] gap-x-6 gap-y-3">
        {CURVE_WINDOWS.map((window) => (
          <div key={window} className="flex min-w-0 flex-col gap-2">
            <CurveChart
              window={window}
              // The curve in effect, as .NET resolves it (PUSH1e); none drawn from a shell older than that.
              points={scope.curves?.[window] ?? []}
              own={switched === 'curve' && own(window) !== null}
              accounts={placed(window)}
            />
            {switched === 'curve' && (
              <CurvePoints
                // Keyed by what is saved, so a save that lands starts the editor afresh from it.
                key={JSON.stringify(own(window))}
                window={window}
                points={own(window)}
                // Where the person sets none, the curve in effect is Daoris's, which their own begins from.
                start={scope.curves?.[window] ?? []}
                busy={busy}
                hint={t('harness.curve.hint', {
                  ...hinted, window,
                  points: own(window)?.map(([gone, used]) => `${gone}:${used}`).join(',') ?? '<elapsed>:<used>,…',
                })}
                onSave={(points, refused) => acts.onUse({ curve: { [window]: points } }, refused)}
              />
            )}
          </div>
        ))}
      </div>
      {switched === 'curve' && CURVE_WINDOWS.some((window) => own(window) !== null) && (
        <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1">
          <Button variant="ghost" disabled={busy} onClick={() => acts.onUse({ noCurve: true })}>{t('harness.curve.clear')}</Button>
          <span className="text-small text-ink-faint"><Inline text={t('harness.curve.clearHint', hinted)} /></span>
        </div>
      )}
    </SettingRow>
  );
}

