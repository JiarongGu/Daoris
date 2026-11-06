import type * as React from 'react';
import { figure } from '../../locales/format';
import type { Tool } from '../tools';
import type { UsedBy } from '../roles/roleStructure';
import { UsedLine } from '../roles/StructureSections';
import { Button, Chip, Prose, SkeletonRows, Tip } from '../../design/ui';
import { type Answered, Refused } from '../../work/InlineConfirm';
import { AccountSettingsForm, AccountSettingsSummary } from '../../settings/accounts/AccountSettings';
import { AgentRules, type AgentRulesState } from '../../settings/repository/AgentRules';
import { type AgentUsage, rulesSummary, settingsSummary, usageSummary } from '../agents';
import { type TFunction } from 'i18next';
import { Fold } from './Fold';
import type { AgentActs, AgentSection, Question } from '../AgentPage';

export function AgentMore({ rulesShown, t, rules, open, toggle, busy, acts, settingsShown, tool, labelOf, tuning, askSettings, lead, settingsBusy, answered, setTuning, refusalOf, sessions, usage, ownLabel, usedBy, product }: {
  rulesShown: boolean;
  t: TFunction<"translation", undefined>;
  rules: AgentRulesState | null;
  open: ReadonlySet<AgentSection>;
  toggle: (section: AgentSection) => void;
  busy: boolean;
  acts: AgentActs;
  settingsShown: boolean;
  tool: Tool;
  labelOf: (name: string) => string;
  tuning: string | null;
  askSettings: (account: string | null) => void;
  lead: string;
  settingsBusy: boolean;
  answered: (question: Question, account: string, close?: () => void) => Answered;
  setTuning: React.Dispatch<React.SetStateAction<string | null>>;
  refusalOf: (question: Question, account: string) => string | null;
  sessions: number;
  usage: AgentUsage[] | null;
  ownLabel: string;
  usedBy: { read: "ok" | "loading" | "failed"; rows: UsedBy[]; } | null;
  product: string;
}) {
  return (
    <>
{rulesShown && (
        <Fold section="rules" title={t('agents.section.rules')} summary={rulesSummary(rules!)} open={open.has('rules')} onToggle={toggle}>
          <AgentRules
            rules={rules!}
            part="agent"
            circles={[]}
            repositories={[]}
            busy={busy}
            onSwitchDefault={acts.rules.onSwitchDefault}
            onRemove={acts.rules.onRemove}
            onAdd={acts.rules.onAdd}
            onAnswer={acts.rules.onAnswer}
          />
        </Fold>
      )}

      {/* Each account's own model and effort (AGT6, D98), only where Daoris knows the tool's settings. */}
      {settingsShown && (
        <Fold section="settings" title={t('agents.section.settings')} summary={settingsSummary(tool)} open={open.has('settings')} onToggle={toggle}>
          {/* What an account's own model means now actors choose models (D205 §6.4, MODELROLE1c2): their *account default*. */}
          <p className="m-0 pb-1.5 text-meta text-ink-soft">{t('agents.settings.roles')}</p>
          <ul className="m-0 list-none p-0">
            {tool.accounts.filter((account) => account.settings).map((account) => (
              <li key={account.home} className="flex flex-wrap items-center gap-x-3 gap-y-1 border-t border-line py-1.5 first:border-t-0">
                <span className="min-w-0 text-body text-ink [overflow-wrap:anywhere]">{labelOf(account.name)}</span>
                <AccountSettingsSummary settings={account.settings!} />
                {tuning !== account.name && (
                  <Button variant="ghost" className="ml-auto" disabled={busy} onClick={() => askSettings(account.name)}>
                    {t('harness.settings.open')}
                  </Button>
                )}
                {/* ACCTUX3: open until the save lands, never closed on the press, so a refused one keeps the choice made and
                    says why under it (ACCTEDIT1's contract). The form holds its own *Never mind* while it saves
                    (SETTINGSWAIT1), so a refusal is never said to nobody. */}
                {tuning === account.name && (
                  <AccountSettingsForm
                    harness={lead}
                    account={account.name}
                    accountLabel={labelOf(account.name)}
                    settings={account.settings!}
                    choices={tool.settingsChoices!}
                    busy={settingsBusy}
                    onSave={(change) => acts.onSaveSettings(account.name, labelOf(account.name), change, answered('settings', account.name, () => {
                      setTuning((was) => (was === account.name ? null : was));
                    }))}
                    onCancel={() => askSettings(null)}
                  />
                )}
                {tuning === account.name && refusalOf('settings', account.name) && (
                  <Refused sentence={refusalOf('settings', account.name)!} />
                )}
              </li>
            ))}
          </ul>
        </Fold>
      )}

      {/* What each account has carried (TOOL3, D57 §4): measured, never priced, and nothing measured said so; an agent not
          installed has carried nothing here, so it has no usage to fold (§5.1, D152 §4.6). */}
      {(tool.present || sessions > 0) && (
        <Fold section="usage" title={t('usage.title')} summary={usage === null ? t('agents.usage.reading') : usageSummary(sessions)} open={open.has('usage')} onToggle={toggle}>
          <Prose className="text-small">{t('usage.body')}</Prose>
          {usage === null && <SkeletonRows rows={2} />}
          {usage !== null && usage.length > 0 && (
            <ul className="m-0 mt-2 list-none p-0">
              {usage.map((account) => {
                const called = account.profile ? labelOf(account.profile) : ownLabel;
                return (
                  <li key={`${account.harness}:${account.profile ?? ''}`} className="flex flex-wrap items-baseline gap-3 border-t border-line py-1.5 first:border-t-0">
                    <span className="font-mono text-small">{account.harness}</span>
                    <Chip>{called}</Chip>
                    <span className="text-small text-ink-soft">{t('usage.sessions', { count: account.sessions })}</span>
                    <Tip content={t('usage.contextTip')}>
                      <span className="ml-auto font-mono text-small text-ink-faint">{t('usage.context', { used: figure(account.used) })}</span>
                    </Tip>
                  </li>
                );
              })}
            </ul>
          )}
          <p className="m-0 mt-2 text-meta text-ink-faint">{t('usage.note')}</p>
        </Fold>
      )}

      {/* The roles that lean on it (COMPANYVIEW1d): read from the structure's last readings, so opening the page asks no account. */}
      {usedBy && (
        <Fold
          section="used" title={t('agents.roles.used.title')} open={open.has('used')} onToggle={toggle}
          summary={usedBy.read === 'failed' ? t('agents.roles.used.failed') : usedBy.read === 'loading' ? t('agents.roles.used.reading')
            : usedBy.rows.length === 0 ? t('agents.roles.used.none') : t('agents.roles.used.summary', { count: usedBy.rows.length })}
        >
          <Prose className="text-small">{t('agents.roles.used.body')}</Prose>
          {usedBy.read === 'failed' && <p role="alert" className="m-0 mt-2 text-small text-ink-soft">{t('agents.roles.used.failed')}</p>}
          {usedBy.read === 'ok' && usedBy.rows.length === 0 && <p className="m-0 mt-2 text-small text-ink-soft">{t('agents.roles.used.none')}</p>}
          {usedBy.read === 'ok' && usedBy.rows.length > 0 && (
            <ul aria-label={t('agents.roles.used.title')} className="m-0 mt-2 list-none p-0">
              {usedBy.rows.map((used) => (
                <li key={`${used.role.id}:${used.from}:${used.model ?? ''}`} className="border-t border-line py-1.5 text-small first:border-t-0">
                  <UsedLine used={used} product={product} />
                </li>
              ))}
            </ul>
          )}
        </Fold>
      )}

      {/* The two doors stay in sight (D50): the agent's id is said here, where the terminal types it; an agent not installed
          has one door to name, its installer. */}
    </>
  );
}
