import { earliest, elapsed, missing } from './records.mjs';

export function actorRows(sessions, home, inRange, logs) {
  const roles = [...new Set(sessions.map(s => s.actor ?? 'unreported'))];
  return roles.map(role => {
    const records = sessions.filter(s => (s.actor ?? 'unreported') === role);
    const failures = records.filter(s => s.state?.toLowerCase() === 'failed' && inRange(s.updated));
    const starts = records.map(s => earliest([
      ...home.tables.event_journal.filter(e => e.family === 'sessions' && e.change === 'state' && e.record?.replace(/^session:/, '') === s.id && e.state_new?.toLowerCase() === 'working').map(e => new Date(e.at).toISOString()),
      ...logs.filter(e => e.event === 'session.started' && e.data?.session === s.id).map(e => e.time),
    ])).filter(Boolean);
    const kinds = {};
    let untyped = 0;
    for (const s of failures) {
      const codes = s.noteParts?.map?.(p => p.code).filter(Boolean) ?? [];
      const kind = s.limited ? 'limit' : codes[0];
      if (kind) kinds[kind] = (kinds[kind] ?? 0) + 1;
      else untyped++;
    }
    return { role: role === 'unreported' ? missing('sessions.actor') : role,
      starts: starts.length ? starts.filter(inRange).length : missing('session.started or event_journal Working transition; created clocks queuing'),
      carryOns: records.filter(s => s.from && s.fromWhy !== 'agent' && inRange(s.created)).length,
      handOvers: records.filter(s => s.from && s.fromWhy === 'agent' && inRange(s.created)).length,
      failures: kinds, untypedFailures: untyped,
      ran: records.map(s => ({ session: s.id, asked: s.asked ?? missing('sessions.asked'), chosen: s.chosen ?? missing('sessions.chosen'), model: s.ran ?? missing('sessions.ran reported model'), tier: s.ran?.tier ?? missing('actual executed tier in sessions.ran; requested effort is not proof') })),
      carryOnCoverage: missing('same-session carry-on event census; from_session counts new parts only'),
      failureCoverage: 'current failed session records with updated in range; recovered failures require the historical transition ledger',
    };
  });
}

export function replyRows(sessions, events, inRange) {
  return sessions.filter(s => s.actor === 'ask-daoris').flatMap(s => {
    const rows = events.get(s.id) ?? [];
    const words = rows.filter(e => e.kind === 'user' && e.origin === 'person' && !e.reaches && inRange(e.at));
    return words.map((word, index) => {
      const next = words[index + 1]?.at;
      const reply = earliest(rows.filter(e => e.kind === 'assistant' && Date.parse(e.at) >= Date.parse(word.at) && (!next || Date.parse(e.at) < Date.parse(next))).map(e => e.at));
      return { session: s.id, word: word.id ?? missing('person word id'), firstReplyMs: elapsed(word.at, reply, 'Ask Daoris assistant reply event after person user event') };
    });
  });
}
