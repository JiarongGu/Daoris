import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { moment } from '../format';
import { Button, CheckField, Icon, Inline, Pill, Prose, Segmented, SelectField, SettingRow, Tip } from '../ui';
import {
  type AccountCooling, type AccountFacts, type AccountScope, type AccountUseChange, type AgentAccounts, cannotLeave,
  coolingLine, heldLine, moved, NEAR_RANGE, nextLine, nothingSaid, offeredLine, ownLine, saidLine, used,
} from './accounts';

// How each agent's accounts are used, as Settings → Agents shows it (TOOL4g; D125 §2.4, §3.7, §6; D130 §3.2, §9, §16.6).
// Molecules: every state arrives as props, so a cooling account, a scope with six accounts, a workspace on this machine's
// accounts and an agent whose sessions say nothing are each a story. The Agents domain holds the hooks.

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
 * The tool's own sign-in, said while a start would run on it (D125 §3.7): who it is where the tool says, that it moves with
 * the person's terminal and no record can say which account ran, its cool-off where it cools, and the press that gives
 * Daoris accounts of its own.
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
export function NextStartRow({ agent, product, scope, labelOf, workspace }: {
  agent: AgentAccounts;
  product: string;
  scope: AccountScope;
  labelOf: (name: string) => string;
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
  const held = heldLine(next, labelOf);
  return (
    <SettingRow
      label={t('harness.next.label')}
      hint={listed ? t('harness.next.hint', { agent: agent.agent, scoped: workspace ? ` --workspace ${workspace}` : '' }) : undefined}
      why={listed ? t(scope.use.use === 'order' ? 'harness.next.whyOrder' : 'harness.next.whyGoal') : undefined}
    >
      <p className="m-0 text-small text-ink">
        {[nextLine(next, scope, labelOf), offered].filter(Boolean).join(t('harness.said.sentences'))}
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
  onInherit: (workspace: string) => void;
};

/**
 * One scope's list and how it is used (D130 §3.2, §16.6): its accounts in order, each with *Use* and, once used, up and
 * down; then *Use accounts*, *Keep for conversations* and *Switch before the limit* with its *near*. The settings come with
 * the list, so a scope with none shows where its starts run instead. Any number of accounts: the editor holds as many rows
 * as the agent has.
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
  // The list in its order, then every account it does not use, as the agent has them.
  const rows = [
    ...scope.list.map((name) => ({ name, used: true })),
    ...accounts.filter((account) => !scope.list.includes(account.name)).map((account) => ({ name: account.name, used: false })),
  ];
  const listed = scope.list.length > 0;
  const nearOf = (name: string) => scope.near.find((each) => each.account === name);
  const coolingOf = (name: string) => agent.accounts.find((account) => account.name === name)?.cooling;
  // Each hint is the terminal's twin for this scope, so a workspace's names its `--workspace`.
  const hinted = { agent: agent.agent, scoped: workspace ? ` --workspace ${workspace}` : '' };

  return (
    <div className="flex min-w-0 flex-col gap-1">
      {/* First: the question a person comes with is where the next start goes, and why (TOOL6e). */}
      <NextStartRow agent={agent} product={product} scope={scope} labelOf={labelOf} workspace={workspace} />
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
                {row.used && near && (
                  <Tip content={saidLine(agent.accounts.find((each) => each.name === row.name)?.said)}>
                    <span><Pill tone="neutral">{t('harness.use.near')}</Pill></span>
                  </Tip>
                )}
                {coolingOf(row.name) && <Pill tone="neutral">{t('harness.cooling.pill')}</Pill>}
                {account?.login === 'out' && !account.keyed && <Pill tone="neutral">{t('harness.login.out')}</Pill>}
                {row.used && (
                  <span className="ml-auto flex gap-0.5">
                    <Button
                      variant="ghost"
                      className="px-1.5"
                      disabled={busy || index === 0}
                      aria-label={`${t('harness.use.up')} · ${labelOf(row.name)}`}
                      onClick={() => acts.onOrder(where, moved(scope.list, row.name, -1))}
                    >
                      <Icon name="chevronUp" size={13} />
                    </Button>
                    <Button
                      variant="ghost"
                      className="px-1.5"
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
            {t('harness.use.empty', { fallback: scope.default ? labelOf(scope.default) : t('harness.use.emptyOwn') })}
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

          <SettingRow
            label={t('harness.use.early.label')}
            hint={t('harness.use.early.hint', hinted)}
            why={t('harness.use.early.why')}
            control={(
              <CheckField
                hideLabel
                checked={scope.use.early}
                disabled={busy}
                onChange={(early) => acts.onUse(where, { early })}
                label={`${t('harness.use.early.label')} · ${scopeName}`}
              />
            )}
          >
            {!agent.speaks && (
              <p className="m-0 text-small text-ink-faint">{t('harness.use.early.silent', { product })}</p>
            )}
          </SettingRow>

          {scope.use.early && (
            <SettingRow
              label={t('harness.use.near.label')}
              hint={t('harness.use.near.hint', hinted)}
              control={<NearField near={scope.use.near} busy={busy} label={`${t('harness.use.near.label')} · ${scopeName}`} onNear={(near) => acts.onUse(where, { near })} />}
            />
          )}
        </>
      )}
    </div>
  );
}

/**
 * *Near*'s percent, held as text while typed: a number passing through `5` on its way to `55` is not written, and a value
 * outside 50–99 or the one it holds puts the held value back (REV3's rule for the strikes).
 */
function NearField({ near, busy, label, onNear }: { near: number; busy?: boolean; label: string; onNear: (near: number) => void }) {
  const [typed, setTyped] = useState<string | null>(null);
  return (
    <span className="inline-flex items-center gap-1">
      <input
        type="number"
        min={NEAR_RANGE.lowest}
        max={NEAR_RANGE.highest}
        aria-label={label}
        disabled={busy}
        value={typed ?? String(near)}
        onChange={(event) => setTyped(event.target.value)}
        onBlur={() => {
          const value = typed === null || typed.trim() === '' ? near : Number(typed);
          setTyped(null);
          if (!Number.isInteger(value) || value < NEAR_RANGE.lowest || value > NEAR_RANGE.highest || value === near) return;
          onNear(value);
        }}
        className="w-[4.5rem] rounded-control border border-line-strong bg-raised px-2.5 py-1 text-right text-body text-ink"
      />
      <span className="text-small text-ink-soft">%</span>
    </span>
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
        hint={own ? undefined : t('harness.use.scope.hint', { agent: agent.agent, workspace })}
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
          <div
            role="group"
            aria-label={t('harness.use.scope.confirmMove')}
            className="mt-1 flex flex-wrap items-center gap-2 rounded-control border border-line bg-sunken px-2.5 py-2"
          >
            <span className="min-w-0 flex-1 basis-64 text-small text-ink-soft">
              <Inline text={t('harness.use.scope.confirm', { workspace, agent: agent.agent })} />
            </span>
            <Button
              variant="danger"
              disabled={busy}
              onClick={() => {
                setConfirming(false);
                setOpening(false);
                acts.onInherit(workspace);
              }}
            >
              {t('harness.use.scope.confirmMove')}
            </Button>
            <Button variant="ghost" onClick={() => setConfirming(false)}>{t('common.cancel')}</Button>
          </div>
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
