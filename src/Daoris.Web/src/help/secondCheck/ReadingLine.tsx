import { type ReactNode, useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { type Reading, type ReadingFinding, useReadingPacket } from '../../bridge/helpReadings';

/** The words of a reading's state: what it found where read, why where not, and each limit where partial. */
function stateSaid(reading: Reading, t: ReturnType<typeof useTranslation>['t']): string {
  const flags = reading.findings.filter((each) => each.severity === 'flag').length;
  const notes = reading.findings.length - flags;
  switch (reading.state) {
    case 'queued': return reading.waiting ? t('help.secondCheck.queuedWaiting', { why: reading.waiting }) : t('help.secondCheck.queued');
    case 'running': return t('help.secondCheck.reading');
    case 'left': return t('help.secondCheck.left');
    case 'unread': return t('help.secondCheck.unread', { why: reading.why ?? '' });
    case 'disabled': return t('help.secondCheck.off');
    default: {
      const found = flags > 0 ? t('help.secondCheck.flagged', { count: flags }) + (notes > 0 ? ` · ${t('help.secondCheck.notes', { count: notes })}` : '')
        : notes > 0 ? t('help.secondCheck.notes', { count: notes })
          : t(reading.state === 'partial' ? 'help.secondCheck.nothingInRead' : 'help.secondCheck.nothing');
      return reading.state === 'partial' ? `${t('help.secondCheck.partial')} · ${found}` : found;
    }
  }
}

/** Who read it, as reported: *Read by codex-acp (gpt-6.1-sol, as reported), another maker*. */
function readerSaid(reading: Reading, t: ReturnType<typeof useTranslation>['t']): string | null {
  if (!reading.reader) return null;
  return t('help.secondCheck.readBy', {
    agent: reading.reader.agent,
    ran: reading.reader.ran ?? t('help.secondCheck.notReported'),
    standing: t(`help.secondCheck.independence.${reading.independence ?? 'unknown'}`),
  });
}

function Finding({ finding }: { finding: ReadingFinding }) {
  const { t } = useTranslation();
  return (
    <li data-reading-finding={finding.severity} className="grid gap-0.5">
      <span className="text-small text-ink">
        <span className={finding.severity === 'flag' ? 'text-warn' : 'text-ink-soft'}>{t(`help.secondCheck.severity.${finding.severity}`)}</span>
        {' · '}{t(`help.secondCheck.certainty.${finding.certainty}`)}{' · '}{finding.says}
      </span>
      <span className="text-meta text-ink-faint">
        {t('help.secondCheck.restsOn', { call: finding.call })}
        {' · '}{finding.instruction === 'none' ? t('help.secondCheck.noWords') : t('help.secondCheck.words', { words: finding.instruction })}
      </span>
    </li>
  );
}

/**
 * One reading's line under the last turn it covers: its state in the catalogue's words, and *Details*, which opens its reader, its
 * evidence limits, its findings each with the call and the person's words it rests on, its leave's reasons, and the packet as
 * sent, asked only once opened. A claim, never a fact: nothing here holds, approves or reverses anything.
 */
export function ReadingLine({ session, reading }: { session: string; reading: Reading }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [sent, setSent] = useState(false);
  const id = useId();
  const packet = useReadingPacket(session, reading.range, sent);
  const by = readerSaid(reading, t);
  return (
    <div data-reading={reading.state} className="grid gap-1 text-small text-ink-soft">
      <div className="flex flex-wrap items-center gap-2">
        <span>{t('help.secondCheck.line', { state: stateSaid(reading, t) })}</span>
        <Button variant="ghost" aria-expanded={open} aria-controls={`${id}-details`} onClick={() => setOpen((was) => !was)}>
          {t(open ? 'help.secondCheck.hide' : 'help.secondCheck.details')}
        </Button>
      </div>
      {open && (
        <div id={`${id}-details`} className="grid gap-1.5 rounded-control border border-line bg-sunken px-3 py-2">
          {by && <p className="m-0">{by}</p>}
          {reading.limits.length > 0 && (
            <p className="m-0 text-ink-faint">{reading.limits.map((limit) => t(`help.secondCheck.limit.${limit.code}`, { defaultValue: limit.code })).join(' · ')}</p>
          )}
          {reading.findings.length > 0 && (
            <ul className="m-0 grid list-none gap-1 p-0">{reading.findings.map((each) => <Finding key={each.index} finding={each} />)}</ul>
          )}
          {reading.more > 0 && <p className="m-0 text-ink-faint">{t('help.secondCheck.more', { count: reading.more })}</p>}
          {reading.reasons.length > 0 && (
            <ul className="m-0 grid list-none gap-0.5 p-0">{reading.reasons.map((reason, at) => <li key={at}>{reason.says}</li>)}</ul>
          )}
          {(reading.state === 'read' || reading.state === 'partial' || reading.state === 'left') && (
            <div className="grid gap-1">
              <Button variant="ghost" aria-expanded={sent} onClick={() => setSent((was) => !was)}>{t('help.secondCheck.whatWasSent')}</Button>
              {sent && (
                <pre data-reading-packet="" className="m-0 max-h-80 overflow-auto whitespace-pre-wrap text-meta text-ink">
                  {packet.data ?? (packet.isLoading ? t('help.secondCheck.loading') : t('help.secondCheck.noPacket'))}
                </pre>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

/** What the conversation draws under each turn: the line of every reading whose last turn it is. A turn not selected has none. */
export function readingsBeneath(session: string | null, readings: readonly Reading[]): (ask: { id?: string }) => ReactNode {
  return (ask) => {
    if (!session || !ask.id) return null;
    const under = readings.filter((reading) => reading.last === ask.id);
    return under.length === 0 ? null : under.map((reading) => <ReadingLine key={reading.range} session={session} reading={reading} />);
  };
}
