import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { moment } from '../locales/format';
import { shellWord } from '../lib/shellWord';
import { type Answered, InlineConfirm } from '../work/InlineConfirm';
import { Button, CheckField, Icon, Inline, Pill, Prose, Segmented, SelectField, SettingRow, Tip } from '../design/ui';
import {
  type AccountCooling, type AccountFacts, type AccountScope, type AccountUseChange, type AgentAccounts, cannotLeave,
  coolingLine, heldLine, moved, nextLine, nothingSaid, offeredLine, ownLine, saidLine, switchOf, used,
} from './accounts';
import { type CurveAccount, CurveChart, CurvePoints } from './UsageCurve';
import { CURVE_WINDOWS, curveFor, curveNow, elapsed, ownPoints, type Switch } from './usageCurves';

// How each agent's accounts are used, as Settings → Agents shows it (TOOL4g; D125 §2.4, §3.7, §6; D130 §3.2, §9, §16.6).
// Molecules: every state arrives as props, so a cooling account, a scope with six accounts, a workspace on this machine's
// accounts and an agent whose sessions say nothing are each a story. The Agents domain holds the hooks.

/**
 * A scope's `--workspace` as its terminal twins print it, or nothing for this machine's scope. The twin is pasted into
 * whichever shell a person has, so the workspace is spelled for every shell (`shellWord`, ACCTQUOTE1c).
 */
const scopedFlag = (workspace?: string) => (workspace ? ` --workspace ${shellWord(workspace, '<workspace>')}` : '');

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

/**
 * The tool's own sign-in, read-only (D175 point 1): who it is where the tool says, that Daoris starts no session in it and
 * conversations begun there go on there, an old cool-off where it holds one, and the press that signs in an account for Daoris.
 */
export function OwnSignInLine({ who, cooling, signsIn, busy, onSignIn }: {
  who?: string | null;
  cooling?: AccountCooling | null;
  /** Whether the tool has a sign-in flow Daoris can run: no press where it has none. */
  signsIn: boolean;
  busy?: boolean;
  onSignIn: () => void;
}) {
  const { t } = useTranslation();
  return (
    <div className="mt-2 flex flex-wrap items-start gap-2 border-l-[3px] border-accent bg-page/60 px-3 py-2">
      <Prose className="min-w-0 flex-1 basis-72 text-small text-ink-soft">{ownLine(who, cooling)}</Prose>
      {signsIn && (
        <Button disabled={busy} onClick={onSignIn}>
          <Icon name="plus" size={13} />
          {t('harness.profile.signInNew')}
        </Button>
      )}
    </div>
  );
}

/** Each account's own plan and terms apply (D130 point 12): said once, under the agent's accounts. */
export function TermsLine() {
  const { t } = useTranslation();
  return <p className="m-0 mt-2 text-meta text-ink-faint">{t('harness.terms')}</p>;
}

/**
 * Which account the scope's next start takes, and why (TOOL6e; D130 §3–§4, D125 §3.7): the walk's step, since when that
 * account is offered again where its cool-off just ended, what holds every other account, and that the tool's own sign-in
 * carries none of these starts, which is the switch a person makes at their terminal and expects Daoris to follow. Drawn
 * only where the answer carries it, so a shell older than TOOL6e shows nothing rather than a guess.
 */
export function NextStartRow({ agent, product, scope, labelOf, isKey, workspace }: {
  agent: AgentAccounts;
  product: string;
  scope: AccountScope;
  labelOf: (name: string) => string;
  /** Whether an account is an API key: a key held signed out is refused, repaired by a new key (ACCTUX1b). */
  isKey?: (name: string) => boolean;
  workspace?: string;
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
      hint={listed ? t('harness.next.hint', { agent: agent.agent, scoped: scopedFlag(workspace) }) : undefined}
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

/** What a scope's editor is handed to change it: each press is the terminal's door's twin (D50). */
export type ScopeActs = {
  /** A list written whole, in order; none clears it, and its settings with it. */
  onOrder: (workspace: string | null, list: string[]) => void;
  /** How the list is used: one setting at a time, a field left out unchanged. */
  onUse: (workspace: string | null, change: AccountUseChange) => void;
  /** A workspace returned to this machine's accounts: its default, list and settings cleared at once. */
  /** Told how it ended, so the ask stays open while it runs and says a refusal inside it (UXFIX2b3c). */
  onInherit: (workspace: string, answered: Answered) => void;
};

/**
 * One scope's list and how it is used (D130 §3.2, §16.6 as D165 §6 amends it): its accounts in order, each with *Use* and,
 * once used, up and down; then *Use accounts*, *Keep for conversations* and *Switch accounts* with each window's curve
 * (SWITCH1d). The settings come with the list, so a scope with none shows where its starts run instead. Any number of
 * accounts: the editor holds as many rows as the agent has.
 */
export function ScopeEditor({ agent, product, scope, accounts, workspace, busy, acts }: {
  agent: AgentAccounts;
  /** What a person calls the tool, for the sentence an agent whose sessions say nothing reads. */
  product: string;
  scope: AccountScope;
  accounts: AccountChoice[];
  /** The workspace, or absent for this machine's scope. */
  workspace?: string;
  busy?: boolean;
  acts: Pick<ScopeActs, 'onOrder' | 'onUse'>;
}) {
  const { t } = useTranslation();
  const where = workspace ?? null;
  const scopeName = workspace ? t('harness.use.workspace', { workspace }) : t('harness.use.machine');
  const labelOf = (name: string) => accounts.find((account) => account.name === name)?.label ?? name;
  const isKey = (name: string) => accounts.find((account) => account.name === name)?.keyed === true;
  // The list in its order, then every account it does not use, as the agent has them.
  const rows = [
    ...scope.list.map((name) => ({ name, used: true })),
    ...accounts.filter((account) => !scope.list.includes(account.name)).map((account) => ({ name: account.name, used: false })),
  ];
  const listed = scope.list.length > 0;
  const nearOf = (name: string) => scope.near.find((each) => each.account === name);
  const coolingOf = (name: string) => agent.accounts.find((account) => account.name === name)?.cooling;
  // Each hint is the terminal's twin for this scope, so a workspace's names its `--workspace`.
  const hinted = { agent: agent.agent, scoped: scopedFlag(workspace) };

  return (
    <div className="flex min-w-0 flex-col gap-1">
      {/* First: the question a person comes with is where the next start goes, and why (TOOL6e). */}
      <NextStartRow agent={agent} product={product} scope={scope} labelOf={labelOf} isKey={isKey} workspace={workspace} />
      <SettingRow
        label={t('harness.use.list')}
        hint={t('harness.use.orderHint', hinted)}
        why={t('harness.use.listWhy')}
      >
        {/* The switch's name, once, above its column (D130 §9: *Use*), where each row's box is named for its account. */}
        <p className="m-0 mt-1 text-meta text-ink-faint">{t('harness.use.member')}</p>
        <ol aria-label={t('harness.use.listLabel', { scope: scopeName })} className="m-0 mt-1 list-none p-0">
          {rows.map((row, index) => {
            const blocked = row.used ? cannotLeave(scope, row.name) : null;
            const account = accounts.find((each) => each.name === row.name);
            const near = nearOf(row.name);
            const toggle = (
              <CheckField
                checked={row.used}
                disabled={busy || blocked !== null}
                onChange={(on) => acts.onOrder(where, used(scope.list, row.name, on))}
                label={t('harness.use.memberFor', { account: labelOf(row.name), scope: scopeName })}
                hideLabel
              />
            );
            return (
              <li key={row.name} className="flex flex-wrap items-center gap-x-2 gap-y-1 border-t border-line py-1.5 first:border-t-0">
                {blocked ? (
                  <Tip content={t(`harness.use.cannotLeave.${blocked}`, { account: labelOf(row.name) })}>
                    <span>{toggle}</span>
                  </Tip>
                ) : toggle}
                <span className={row.used ? 'text-body text-ink [overflow-wrap:anywhere]' : 'text-body text-ink-faint [overflow-wrap:anywhere]'}>
                  {labelOf(row.name)}
                </span>
                {row.used && scope.begins === row.name && <Pill tone="neutral">{t('harness.use.begins')}</Pill>}
                {row.used && scope.use.keep === row.name && <Pill tone="neutral">{t('harness.use.kept')}</Pill>}
                {/* What passes it at a start, in the curve's words (SWITCH1d): over its curve, its limit reached, or credits. */}
                {row.used && near && (
                  <Tip content={saidLine(agent.accounts.find((each) => each.name === row.name)?.said, { use: scope.use })}>
                    <span><Pill tone="neutral">{t(`harness.use.passed.${near.by}`)}</Pill></span>
                  </Tip>
                )}
                {coolingOf(row.name) && <Pill tone="neutral">{t('harness.cooling.pill')}</Pill>}
                {account?.login === 'out' && !account.keyed && <Pill tone="neutral">{t('harness.login.out')}</Pill>}
                {/* Each arrow a target of 28 px at least (the platform language §6, ACCTUX2): its glyph and padding were 25. */}
                {row.used && (
                  <span className="ml-auto flex gap-0.5">
                    <Button
                      variant="ghost"
                      className="min-w-7 justify-center px-1.5"
                      disabled={busy || index === 0}
                      aria-label={`${t('harness.use.up')} · ${labelOf(row.name)}`}
                      onClick={() => acts.onOrder(where, moved(scope.list, row.name, -1))}
                    >
                      <Icon name="chevronUp" size={13} />
                    </Button>
                    <Button
                      variant="ghost"
                      className="min-w-7 justify-center px-1.5"
                      disabled={busy || index === scope.list.length - 1}
                      aria-label={`${t('harness.use.down')} · ${labelOf(row.name)}`}
                      onClick={() => acts.onOrder(where, moved(scope.list, row.name, 1))}
                    >
                      <Icon name="chevronDown" size={13} />
                    </Button>
                  </span>
                )}
              </li>
            );
          })}
        </ol>
        {!listed && (
          <p className="m-0 mt-1 text-small text-ink-faint">
            {/* Where nothing is named, Daoris's accounts are taken in turn (D175 point 2); only a test double's own home
                runs on the tool's own sign-in, which the answer says by `inherited: false`. */}
            {t('harness.use.empty', {
              fallback: scope.default
                ? labelOf(scope.default)
                : t(scope.inherited === false ? 'harness.use.emptyOwn' : 'harness.use.emptyInherited'),
            })}
          </p>
        )}
        {scope.problem && (
          <p className="m-0 mt-1.5 border-l-[3px] border-warn bg-page/60 px-3 py-1.5 text-small text-ink-soft">
            {t(`harness.use.problem.${scope.problem.kind}`, { account: labelOf(scope.problem.account) })}
          </p>
        )}
        {scope.unknown.length > 0 && (
          <p className="m-0 mt-1 text-meta text-ink-faint">{t('harness.use.unknown', { settings: scope.unknown.join(', ') })}</p>
        )}
      </SettingRow>

      {listed && (
        <>
          <SettingRow
            label={t('harness.use.mode.label')}
            hint={t('harness.use.mode.hint', hinted)}
            why={t('harness.use.mode.why')}
            control={(
              <Segmented
                label={`${t('harness.use.mode.label')} · ${scopeName}`}
                value={scope.use.use}
                options={[
                  { value: 'goal', label: t('harness.use.mode.goal') },
                  { value: 'order', label: t('harness.use.mode.order') },
                ]}
                onChange={(use) => { if (!busy && use !== scope.use.use) acts.onUse(where, { use }); }}
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
            control={scope.list.length > 1 ? (
              <SelectField
                value={scope.use.keep ?? ''}
                disabled={busy}
                ariaLabel={`${t('harness.use.keep.label')} · ${scopeName}`}
                placeholder={t('harness.use.keep.none')}
                options={[
                  { value: '-', label: t('harness.use.keep.none') },
                  ...scope.list.map((name) => ({ value: name, label: labelOf(name) })),
                ]}
                onChange={(value) => acts.onUse(where, value === '-' ? { noKeep: true } : { keep: value })}
              />
            ) : undefined}
          >
            {scope.list.length < 2 && <p className="m-0 text-small text-ink-faint">{t('harness.use.keep.needsTwo')}</p>}
          </SettingRow>

          <SwitchRow agent={agent} product={product} scope={scope} labelOf={labelOf} workspace={workspace} busy={busy} acts={acts} />
        </>
      )}
    </div>
  );
}

/**
 * *Switch accounts* (SWITCH1d; D165 point 2; the switch design §5, §6), the scope's `switch` as `profile use --switch` writes
 * it: *Smart*, Daoris's curve; *My own curve*, the person's points; *Off*, no switch while a session runs. Under it each window's
 * curve in effect, drawn with each account of the list where it stands now; under *My own curve*, each window's points to edit,
 * saved as `--curve` saves them, and *Clear my own curve* as `--no-curve`. The person's curve is kept whatever the switch says,
 * and read only under *My own curve*.
 */
function SwitchRow({ agent, product, scope, labelOf, workspace, busy, acts, now = new Date() }: {
  agent: AgentAccounts;
  product: string;
  scope: AccountScope;
  labelOf: (name: string) => string;
  workspace?: string;
  busy?: boolean;
  acts: Pick<ScopeActs, 'onUse'>;
  /** The moment the accounts are placed at; now, unless a story's. */
  now?: Date;
}) {
  const { t } = useTranslation();
  const where = workspace ?? null;
  const scopeName = workspace ? t('harness.use.workspace', { workspace }) : t('harness.use.machine');
  const hinted = { agent: agent.agent, scoped: scopedFlag(workspace) };
  const switched = switchOf(scope.use);
  const own = (window: string) => ownPoints(scope.use, window);
  // Each account of the list on a window now, where its agent said a share of it.
  const placed = (window: string): CurveAccount[] => scope.list.flatMap((name) => {
    const said = agent.accounts.find((account) => account.name === name)?.said?.windows.find((each) => each.window === window);
    const gone = said ? elapsed(window, said.reset, now) : null;
    const curve = said ? curveNow(scope.use, window, said.reset, now) : null;
    if (!said || typeof said.used !== 'number' || gone === null || curve === null) return [];
    return [{ name, label: labelOf(name), gone, used: said.used * 100, over: curve - said.used * 100 < 0 }];
  });
  return (
    <SettingRow
      label={t('harness.use.switch.label')}
      hint={t('harness.use.switch.hint', hinted)}
      why={t('harness.use.switch.why')}
      control={(
        <Segmented
          label={`${t('harness.use.switch.label')} · ${scopeName}`}
          value={switched}
          options={[
            { value: 'smart', label: t('harness.use.switch.smart') },
            { value: 'curve', label: t('harness.use.switch.curve') },
            { value: 'off', label: t('harness.use.switch.off') },
          ]}
          onChange={(to: Switch) => { if (!busy && to !== switched) acts.onUse(where, { switch: to }); }}
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
              points={curveFor(scope.use, window) ?? []}
              own={switched === 'curve' && own(window) !== null}
              accounts={placed(window)}
            />
            {switched === 'curve' && (
              <CurvePoints
                // Keyed by what is saved, so a save that lands starts the editor afresh from it.
                key={JSON.stringify(own(window))}
                window={window}
                points={own(window)}
                busy={busy}
                hint={t('harness.curve.hint', {
                  ...hinted, window,
                  points: own(window)?.map(([gone, used]) => `${gone}:${used}`).join(',') ?? '<elapsed>:<used>,…',
                })}
                onSave={(points) => acts.onUse(where, { curve: { [window]: points } })}
              />
            )}
          </div>
        ))}
      </div>
      {switched === 'curve' && CURVE_WINDOWS.some((window) => own(window) !== null) && (
        <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1">
          <Button variant="ghost" disabled={busy} onClick={() => acts.onUse(where, { noCurve: true })}>{t('harness.curve.clear')}</Button>
          <span className="text-small text-ink-faint"><Inline text={t('harness.curve.clearHint', hinted)} /></span>
        </div>
      )}
    </SettingRow>
  );
}

/**
 * A workspace's scope (D130 §3.2): *This machine's accounts* or *Its own accounts*. On this machine's, it says where its starts
 * run; on its own, its list and settings are the editor's. Back to this machine's clears its own default, list and settings,
 * so the first press asks.
 */
export function WorkspaceScope({ agent, product, workspace, scope, machine, accounts, busy, acts }: {
  agent: AgentAccounts;
  product: string;
  workspace: string;
  /** Its own scope, or null where it names neither a default nor a list of its own. */
  scope: AccountScope | null;
  /** This machine's scope, which it runs on while it has none of its own. */
  machine: AccountScope;
  accounts: AccountChoice[];
  busy?: boolean;
  acts: ScopeActs;
}) {
  const { t } = useTranslation();
  // Its own accounts chosen before any is used: the editor shows the list empty until the first Use writes it.
  const [opening, setOpening] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const own = scope !== null || opening;
  const labelOf = (name: string) => accounts.find((account) => account.name === name)?.label ?? name;
  const inherits = machine.list.length > 0
    ? t('harness.use.scope.inheritsList', { accounts: machine.list.map(labelOf).join(', ') })
    : machine.default
      ? t('harness.use.scope.inheritsDefault', { account: labelOf(machine.default) })
      : t('harness.use.scope.inheritsOwn');

  return (
    <div className="mt-2 border-t border-line pt-2">
      <SettingRow
        label={t('harness.use.workspace', { workspace })}
        hint={own ? undefined : t('harness.use.scope.hint', { agent: agent.agent, workspace: shellWord(workspace, '<workspace>') })}
        control={(
          <Segmented
            label={`${t('harness.use.scope.label')} · ${workspace}`}
            value={own ? 'own' : 'machine'}
            options={[
              { value: 'machine', label: t('harness.use.scope.machine') },
              { value: 'own', label: t('harness.use.scope.own') },
            ]}
            onChange={(choice) => {
              if (busy) return;
              if (choice === 'own') {
                setConfirming(false);
                setOpening(true);
              } else if (scope === null) {
                setOpening(false);
              } else {
                setConfirming(true);
              }
            }}
          />
        )}
      >
        {!own && <p className="m-0 text-small text-ink-soft">{inherits}</p>}
        {confirming && scope !== null && (
          // UXFIX2b3c: the shared ask stays open while the act runs and says a refusal inside it (ACCTEDIT1's contract).
          <InlineConfirm
            label={t('harness.use.scope.confirmMove')}
            says={<Inline text={t('harness.use.scope.confirm', { workspace, agent: agent.agent })} />}
            meanIt={t('harness.use.scope.confirmMove')}
            busy={busy}
            className="mt-1"
            onConfirm={(answered) => acts.onInherit(workspace, {
              done: () => { setOpening(false); answered.done(); },
              refused: answered.refused,
            })}
            onClose={() => setConfirming(false)}
          />
        )}
      </SettingRow>
      {own && (
        <div className="pl-3">
          <ScopeEditor
            agent={agent}
            product={product}
            scope={scope ?? { workspace, default: null, list: [], begins: null, use: machine.use, unknown: [], problem: null, near: [] }}
            accounts={accounts}
            workspace={workspace}
            busy={busy}
            acts={acts}
          />
        </div>
      )}
    </div>
  );
}
