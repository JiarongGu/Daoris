export const askOf = (q) => /^ask\s+#?([\w-]+)/i.exec(q.from ?? '')?.[1];
const entryId = (s, kind) => new RegExp(`^${kind}:([^/]+)`).exec(s.entry ?? '')?.[1];

export function sessionLinks(records, events, landings) {
  const links = new Map();
  for (const s of records) {
    const q = s.quest ?? s.tookQuest ?? entryId(s, 'quest');
    if (q) links.set(s.id, q);
  }
  for (const l of landings) if (l.session && l.quest && !links.has(l.session)) links.set(l.session, l.quest);
  for (const [id, rows] of events) {
    if (links.has(id)) continue;
    const first = rows.find(r => r.kind === 'user' && /quest `#([0-9a-f]{8,})`/.test(r.text ?? ''));
    if (first) links.set(id, /quest `#([0-9a-f]{8,})`/.exec(first.text)[1]);
  }
  let moved = true;
  while (moved) {
    moved = false;
    for (const s of records) if (!links.has(s.id) && links.has(s.from ?? s.serves)) { links.set(s.id, links.get(s.from ?? s.serves)); moved = true; }
  }
  return links;
}

export function caseScope(value, quests, asks, sessions, links, events) {
  const allSessions = [...new Set([...sessions.map(s => s.id), ...events.keys()])];
  if (!value) return { requested: [], quests: quests.map(q => q.id), asks: asks.map(a => a.id), sessions: allSessions, looks: 'global driver loop in the date range' };
  const requested = String(value).split(',').map(v => v.trim().replace(/^(?:ask|quest)\s*/i, '').replace(/^#/, '')).filter(Boolean);
  if (!requested.length) throw new Error('case needs an ask or quest id');
  const qids = new Set(), aids = new Set();
  for (const id of requested) {
    const qm = quests.filter(q => q.id === id || q.id.startsWith(id));
    const am = asks.filter(a => a.id === id || a.id.startsWith(id));
    const inferred = [...new Set(quests.map(askOf).filter(a => a && (a === id || a.startsWith(id))))];
    const matches = [...new Set([...qm.map(q => `q:${q.id}`), ...am.map(a => `a:${a.id}`), ...inferred.map(a => `a:${a}`)])];
    if (matches.length !== 1) throw new Error(`case ${id} ${matches.length ? 'is ambiguous' : 'is unknown in the records read'}`);
    if (matches[0].startsWith('q:')) qids.add(matches[0].slice(2));
    else {
      const ask = matches[0].slice(2);
      aids.add(ask);
      for (const q of quests) if (askOf(q) === ask || asks.find(a => a.id === ask)?.quests?.includes(q.id)) qids.add(q.id);
    }
  }
  let moved = true;
  while (moved) {
    moved = false;
    for (const q of quests) if (q.parent && (qids.has(q.id) || qids.has(q.parent))) {
      for (const id of [q.id, q.parent]) if (!qids.has(id)) { qids.add(id); moved = true; }
    }
  }
  for (const q of quests) if (qids.has(q.id) && askOf(q)) aids.add(askOf(q));
  const sids = new Set(allSessions.filter(id => qids.has(links.get(id))));
  for (const s of sessions) if (aids.has(s.ask) || aids.has(entryId(s, 'ask')) && (!links.has(s.id) || qids.has(links.get(s.id)))) sids.add(s.id);
  moved = true;
  while (moved) {
    moved = false;
    for (const s of sessions) if (!sids.has(s.id) && (sids.has(s.from) || sids.has(s.serves))) { sids.add(s.id); moved = true; }
  }
  return { requested, quests: [...qids].sort(), asks: [...aids].sort(), sessions: [...sids].sort(), looks: 'global driver loop in the date range; look events carry no case identity' };
}
