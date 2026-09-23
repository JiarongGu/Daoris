import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Ask, Session } from '../api';
import { ago, sessionTool, size } from '../format';
import { Button, Drawer, Icon, Pill, SectionTitle, SelectField, SESSION_TONE } from '../ui';
import { ASK_TONE, firstLine, tierWords } from './AskCard';

/**
 * An ask's record (INT4c) — the screen twin of `daoris-driver ask`'s answer, and its two verbs: what
 * was asked and with what, which tier answered, what it proposed, the quests it became, *publish
 * to…* and *close* (D50: `ask --publish <id> --to` and `ask --close <id> --reason` are the terminal's).
 *
 * @remarks
 * **A proposal is a person's to accept** (INT4a): the declarations tier publishes nothing, so each
 * repository it proposed is offered as a publish, and any other adopter in the ask's circle can be
 * chosen instead. The service judges every publish; its sentence reaches the person verbatim, through
 * the holder.
 *
 * **The record says who answered** (INT4d): the tier in words, and the intake session that served
 * it, if one did. On the desktop that session is a door into Sessions, where its question is on its
 * transcript. A browser has no Sessions, so there it is named and nothing pretends to open it.
 *
 * **A closed ask becomes nothing more** — its reason stays and no verb is offered.
 *
 * The ask's files are named and never located: the host answers their path to this machine only, and a
 * page does not show a machine path (D47 §4, D65 §2).
 *
 * Props only, no hook from the query layer or the shell (components §2).
 */
export function AskRecord({
  ask, receivers, questTitles, intake = null, onAttend, busy = false, onPublish, onClose, onOpenQuest, onDismiss,
}: {
  ask: Ask;
  /** Whom the ask can be published to: the adopted repositories in its circle. */
  receivers: string[];
  /** Titles of the quests this page holds, by id — one it has not loaded is named by its id, and not opened. */
  questTitles: Record<string, string>;
  /** The record of the intake session that served it (INT4d), when the page holds it. */
  intake?: Session | null;
  /** The door into Sessions — absent where there are none, which is a browser. */
  onAttend?: (session: string) => void;
  busy?: boolean;
  onPublish: (to: string) => void;
  onClose: (reason: string) => void;
  onOpenQuest: (id: string) => void;
  onDismiss: () => void;
}) {
  const { t } = useTranslation();
  const [another, setAnother] = useState('');
  const [closing, setClosing] = useState(false);
  const [reason, setReason] = useState('');
  const live = ask.state !== 'Closed';

  return (
    <Drawer
      title={firstLine(ask.sentence)}
      onClose={onDismiss}
      meta={
        <>
          <Pill tone={ASK_TONE[ask.state]}>{t(`asks.state.${ask.state}`)}</Pill>
          <span className="font-mono text-meta text-ink-faint">#{ask.id}</span>
        </>
      }
      footer={live && (
        closing ? (
          <div className="grid gap-2">
            <textarea
              aria-label={t('asks.record.closeWhy')}
              placeholder={t('asks.record.closeWhy')}
              rows={2} value={reason}
              onChange={(e) => setReason(e.target.value)}
              className="resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
            />
            <div className="flex flex-wrap gap-2">
              <Button variant="danger" disabled={busy || !reason.trim()} onClick={() => onClose(reason.trim())}>
                {t('asks.record.closeConfirm')}
              </Button>
              <Button variant="ghost" disabled={busy} onClick={() => { setClosing(false); setReason(''); }}>
                {t('common.cancel')}
              </Button>
            </div>
          </div>
        ) : (
          <Button variant="ghost" disabled={busy} onClick={() => setClosing(true)}>{t('asks.record.close')}</Button>
        )
      )}
    >
      <p className="m-0 mb-4 whitespace-pre-wrap text-body leading-relaxed">{ask.sentence}</p>

      <dl className="m-0 mb-4 grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1.5 text-body">
        <dt className="text-ink-faint">{t('asks.record.circle')}</dt><dd className="m-0">{ask.workspace}</dd>
        <dt className="text-ink-faint">{t('asks.record.asked')}</dt>
        <dd className="m-0">{new Date(ask.asked).toLocaleString()} · {ago(ask.asked)}</dd>
        {ask.asker && (
          <><dt className="text-ink-faint">{t('asks.record.asker')}</dt><dd className="m-0">{ask.asker}</dd></>
        )}
        <dt className="text-ink-faint">{t('asks.record.tier')}</dt><dd className="m-0">{tierWords(t, ask.tier)}</dd>
        {ask.note && (
          /* Why it closed, or the service's sentence about the receiver it named — verbatim either way. */
          <>
            <dt className="text-ink-faint">{t(live ? 'asks.record.notPublished' : 'asks.record.closedBecause')}</dt>
            <dd className="m-0 whitespace-pre-wrap">{ask.note}</dd>
          </>
        )}
      </dl>

      {ask.intake && (
        /* Who answered (INT4d): the session the intake ran as — its state, then its tool, and its note,
           verbatim like every driver sentence. A door into Sessions only where Sessions exists, and
           only onto a record the page holds; one it does not is named by its id. */
        <section aria-label={t('asks.record.intake')} className="mb-4">
          <SectionTitle>{t('asks.record.intake')}</SectionTitle>
          {intake ? (
            <>
              <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
                <Pill tone={SESSION_TONE[intake.state]}>{t(`sessionState.${intake.state}`)}</Pill>
                {onAttend ? (
                  <button
                    type="button"
                    onClick={() => onAttend(intake.id)}
                    className="min-w-0 cursor-pointer truncate border-0 bg-transparent p-0 text-left font-mono text-meta text-accent underline-offset-2 hover:underline"
                  >
                    {sessionTool(intake)}
                  </button>
                ) : (
                  <span className="min-w-0 truncate font-mono text-meta text-ink-soft">{sessionTool(intake)}</span>
                )}
                <span className="font-mono text-meta text-ink-faint">#{intake.id.slice(0, 6)} · {ago(intake.updated)}</span>
              </p>
              {intake.note && <p className="mt-1.5 mb-0 text-body text-ink-soft">{intake.note}</p>}
            </>
          ) : (
            <span className="font-mono text-meta text-ink-soft">#{ask.intake.slice(0, 6)}</span>
          )}
        </section>
      )}

      {ask.quests.length > 0 && (
        <section aria-label={t('asks.record.quests')} className="mb-4">
          <SectionTitle>{t('asks.record.quests')}</SectionTitle>
          <ul className="m-0 grid list-none gap-1 p-0">
            {ask.quests.map((id) => (
              <li key={id}>
                {/* A door only onto a quest the page holds: just after a publish the answer names the
                    quest before the list has it, and a door that opens nothing is a dead click. */}
                {questTitles[id] ? (
                  <button
                    type="button"
                    onClick={() => onOpenQuest(id)}
                    className="inline-flex max-w-full items-baseline gap-2 text-left text-body text-accent underline-offset-2 hover:underline"
                  >
                    <span className="shrink-0 font-mono text-meta">#{id.slice(0, 6)}</span>
                    <span className="truncate">{questTitles[id]}</span>
                  </button>
                ) : (
                  <span className="font-mono text-meta text-ink-soft">#{id.slice(0, 6)}</span>
                )}
              </li>
            ))}
          </ul>
        </section>
      )}

      {live && (
        <section aria-label={t('asks.record.proposal')} className="mb-4">
          <SectionTitle>{t('asks.record.proposal')}</SectionTitle>
          {ask.proposal.length === 0 ? (
            <p className="m-0 mb-2 text-body text-ink-soft">{t('asks.record.noProposal')}</p>
          ) : (
            <ul className="m-0 mb-3 grid list-none gap-2 p-0">
              {ask.proposal.map((match) => (
                <li key={match.repository} className="flex items-center justify-between gap-3">
                  <span className="grid min-w-0 gap-0.5">
                    <span className="text-body font-semibold">{match.repository}</span>
                    <span className="truncate text-small text-ink-soft">
                      {t('asks.record.matched', { words: match.matched.join(', ') })}
                    </span>
                  </span>
                  <Button disabled={busy} onClick={() => onPublish(match.repository)}>
                    {t('asks.record.publishTo', { repository: match.repository })}
                  </Button>
                </li>
              ))}
            </ul>
          )}
          {/* Any adopter in its circle, not only the proposed: the declarations propose, a person
              decides — and a repository the declarations missed is still somebody's to name. */}
          <div className="flex flex-wrap items-center gap-2">
            <SelectField
              value={another}
              onChange={setAnother}
              ariaLabel={t('asks.record.another')}
              placeholder={t('asks.record.anotherPlaceholder', { circle: ask.workspace })}
              options={receivers.map((name) => ({ value: name, label: name }))}
            />
            <Button disabled={busy || !another} onClick={() => onPublish(another)}>{t('asks.record.publish')}</Button>
          </div>
        </section>
      )}

      {ask.links.length > 0 && (
        <section className="mb-4">
          <SectionTitle>{t('asks.record.links')}</SectionTitle>
          <ul className="m-0 grid list-none gap-1 p-0">
            {ask.links.map((link) => (
              <li key={link} className="min-w-0">
                <a
                  href={link} target="_blank" rel="noreferrer"
                  className="inline-flex max-w-full items-center gap-1.5 text-body text-accent underline-offset-2 hover:underline"
                >
                  <Icon name="link" size={12} />
                  <span className="truncate">{link}</span>
                </a>
              </li>
            ))}
          </ul>
        </section>
      )}

      {ask.attachments.length > 0 && (
        <section>
          <SectionTitle>{t('asks.record.files')}</SectionTitle>
          <ul className="m-0 grid list-none gap-1 p-0">
            {ask.attachments.map((file) => (
              <li key={file.sha256} className="inline-flex min-w-0 flex-wrap items-center gap-1.5 text-body text-ink-soft">
                <Icon name="attach" size={12} />
                <span className="truncate">{file.name}</span>
                <span className="font-mono text-meta text-ink-faint">{size(file.bytes)} · {t('asks.record.fileKept')}</span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </Drawer>
  );
}
