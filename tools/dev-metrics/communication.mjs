import { elapsed, earliest, missing, moment } from './records.mjs';

export function communicationRows(home, scope, events, quests, inRange) {
  const scoped = scope.requested.length > 0;
  const stream = [...events].flatMap(([session, rows]) => rows.map(r => ({ ...r, session })));
  const channel = [];
  for (const [table, ackTable] of [['channel', 'channel_acks'], ['channel_addressed', 'channel_addressed_acks']]) {
    for (const post of home.tables[table].filter(p => inRange(p.at) && (!scoped || scope.asks.includes(p.ask) || scope.sessions.includes(p.to_session) || scope.asks.some(a => p.origin === `ask:${a}`)))) {
      const acks = home.tables[ackTable].filter(a => a.message === (post.origin && table === 'channel' ? `${post.origin}/${post.id}` : post.id) && (!scoped || scope.sessions.includes(a.session) || scope.asks.some(id => a.entry?.startsWith(`ask:${id}/`))));
      const expected = [...new Set(home.tables.sessions.filter(s => s.entry?.startsWith(`ask:${post.ask}/`) && s.id !== post.session && (!post.to_repo || s.repository === post.to_repo)).map(s => s.entry))];
      const recipients = [...acks];
      for (const entry of expected) if (!acks.some(a => a.entry === entry)) recipients.push({ entry });
      if (!recipients.length) recipients.push({ session: post.to_session });
      for (const ack of recipients) {
        const readAt = ['read', 'taken'].includes(ack.state) ? ack.at : undefined;
        channel.push({ message: post.id, ask: post.ask, addressee: ack.session ?? ack.entry ?? post.to_repo ?? missing('channel addressee entry/session'), state: ack.state ?? missing(`${ackTable} message acknowledgement`), postedAt: post.at,
          readAt: moment(readAt, `${ackTable}.at with state read/taken; sent is delivery only`), latencyMs: elapsed(post.at, readAt, `${ackTable}.at with state read/taken`),
          deliveredMs: elapsed(post.at, ['sent', 'read', 'taken'].includes(ack.state) ? ack.at : undefined, `${ackTable} delivery acknowledgement`),
          clock: 'latest retained acknowledgement; an earlier read clock overwritten by taken is not recoverable' });
      }
    }
  }
  const helpers = home.helpers.filter(h => inRange(h.at) && (!scoped || scope.sessions.includes(h.asker))).map(h => {
    const candidates = stream.filter(e => e.kind === 'user' && (h.state?.handed && e.id === h.state.handed || h.state?.helper && e.helper === h.state.helper) && !e.reaches);
    const deliveredAt = earliest(candidates.flatMap(word => stream.filter(e => e.kind === 'receipt' && e.id === word.id && e.session === word.session && e.receipt === 'sent').map(e => e.at)));
    return { helperAsk: h.id, asker: h.asker, askedAt: h.at, answeredAt: moment(h.answer?.at, 'helpers/<id>.answer.json at'), deliveredAt: moment(deliveredAt, 'asker session sent receipt matching helper answer word id'), latencyMs: elapsed(h.at, deliveredAt, 'helper ask.at and matching answer word sent receipt') };
  });
  const words = [];
  for (const [session, rows] of events) {
    const said = new Map();
    for (const e of rows.filter(e => e.kind === 'user' && e.id && e.reaches)) if (!said.has(e.id)) said.set(e.id, e);
    for (const [id, word] of said) {
      if (!inRange(word.at)) continue;
      const sentAt = earliest(rows.filter(e => e.kind === 'receipt' && e.id === id && e.receipt === 'sent').map(e => e.at));
      words.push({ session, word: id, saidAt: word.at, deliveredAt: moment(sentAt, 'session receipt sent under word id'), deliveredMs: elapsed(word.at, sentAt, 'session receipt sent under word id'), turnStartedMs: missing('per-turn started event linked to word id; user/taken and turn/end are not turn starts') });
    }
  }
  const questResponses = quests.filter(q => q.status === 'Done' && inRange(q.submission?.at ?? q.updated)).map(q => ({ quest: q.id, respondedAt: moment(q.submission?.at ?? q.updated, 'quest submission or done timestamp'), latencyMs: missing('quest response read acknowledgement with response operation id, reader and timestamp') }));
  const losses = stream.filter(e => e.kind === 'note' && e.code === 'lost' && inRange(e.at));
  return { channel, helpers, words, questResponses, lostWords: stream.length ? losses.length : missing('session events code lost'), coverage: missing('complete communication loss ledger, including quest response acknowledgements and recipients without a session record') };
}
