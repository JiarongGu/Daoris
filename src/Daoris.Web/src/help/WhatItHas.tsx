import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { HelpReceipt, HelpWired, HelpWiredLack } from './wired';
import { Button, Icon, PathText, Prose } from '../design/ui';
import { clockOf } from '../locales/format';
import { cn } from '../lib/cn';
import { lackDoor } from './modes';
import type { StarterDoor } from './starters';

/**
 * *What it has* (HELPMODE1b, D183 point 4; the modes design §6): what the starts of the conversation in view handed its agent, read
 * from the receipts each start kept (`HELP_WIRED`), never worked out again. The newest is read whole: its mode, its agent and account,
 * its folder, Daoris's tools, the skills, each server with its state, the rules and the checkouts; in restricted mode, what it lacks,
 * each with the door that grants it. The earlier starts sit behind a disclosure, and a conversation from before says not recorded.
 *
 * @remarks
 * A molecule: the answer arrives read, and a door goes out (`onDoor` to a place, `onMode` to the panel's chip). Desktop-only, as the
 * receipt is: its paths are this machine's.
 */
export function WhatItHas({ wired, loading, error, onRetry, onDoor, onMode, onClose }: {
  wired?: HelpWired;
  loading: boolean;
  /** Why the receipts could not be read, in the bridge's sentence; null when they were. */
  error: string | null;
  onRetry?: () => void;
  /** A lack's door to a place: its plugin's page, the browser's or this install's settings, AI features. */
  onDoor: (door: StarterDoor) => void;
  /** A lack's door to the mode: the panel's chip, opened. */
  onMode: () => void;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const [newest, ...earlier] = wired?.receipts ?? [];

  return (
    <section aria-label={t('help.wired.title')} className="grid min-w-0 gap-3">
      <header className="flex items-center gap-1">
        <Button variant="ghost" aria-label={t('help.wired.close')} onClick={onClose} className="h-7 shrink-0 px-1.5">
          <Icon name="back" size={14} />
        </Button>
        <h4 className="m-0 text-body font-semibold text-ink">{t('help.wired.title')}</h4>
      </header>

      {error !== null ? (
        <div role="alert" className="grid justify-items-start gap-1.5">
          <p className="m-0 text-small text-ink">{t('help.wired.failed')}</p>
          <p className="m-0 text-small text-ink-soft wrap-anywhere">{error}</p>
          {onRetry && <Button onClick={onRetry}>{t('help.history.retry')}</Button>}
        </div>
      ) : !wired ? (
        loading ? <p role="status" className="m-0 text-small text-ink-soft">{t('help.wired.loading')}</p> : null
      ) : !newest ? (
        <Prose className="m-0 text-small text-ink-soft">{t('help.wired.notRecorded')}</Prose>
      ) : (
        <>
          <Receipt receipt={newest} onDoor={onDoor} onMode={onMode} />
          {earlier.length > 0 && (
            <details className="min-w-0 border-t border-line pt-2">
              <summary className="cursor-pointer text-small text-ink-soft">{t('help.wired.earlier', { count: earlier.length })}</summary>
              <div className="mt-2 grid gap-4">
                {earlier.map((receipt) => <Receipt key={receipt.at} receipt={receipt} onDoor={onDoor} onMode={onMode} />)}
              </div>
            </details>
          )}
          <p className="m-0 text-meta text-ink-faint">{t('help.wired.private')}</p>
        </>
      )}
    </section>
  );
}

/** One start's receipt, field by field as it was kept. */
function Receipt({ receipt, onDoor, onMode }: {
  receipt: HelpReceipt; onDoor: (door: StarterDoor) => void; onMode: () => void;
}) {
  const { t } = useTranslation();
  const full = receipt.mode === 'full';
  const none = <span className="text-ink-faint">{t('help.wired.none')}</span>;
  // A code the page does not know is said as written: a newer driver's word is still its word.
  const why = (code: string) => t(`help.wired.why.${code}`, { defaultValue: code });
  const state = (code: string) => t(`help.wired.state.${code}`, { defaultValue: code });

  return (
    <div className="grid min-w-0 gap-2">
      <p className="m-0 text-meta text-ink-faint">
        {t(receipt.start === 'going-on' ? 'help.wired.at.going-on' : 'help.wired.at.new', { at: clockOf(receipt.at) })}
      </p>
      <dl className="m-0 grid min-w-0 gap-2 text-small">
        <Field name="mode">
          <span className={cn('inline-flex items-center gap-1', full ? 'text-ink' : 'text-ink-soft')}>
            <Icon name={full ? 'conflict' : 'shield'} size={12} className={full ? 'text-warn' : 'text-ink-faint'} />
            {t(full ? 'help.mode.full' : 'help.mode.restricted')}
          </span>
        </Field>
        <Field name="agent">{[receipt.harness, receipt.version].filter(Boolean).join(' ')}</Field>
        <Field name="account">{receipt.account ?? <span className="text-ink-soft">{t('help.wired.account.own')}</span>}</Field>
        <Field name="folder"><PathText path={receipt.folder} /></Field>
        <Field name="tools">
          {receipt.tools.length > 0 ? (
            <Names names={receipt.tools.map((tool) => tool.split('__').at(-1) ?? tool)} />
          ) : full ? <span className="text-ink-soft">{t('help.wired.tools.full')}</span> : none}
        </Field>
        <Field name="skills">
          {receipt.skills.length === 0 ? none : (
            <Rows>
              {receipt.skills.map((skill) => (
                <li key={skill.name}>
                  <code>{skill.name}</code>
                  <span className="text-ink-soft"> · {state(skill.state)}{skill.why && `: ${why(skill.why)}`}</span>
                </li>
              ))}
            </Rows>
          )}
        </Field>
        <Field name="servers">
          {receipt.servers.length === 0 ? none : (
            <Rows>
              {receipt.servers.map((server) => (
                <li key={server.name}>
                  <code>{server.name}</code>
                  {server.plugin && <span className="text-ink-faint"> {t('help.wired.plugin', { plugin: server.plugin })}</span>}
                  <span className={server.state === 'full-access' ? 'text-ink' : 'text-ink-soft'}>
                    {' · '}{state(server.state)}{server.why && `: ${why(server.why)}`}
                  </span>
                  {server.tools && server.tools.length > 0 && (
                    <span className="block text-meta text-ink-faint wrap-anywhere">{server.tools.join(', ')}</span>
                  )}
                </li>
              ))}
            </Rows>
          )}
        </Field>
        <Field name="rules">
          {receipt.rules === null ? <span className="text-ink-soft">{t('help.wired.rules.none')}</span> : (
            <div className="grid gap-1">
              <details>
                <summary className="cursor-pointer text-ink-soft">
                  {t('help.wired.rules.counts', {
                    allow: receipt.rules.allow.length, ask: receipt.rules.ask.length, deny: receipt.rules.deny.length,
                  })}
                </summary>
                {(['allow', 'ask', 'deny'] as const).filter((list) => receipt.rules![list].length > 0).map((list) => (
                  <div key={list} className="mt-1">
                    <span className="text-meta text-ink-faint">{t(`help.wired.rules.${list}`)}</span>
                    <Names names={receipt.rules![list]} />
                  </div>
                ))}
              </details>
              {receipt.guarded && <span className="text-ink-soft">{t('help.wired.guarded')}</span>}
            </div>
          )}
        </Field>
        <Field name="checkouts">
          {receipt.checkouts.length === 0 ? none : (
            <Rows>
              {receipt.checkouts.map((checkout) => (
                <li key={checkout.path}>
                  <span>{checkout.name}</span>
                  <span className="text-ink-faint"> · {checkout.workspace}</span>
                  <PathText path={checkout.path} className="block text-meta text-ink-faint" />
                </li>
              ))}
            </Rows>
          )}
        </Field>
        {receipt.lacks.length > 0 && (
          <Field name="lacks">
            <Rows>
              {receipt.lacks.map((lack) => (
                <Lack key={`${lack.kind}:${lack.name}`} lack={lack} why={why(lack.why)} onDoor={onDoor} onMode={onMode} />
              ))}
            </Rows>
          </Field>
        )}
      </dl>
    </div>
  );
}

/** What restricted mode lacks, said with why and the door that grants it (design §6). */
function Lack({ lack, why, onDoor, onMode }: {
  lack: HelpWiredLack; why: string; onDoor: (door: StarterDoor) => void; onMode: () => void;
}) {
  const { t } = useTranslation();
  const door = lackDoor(lack);
  return (
    <li className="grid justify-items-start gap-0.5">
      <span>
        {lack.kind === 'server' ? <code>{lack.name}</code> : <span className="text-ink">{t(`help.wired.lack.${lack.kind}`, { defaultValue: lack.name })}</span>}
        <span className="text-ink-soft"> · {why}</span>
      </span>
      {door === 'mode' ? (
        <Button variant="ghost" className="px-0 text-small text-accent" onClick={onMode}>{t('help.wired.door.mode')}</Button>
      ) : door ? (
        <Button variant="ghost" className="px-0 text-small text-accent" onClick={() => onDoor(door)}>
          {t(`help.door.${door.section ?? door.view}`)}
        </Button>
      ) : null}
    </li>
  );
}

/** One field of a receipt: its term, and its value. */
function Field({ name, children }: { name: string; children: ReactNode }) {
  const { t } = useTranslation();
  return (
    <div className="grid min-w-0 grid-cols-[minmax(0,8rem)_minmax(0,1fr)] gap-x-3 max-sm:grid-cols-1">
      <dt className="text-ink-faint">{t(`help.wired.field.${name}`)}</dt>
      <dd className="m-0 min-w-0 wrap-anywhere text-ink">{children}</dd>
    </div>
  );
}

function Rows({ children }: { children: ReactNode }) {
  return <ul className="m-0 grid list-none gap-1 p-0">{children}</ul>;
}

/** Names set as code, each on its own, wrapping as the line allows. */
function Names({ names }: { names: readonly string[] }) {
  return (
    <ul className="m-0 flex list-none flex-wrap gap-x-2 gap-y-0.5 p-0">
      {names.map((name) => <li key={name}><code>{name}</code></li>)}
    </ul>
  );
}
