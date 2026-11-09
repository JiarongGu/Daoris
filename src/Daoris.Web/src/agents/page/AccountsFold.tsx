import type * as React from 'react';
import { clockOf } from '../../locales/format';
import type { Account, Tool } from '../tools';
import { Button, Icon, SectionTitle, Tip } from '../../design/ui';
import { type Answered, Refused } from '../../work/InlineConfirm';
import { type AccountScope, type AgentAccounts, ownLine, type PoolReceipt } from '../../settings/accounts/accounts';
import { type AccountChoice, ScopeEditor } from '../../settings/accounts/AccountUse';
import { ACCOUNT_COLUMNS, AccountColumnsHead, AccountRow } from '../AccountRow';
import { type BackAccount, NameAccount, ReSignIn } from '../AddAccount';
import { pooledLines, useSummary, type NextStartSaid } from '../agents';
import { type TFunction } from 'i18next';
import { Fold } from './Fold';
import type { AgentActs, AgentSection, Question } from '../AgentPage';

export function AccountsFold({ adding, goingBack, signInNewPanel, added, back, busy, now, setGoingBack, acts, tool, t, addedKey, addedWho, offeredName, addedFallback, refusalOf, answered, latest, use, product, readingNow, reading, pooled, labelOf, nextSaid, accountRow, ownLabel, firstPress, termsOpen, setTermsOpen, keying, addKey, keyDraft, setKeyDraft, askKey, machine, open, toggle, choices }: {
  adding: boolean;
  goingBack: boolean;
  signInNewPanel: React.ReactNode;
  added: { account: string; who: string | null; key?: string | null | undefined; offered?: string | null | undefined; } | null;
  back: BackAccount[];
  busy: boolean;
  now: Date | undefined;
  setGoingBack: React.Dispatch<React.SetStateAction<boolean>>;
  acts: AgentActs;
  tool: Tool;
  t: TFunction<"translation", undefined>;
  addedKey: string | null;
  addedWho: string | null;
  offeredName: string;
  addedFallback: string;
  refusalOf: (question: Question, account: string) => string | null;
  answered: (question: Question, account: string, close?: () => void) => Answered;
  latest: string | null;
  use: AgentAccounts | null;
  product: string;
  readingNow: boolean;
  reading: boolean;
  pooled: PoolReceipt[];
  labelOf: (name: string) => string;
  nextSaid: NextStartSaid | null;
  accountRow: (account: Account) => React.JSX.Element;
  ownLabel: string;
  firstPress: string | null;
  termsOpen: boolean;
  setTermsOpen: React.Dispatch<React.SetStateAction<boolean>>;
  keying: boolean;
  addKey: () => void;
  keyDraft: string;
  setKeyDraft: React.Dispatch<React.SetStateAction<string>>;
  askKey: (open: boolean) => void;
  machine: AccountScope | null;
  open: ReadonlySet<AgentSection>;
  toggle: (section: AgentSection) => void;
  choices: AccountChoice[];
}) {
  const said = nextSaid && tool.accounts.length === 0 && firstPress ? (nextSaid.doors.length > 0 ? { ...nextSaid, takes: '' } : null) : nextSaid;
  return (
    <>
{adding && (
        <div className="mb-4 grid gap-2">
          {goingBack && !signInNewPanel && !added && (
            <ReSignIn
              accounts={back}
              busy={busy}
              now={now}
              onSignIn={(id) => { setGoingBack(false); acts.onSignIn(id); }}
              onNew={() => { setGoingBack(false); acts.onSignInNew(); }}
              onClose={() => setGoingBack(false)}
            />
          )}
          {signInNewPanel}
          {added && (
            <NameAccount
              key={added.account}
              agent={tool.name}
              account={added.account}
              title={t(addedKey ? 'harness.profile.addKey' : 'agents.add.title')}
              signedIn={addedKey ? undefined : addedWho}
              lead={addedKey ? t('agents.add.keyAdded', { handle: addedKey }) : undefined}
              offered={offeredName}
              fallback={addedFallback}
              busy={busy}
              refusal={refusalOf('added', added.account)}
              // The organism closes this step once the name lands (`added` goes); a refusal is said in it.
              onDone={(answer) => acts.onAddedAnswer(answer, answered('added', added.account))}
            />
          )}
        </div>
      )}

      {/* The accounts: one list, whatever doors reach the agent, a row each. Never folded;
          an agent not installed has none until it is, and no list head stands over nothing. */}
      {(tool.present || tool.accounts.length > 0) && (
        <section id="agents-accounts" aria-label={t('harness.accounts')} className="scroll-mt-3">
          <div className="mb-1 flex flex-wrap items-baseline gap-x-3 gap-y-1">
            <SectionTitle>{t('harness.accounts')}</SectionTitle>
            <span className="ml-auto flex items-baseline gap-2 text-meta text-ink-faint">
              {latest && <span>{t('agents.read.at', { when: clockOf(latest, now) })}</span>}
              {/* Each account's use, asked of its agent with no prompt: no sign-in, so beside *Read again*. */}
              {tool.present && use?.reads && (
                <Tip content={t('agents.read.nowTip', { agent: product })}>
                  <Button variant="ghost" disabled={readingNow || busy} aria-busy={readingNow || undefined} onClick={acts.onReadNow}>
                    {t('agents.read.now')}
                  </Button>
                </Tip>
              )}
              {tool.present && (
                <Tip content={t('agents.read.againTip', { agent: product })}>
                  <Button variant="ghost" disabled={reading || busy} aria-busy={reading || undefined} onClick={acts.onReadAgain}>
                    <Icon name="refresh" size={13} />
                    {reading ? t('agents.read.reading') : t('agents.read.again')}
                  </Button>
                </Tip>
              )}
            </span>
          </div>
          {/* What the fold into one set changed, one block above the rows while
              any of it is unsaid, a line per change, each receipt's lines in the reader's language; *Got it* marks the receipts
              shown said, by id, and the person's acknowledgement is all it is. */}
          {pooled.length > 0 && (
            <section
              aria-label={t('agents.pooled.title', { product })}
              className="mb-2 flex flex-wrap items-start gap-x-3 gap-y-1.5 border-l-[3px] border-accent bg-page/60 px-3 py-2"
            >
              <div className="min-w-0 flex-1 basis-72">
                <p className="m-0 text-small font-medium text-ink">{t('agents.pooled.title', { product })}</p>
                <ul className="m-0 mt-1 list-none p-0 text-small text-ink-soft [overflow-wrap:anywhere]">
                  {pooled.flatMap((receipt) => pooledLines(receipt, labelOf).map((line, at) => (
                    <li key={`pooled:${receipt.id}:${at}`}>{line}</li>
                  )))}
                </ul>
              </div>
              <Button disabled={busy} onClick={() => acts.onPooledSaid(pooled.map((receipt) => receipt.id))}>
                {t('agents.pooled.gotIt')}
              </Button>
            </section>
          )}
          {/* Which account the next start takes and why, in any workspace, above the rows that answer it in parts: a light line, not a setting's row, in the words of the fold's *Next start*; what
              holds the account the order begins on follows in the soft ink, since that is the one a person expects it to take. */}
          {said && (
            <p className="m-0 mb-2 text-small text-ink [overflow-wrap:anywhere]">
              {said.takes && <span>{said.takes}</span>}
              {said.held && <>{t('harness.said.sentences')}<span className="text-ink-soft">{said.held}</span></>}
              {said.doors.map((line) => <span key={line} className="block text-ink-soft">{line}</span>)}
            </p>
          )}
          <ul className={ACCOUNT_COLUMNS}>
            {tool.accounts.length > 0 && <AccountColumnsHead />}
            {tool.accounts.length === 0 && firstPress && (
              <li className="col-span-full mb-1 text-small text-ink-soft [overflow-wrap:anywhere]">{t('agents.account.none', { press: firstPress })}</li>
            )}
            {tool.accounts.map(accountRow)}
            {keying && (
              <li className="col-span-full">
              <form className="mt-2 flex flex-wrap items-center gap-2" onSubmit={(event) => { event.preventDefault(); addKey(); }}>
                <input
                  autoFocus
                  type="password"
                  autoComplete="off"
                  spellCheck={false}
                  value={keyDraft}
                  onChange={(event) => setKeyDraft(event.target.value)}
                  aria-label={t('harness.profile.keyLabel', { tool: product })}
                  placeholder={t('harness.profile.keyPlaceholder')}
                  className="min-w-72 flex-1 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink outline-none placeholder:text-ink-faint"
                />
                <Button type="submit" variant="primary" disabled={busy || !keyDraft.trim()}>{t('harness.profile.keySave')}</Button>
                {/* Held while the key is on its way, so a refusal is never said to nobody. */}
                <Button variant="ghost" disabled={busy} onClick={() => askKey(false)}>{t('common.cancel')}</Button>
                {refusalOf('key', '') && <Refused sentence={refusalOf('key', '')!} />}
                <span className="basis-full text-meta text-ink-faint">{t('harness.profile.keyHint')}</span>
              </form>
              </li>
            )}
            {/* Your own sign-in last, outside Daoris's accounts : its name and its ⓘ, which says all of it, and no state, no who, no reading, no act and no ⋯, since Daoris
                never asks it anything and never changes it. An agent not installed has none. */}
            {tool.present && <AccountRow name={ownLabel} why={ownLine()} menu={[]} now={now} />}
          </ul>
          {/* Each account's own plan and terms apply: one line at the list's foot that opens the paragraph,
              a target of 28 px where its line of text is about 16. */}
          {tool.accounts.length > 0 && (
            <div className="mt-1">
              <button
                type="button"
                aria-expanded={termsOpen}
                onClick={() => setTermsOpen((was) => !was)}
                className="flex min-h-7 items-center gap-1 rounded-control text-meta text-ink-faint hover:text-ink"
              >
                {t('agents.terms.show')}
                <Icon name={termsOpen ? 'chevronDown' : 'chevronRight'} size={12} />
              </button>
              {termsOpen && <p className="m-0 mt-1 text-meta text-ink-faint">{t('harness.terms')}</p>}
            </div>
          )}
        </section>
      )}

      {/* How the accounts are used: the one set's order and its one
          entry of settings, every workspace's. There is one scope to say. */}
      {use && machine && tool.accounts.length > 0 && (
        <Fold
          section="use"
          title={t('harness.use.title')}
          summary={useSummary(use, tool.accounts.length)}
          open={open.has('use')}
          onToggle={toggle}
        >
          <ScopeEditor agent={use} product={product} scope={machine} accounts={choices} busy={busy} acts={acts.scope} />
        </Fold>
      )}

      {/* The ways in, a property of the agent: each door, its version, its pin, which one driven work starts on. */}
    </>
  );
}
