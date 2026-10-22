import { useTranslation } from 'react-i18next';
import { accountLabel, accountName, accountWho } from '../../agents/agents';
import type { Account } from '../../agents/tools';
import { Icon, Menu } from '../../design/ui';
import { CHIP } from './RepositoryPicker';

/** "Leave it to the driver", as a radio row's value: a row's value is never empty, and a default stays choosable. */
const DEFAULT = '*';

/** What the person chose: an agent (empty is the driver's own) and one of its accounts (empty is the default). */
export type AgentChoice = { adapter: string; profile: string };

/** Each agent's accounts, with their sign-in state (D49 §4), by agent. */
export type AgentAccounts = Readonly<Record<string, Pick<Account, 'name' | 'login' | 'account' | 'displayName' | 'key' | 'org' | 'plan'>[]>>;

/**
 * The agent and account chip (NEW1b, D162; factored out for MIXAGENT1b-i, D163): one chip, and a menu of the agents, then the
 * chosen agent's accounts of Daoris's in the set's order, every workspace's (POOL1c, D197; the account-pool design §4.3), with
 * no group of accounts outside a list. *New conversation* leaves both to the driver by default; *Go on with another agent…*
 * names the agent, so it has no default row (`named`) and lists only the agents handed in.
 *
 * @remarks
 * A molecule: the rosters arrive as props. An account is an agent's own: choosing another agent drops a chosen account the new
 * one does not have. A signed-out account is offered and labelled, since the spawn's refusal names the sign-in, and a refused
 * key is repaired by a new key (ACCTNAME1, D152 §4.2).
 */
export function AgentChip({
  value, onChange, harnesses, labels = {}, names = {}, defaultHarness = '', accounts, orderOf, named = false, disabled = false,
}: {
  value: AgentChoice;
  onChange: (next: AgentChoice) => void;
  /** The agents offered; with `named` false, fewer than two offers no choice. */
  harnesses: readonly string[];
  /** What a person calls each way in, as Settings names it (UX5 U67); absent, its short name, then its id. */
  labels?: Readonly<Record<string, string>>;
  /** Each agent's short name for the chip (*Claude Code*); absent, its label, then its id. */
  names?: Readonly<Record<string, string>>;
  /** The driver's own agent, whose accounts are offered while no other is chosen. */
  defaultHarness?: string;
  accounts: AgentAccounts;
  /** The set's order of this agent's accounts of Daoris's, every workspace's (D197 point 2); null where it was not answered. */
  orderOf?: (harness: string) => readonly string[] | null;
  /** The agent is the person's to name: no *driver's default* row, and the agents are listed even where there is one. */
  named?: boolean;
  disabled?: boolean;
}) {
  const { t } = useTranslation();
  const agent = value.adapter || defaultHarness;
  const label = (id: string) => labels[id] ?? names[id] ?? id;
  const profiles = accounts[agent] ?? [];
  const account = profiles.find((choice) => choice.name === value.profile) ?? null;
  // The set's accounts in its order, then any it does not name yet, as the next change would place them (D197 point 2).
  const order = agent ? orderOf?.(agent) ?? [] : [];
  const ordered = [
    ...order.map((name) => profiles.find((choice) => choice.name === name)).filter((choice) => choice !== undefined),
    ...profiles.filter((choice) => !order.includes(choice.name)),
  ];

  const chooseAgent = (next: string) => {
    const harness = next === DEFAULT ? '' : next;
    // An account is an agent's own: one this agent does not have means nothing here.
    const keeps = (accounts[harness || defaultHarness] ?? []).some((choice) => choice.name === value.profile);
    onChange({ adapter: harness, profile: keeps ? value.profile : '' });
  };

  const agentName = agent ? names[agent] ?? labels[agent] ?? agent : t('work.start.harness');
  // Each account told apart from another of the agent's of one email (ACCOUNTID1b), among the accounts offered here.
  const among = { accounts: profiles };
  const accountShown = account ? accountLabel(account, among) : t('work.start.accountDriver');
  const showAgents = named ? harnesses.length > 0 : harnesses.length > 1;

  return (
    <Menu.Root>
      <Menu.Trigger asChild>
        <button
          type="button"
          disabled={disabled}
          aria-label={t('work.start.chip', { field: t('work.start.agentAccount'), value: `${agentName} · ${accountShown}` })}
          className={CHIP}
        >
          <Icon name="stepWork" size={13} className="shrink-0 text-ink-faint" />
          <span className="min-w-0 truncate">{`${agentName} · ${accountShown}`}</span>
          <Icon name="chevronDown" size={12} className="shrink-0 text-ink-faint" />
        </button>
      </Menu.Trigger>
      <Menu.Content align="start" className="w-80 max-w-[calc(100vw-2rem)]">
        {showAgents && (
          <>
            <Menu.Label>{t('work.start.harness')}</Menu.Label>
            <Menu.RadioGroup aria-label={t('work.start.harness')} value={value.adapter || (named ? '' : DEFAULT)} onValueChange={chooseAgent}>
              {/* Which it is, where the machine has one (UX5 U67): *this machine's default* said nothing of what a start
                  would run on. A hand-over has none: the agent it goes on with is the person's to name. */}
              {!named && (
                <Menu.RadioItem value={DEFAULT}>
                  {defaultHarness
                    ? t('work.start.harnessDefaultNamed', { name: label(defaultHarness) })
                    : t('work.start.harnessDefault')}
                </Menu.RadioItem>
              )}
              {harnesses.map((harness) => <Menu.RadioItem key={harness} value={harness}>{label(harness)}</Menu.RadioItem>)}
            </Menu.RadioGroup>
          </>
        )}
        {showAgents && profiles.length > 0 && <Menu.Separator />}
        {profiles.length > 0 && (
          <>
            <Menu.Label>{t('work.start.profile')}</Menu.Label>
            <Menu.RadioGroup
              aria-label={t('work.start.profile')}
              value={value.profile || DEFAULT}
              onValueChange={(next) => onChange({ adapter: value.adapter, profile: next === DEFAULT ? '' : next })}
            >
              <Menu.RadioItem value={DEFAULT}>{t('work.start.profileDefault')}</Menu.RadioItem>
              {ordered.map((choice) => {
                const who = accountWho(choice, among);
                const name = who ? `${accountName(choice)} · ${who}` : accountName(choice);
                return (
                  <Menu.RadioItem key={choice.name} value={choice.name}>
                    {choice.login === 'out' ? t(choice.key ? 'harness.profileRefused' : 'harness.profileOut', { name }) : name}
                  </Menu.RadioItem>
                );
              })}
            </Menu.RadioGroup>
          </>
        )}
        {!showAgents && profiles.length === 0 && (
          <Menu.Label>{t('work.start.harnessDefaultNamed', { name: agentName })}</Menu.Label>
        )}
      </Menu.Content>
    </Menu.Root>
  );
}
