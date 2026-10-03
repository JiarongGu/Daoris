import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Ask, Session } from '../api';
import { ago, sessionTool, size, stamp } from '../format';
import { ExternalLink } from '../links';
import { Button, Icon, type IconName, Inline, Pill, Prose, SelectField, SESSION_TONE } from '../ui';
import { lastAbandon, pauseAsk, type WorkDoor, workOffers, type WorkPlan, type WorkTarget } from '../work/pausing';
import { PageHead, PageSection, ViewMain } from '../work/ViewMain';
import { AbandonAsk, AbandonedWork, PauseAsk } from '../work/WorkAsks';
import { ASK_TONE, firstLine, tierWords } from './AskRow';
import { GoAheadList } from './GoAheadList';

/** The acts in an ask's header (CTX1, D138 §4): its buttons and its page's right-click draw this one list. */
type AskAct = 'resume' | 'pause' | 'close' | 'abandon' | 'delete';

/** Each act's name, its button's look (the loud act is *Resume*, §7.1) and its glyph in a menu. */
const ASK_ACT: Record<AskAct, { label: string; variant: 'primary' | 'default' | 'ghost'; icon?: IconName }> = {
  resume: { label: 'asks.record.resume', variant: 'primary', icon: 'resume' },
  pause: { label: 'asks.record.pause', variant: 'default', icon: 'pause' },
  close: { label: 'asks.record.close', variant: 'default' },
  abandon: { label: 'asks.record.abandon', variant: 'ghost' },
  delete: { label: 'asks.record.delete', variant: 'ghost', icon: 'remove' },
};

/**
 * An ask's page (INT4c; FRAME1d, D118 §3d): the record in Quests' main area, the screen twin of
 * `daoris-driver ask`'s answer — what was asked and with what, which tier answered, what it proposed, the
 * quests it became — with *Close ask* and *Delete…* in its header and *publish to…* where the proposal is
 * read (D50: `ask --publish <id> --to` and `ask --close <id> --reason` are the terminal's).
 *
 * @remarks
 * **A record is the main area** (D118 §3d): it was a drawer, which since DOCK1a lay over the side bar and
 * the panel that hold Ask Daoris and the attended session. The composer stays a drawer, since it is a form.
 *
 * **A proposal is a person's to accept** (INT4a): the declarations tier publishes nothing, so each
 * repository it proposed is offered as a publish, and any other adopter in the ask's circle can be
 * chosen instead. The service judges every publish; its sentence reaches the person verbatim, through
 * the holder.
 *
 * **The record says who answered** (INT4d): the tier in words, and the intake session that served
 * it, if one did. On the desktop that session is a door into Sessions, where its question is on its
 * transcript. A browser has no Sessions, so there it is named and nothing pretends to open it.
 *
 * **A closed ask becomes nothing more** — its reason stays and no act is offered but *delete*.
 *
 * **An ask made by mistake can be deleted** (D95), with every quest asked by it — offered only where
 * the service says it may go (`deletable`), and asked once under the header, because nothing gives the
 * record back. `daoris-driver ask --delete <id>` is the terminal's twin.
 *
 * **The go-aheads its sessions asked** (KNOWUSE1a, D135 §2) come first among its sections, each once, by its act: one
 * waiting is answered here, yes or no with the person's words, and every session on the ask is handed the answer.
 * `daoris-driver ask --go-ahead` is the terminal's twin.
 *
 * The ask's files are named and never located: the host answers their path to this machine only, and a
 * page does not show a machine path (D47 §4, D65 §2).
 *
 * **Its work is paused, resumed and abandoned here** (PAUSE1e, D132 §7.1), from this machine's driver's plan of it, each
 * act where it applies and absent where it does not: *Pause…* while a pause would hold something, asking once where it
 * ends work in flight (§2.6); *Resume*, the loud act, while it is paused, with *paused* beside its state; *Abandon…*,
 * quiet, while the abandon would take anything, listing first and abandoning on its second press with the person's reason
 * (§3.1). After an abandon it says when, *What went* and *What stayed* (§4.2). Closing says it leaves the quests (§6.5). A
 * browser has no driver, so it offers none of the three and names the terminal's commands.
 *
 * Props only, no hook from the query layer or the shell (components §2).
 */
export function AskPage({
  ask, receivers, questTitles, intake = null, onAttend, busy = false, onPublish, onClose, onDelete, onOpenQuest, onAnswerGoAhead,
  work,
}: {
  ask: Ask;
  /** Whom the ask can be published to: the repositories the host says can be asked, in its circle (D70). */
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
  /** Delete the ask with every quest asked by it (D95) — absent where there is no door to do it. */
  onDelete?: () => void;
  onOpenQuest: (id: string) => void;
  /** The person's yes or no to a go-ahead its sessions asked (KNOWUSE1a) — absent where there is no door to give it. */
  onAnswerGoAhead?: (number: number, approved: boolean, words?: string) => void;
  /** This machine's driver's half (PAUSE1e): the plan and the three presses. Absent in a browser, which has no driver. */
  work?: WorkDoor;
}) {
  const { t } = useTranslation();
  const [another, setAnother] = useState('');
  // One question asks under the header at a time: closing, deleting, pausing, or the abandon's list.
  const [asking, setAsking] = useState<'close' | 'delete' | 'pause' | 'abandon' | null>(null);
  // The plan the abandon's list showed, held from when it opened: the second press sends exactly what it listed (§3.1).
  const [listed, setListed] = useState<WorkPlan | null>(null);
  const [reason, setReason] = useState('');
  const live = ask.state !== 'Closed';
  const deletable = ask.deletable === true && onDelete !== undefined;
  const rest = ask.sentence.split('\n').slice(1).join('\n').trim();
  const target: WorkTarget = { scope: 'ask', id: ask.id };
  const offers = workOffers(work?.plan);
  const waiting = busy || work?.busy === true;
  const pauseLines = work?.plan ? pauseAsk(work.plan, { wired: work.wired }) : null;
  const abandoned = lastAbandon(work);
  const closing = asking === 'close';
  const deleting = asking === 'delete';

  // A pause that stops nothing applies at once, with its notice, since nothing is lost (§2.6).
  const onPauseFirst = () => {
    if (pauseLines === null) work?.onPause(() => {});
    else setAsking('pause');
  };

  // The acts in the header (D118 §3b); while one asks under it, its first press is not offered twice. The loud act is the
  // next step: *Resume* while paused (§7.1). One list for its buttons and the page's right-click (CTX1).
  const headActs: AskAct[] = [
    ...(offers.resume ? ['resume' as const] : []),
    ...(offers.pause && asking !== 'pause' ? ['pause' as const] : []),
    ...(live && !closing ? ['close' as const] : []),
    ...(offers.abandon && asking !== 'abandon' ? ['abandon' as const] : []),
    ...(deletable && !deleting ? ['delete' as const] : []),
  ];
  const press = (act: AskAct) => {
    switch (act) {
      case 'resume': work?.onResume(); return;
      case 'pause': onPauseFirst(); return;
      case 'close': setAsking('close'); return;
      case 'abandon': setListed(work!.plan); setAsking('abandon'); return;
      case 'delete': setAsking('delete'); return;
    }
  };
  const acts = headActs.length > 0 && (
    <>
      {headActs.map((act) => (
        <Button key={act} variant={ASK_ACT[act].variant} disabled={waiting} onClick={() => press(act)}>
          {act === 'delete' && <Icon name="remove" size={13} />}
          {t(ASK_ACT[act].label)}
        </Button>
      ))}
    </>
  );
  const menu = {
    label: firstLine(ask.sentence),
    acts: [
      ...headActs.map((act) => ({
        id: act, label: t(ASK_ACT[act].label), icon: ASK_ACT[act].icon, disabled: waiting, onSelect: () => press(act),
      })),
      { id: 'copy', label: t('contextMenu.act.copyAsk'), icon: 'copy' as const, copy: ask.id },
    ],
  };

  const head = (
    <PageHead
      title={firstLine(ask.sentence)}
      pills={(
        <>
          <Pill tone={ASK_TONE[ask.state]}>{t(`asks.state.${ask.state}`)}</Pill>
          {/* The ask keeps its state, and says it is paused beside it (§2.2): a pause is this machine's, never a state. */}
          {offers.paused && <span title={t('asks.record.pausedTip')}><Pill tone="neutral">{t('asks.record.paused')}</Pill></span>}
        </>
      )}
      id={`#${ask.id}`}
      acts={acts || undefined}
    />
  );

  return (
    <ViewMain header={head} menu={menu}>
      {asking === 'pause' && work && pauseLines && (
        <PauseAsk
          className="mb-4"
          target={target}
          lines={pauseLines}
          meanIt={t('asks.record.pauseMeanIt')}
          busy={waiting}
          onPause={() => work.onPause(() => setAsking(null))}
          onCancel={() => setAsking(null)}
        />
      )}

      {asking === 'abandon' && work && listed && (
        <AbandonAsk
          target={target}
          plan={listed}
          meanIt={t('asks.record.abandonMeanIt')}
          placeholder={t('asks.record.abandonWhy')}
          busy={waiting}
          onAbandon={(why) => work.onAbandon(why, listed.abandon.pieces, () => { setAsking(null); setListed(null); })}
          onCancel={() => { setAsking(null); setListed(null); }}
        />
      )}

      {deleting && deletable && (
        /* 🔴 Nothing gives a deleted record back (D95): the first press only asks, and says the quests
           go too, the way removing an account says what it deletes. */
        <div
          role="group"
          aria-label={t('asks.record.deleteTitle')}
          className="mb-4 flex flex-wrap items-center gap-2 rounded-control border border-line bg-sunken px-2.5 py-2"
        >
          <span className="min-w-0 flex-1 basis-64 text-small text-ink-soft">{t('asks.record.deleteConfirm')}</span>
          <Button variant="danger" disabled={busy} onClick={() => { setAsking(null); onDelete!(); }}>
            {t('asks.record.deleteMeanIt')}
          </Button>
          <Button variant="ghost" disabled={busy} onClick={() => setAsking(null)}>{t('common.cancel')}</Button>
        </div>
      )}

      {live && closing && (
        <div className="mb-4 grid gap-2 rounded-control border border-line bg-sunken px-2.5 py-2">
          {/* A close is the person's word that the ask is answered, and leaves its quests as they are (§6.5). */}
          <span className="text-small text-ink-soft">{t('asks.record.closeLeaves')}</span>
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
            <Button variant="ghost" disabled={busy} onClick={() => { setAsking(null); setReason(''); }}>
              {t('common.cancel')}
            </Button>
          </div>
        </div>
      )}

      {/* The first line is the page's title, so the body is what follows it — a one-line ask was its title
          and then its body, word for word (POLISH4). The title wraps, so nothing is lost. */}
      {rest && <p className="m-0 mb-4 whitespace-pre-wrap text-body leading-relaxed">{rest}</p>}

      <dl className="m-0 mb-4 grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1.5 text-body">
        <dt className="text-ink-faint">{t('asks.record.circle')}</dt><dd className="m-0">{ask.workspace}</dd>
        <dt className="text-ink-faint">{t('asks.record.asked')}</dt>
        <dd className="m-0">{stamp(ask.asked)} · {ago(ask.asked)}</dd>
        {ask.asker && (
          <><dt className="text-ink-faint">{t('asks.record.asker')}</dt><dd className="m-0">{ask.asker}</dd></>
        )}
        <dt className="text-ink-faint">{t('asks.record.tier')}</dt>
        <dd className="m-0">
          {/* An intake that parks published nothing, so the tier stays the declarations' — whose words
              say no intake ran, which the section below would contradict. */}
          {ask.tier === 'declarations' && ask.intake ? t('asks.record.tierIntakeUnpublished') : tierWords(t, ask.tier)}
        </dd>
        {ask.note && (
          /* Why it closed, or the service's sentence about the receiver it named — verbatim either way. */
          <>
            <dt className="text-ink-faint">{t(live ? 'asks.record.notPublished' : 'asks.record.closedBecause')}</dt>
            <dd className="m-0 whitespace-pre-wrap">{ask.note}</dd>
          </>
        )}
        {abandoned && (
          /* When it was abandoned on this machine (§4.2); the reason is the close's note above, verbatim. */
          <>
            <dt className="text-ink-faint">{t('asks.record.abandonedAt')}</dt>
            <dd className="m-0">{stamp(abandoned.at)} · {ago(abandoned.at)}</dd>
          </>
        )}
      </dl>

      {abandoned && (
        <div className="mb-4">
          <AbandonedWork target={target} outcome={abandoned.outcome} went={t('asks.record.went')} stayed={t('asks.record.stayed')} />
        </div>
      )}

      {!work && live && (
        /* A browser has no driver (D47 §4): none of the three is offered, and the terminal's commands are named. */
        <Prose className="mb-4 text-small"><Inline text={t('asks.record.noDriver', { id: ask.id })} /></Prose>
      )}

      {ask.goAheads && ask.goAheads.length > 0 && (
        /* What its sessions asked the person for, each once (KNOWUSE1a): first among the sections, since one waiting
           holds a session until the person answers it. */
        <PageSection title={t('asks.record.goAheads')}>
          <GoAheadList goAheads={ask.goAheads} busy={busy} onAnswer={onAnswerGoAhead} />
        </PageSection>
      )}

      {ask.intake && (
        /* Who answered (INT4d): the session the intake ran as — its state, then its tool, and its note,
           verbatim like every driver sentence. A door into Sessions only where Sessions exists, and
           only onto a record the page holds; one it does not is named by its id. */
        <PageSection title={t('asks.record.intake')}>
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
        </PageSection>
      )}

      {ask.quests.length > 0 && (
        <PageSection title={t('asks.record.quests')}>
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
        </PageSection>
      )}

      {live && (
        <PageSection title={t('asks.record.proposal')}>
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
        </PageSection>
      )}

      {ask.links.length > 0 && (
        <PageSection title={t('asks.record.links')}>
          <ul className="m-0 grid list-none gap-1 p-0">
            {ask.links.map((link) => (
              <li key={link} className="min-w-0">
                {/* Where the person chose, their browser or Daoris's (BRW7). */}
                <ExternalLink
                  href={link}
                  className="inline-flex max-w-full items-center gap-1.5 text-body text-accent underline-offset-2 hover:underline"
                >
                  <Icon name="link" size={12} />
                  <span className="truncate">{link}</span>
                </ExternalLink>
              </li>
            ))}
          </ul>
        </PageSection>
      )}

      {ask.attachments.length > 0 && (
        <PageSection title={t('asks.record.files')}>
          <ul className="m-0 grid list-none gap-1 p-0">
            {ask.attachments.map((file) => (
              <li key={file.sha256} className="inline-flex min-w-0 flex-wrap items-center gap-1.5 text-body text-ink-soft">
                <Icon name="attach" size={12} />
                <span className="truncate">{file.name}</span>
                <span className="font-mono text-meta text-ink-faint">{size(file.bytes)} · {t('asks.record.fileKept')}</span>
              </li>
            ))}
          </ul>
        </PageSection>
      )}
    </ViewMain>
  );
}
