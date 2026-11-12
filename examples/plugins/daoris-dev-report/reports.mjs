// The reports this plugin keeps, one record each in its own private storage (the kit's storage.mjs, one record at a time under a lock),
// and the two commands over them: `report` files one, `inbox` lists, reads and marks them. No network, no repository: a report is
// words a session wrote, with the session and place Daoris handed the round.
import { randomBytes } from 'node:crypto';
import { join } from 'node:path';
import { records } from './storage.mjs';

const SECTION = 'reports';
const SEVERITIES = ['note', 'defect', 'blocking'];

/** What a round's `said` may carry: 40 entries of 2,000 characters and 8 KiB in all. The body and evidence are bounded so a report reads whole. */
const MAX_TITLE = 200;
const MAX_BODY = 5000;
const MAX_EVIDENCE = 2000;
const CHUNK = 1900;

const bytes = (text) => Buffer.byteLength(text, 'utf8');
const text = (value) => (typeof value === 'string' ? value.trim() : '');

const store = () => records(join(process.env.DAORIS_PLUGIN_PRIVATE, 'reports.json'));

/** Report ids sort by time and end in a random tail, which names a report when no other ends the same. */
const newId = (at) => `${at.toISOString().replace(/[-:]/g, '').replace(/\.\d+Z$/, 'Z')}-${randomBytes(3).toString('hex')}`;

/** The lines of a long text packed into entries a round may say. */
function pack(lines) {
  const entries = [];
  for (const line of lines) {
    for (let from = 0; from === 0 || from < line.length; from += CHUNK) {
      const piece = line.slice(from, from + CHUNK);
      const last = entries.length - 1;
      if (last >= 0 && entries[last].length + piece.length + 1 <= CHUNK) entries[last] += `\n${piece}`;
      else entries.push(piece);
    }
  }
  return entries;
}

const said = (lines, exit, data) => ({ said: pack(lines), exit, ...(data === undefined ? {} : { data }) });

function file(args, call) {
  const title = text(args.title);
  const body = typeof args.body === 'string' ? args.body.trim() : '';
  const evidence = text(args.evidence);
  const severity = text(args.severity) || 'note';
  if (!title) return said(['A report needs a `title`.'], 1);
  if (!body) return said(['A report needs a `body`.'], 1);
  if (!SEVERITIES.includes(severity)) return said([`\`severity\` is one of ${SEVERITIES.join(', ')}.`], 1);
  if (title.length > MAX_TITLE) return said([`The \`title\` is over ${MAX_TITLE} characters: shorten it.`], 1);
  if (bytes(body) > MAX_BODY) return said([`The \`body\` is over ${MAX_BODY} bytes: shorten it, and put the long evidence in a file the report names.`], 1);
  if (bytes(evidence) > MAX_EVIDENCE) return said([`The \`evidence\` is over ${MAX_EVIDENCE} bytes: quote the lines that matter and name the file.`], 1);
  const at = new Date();
  const id = newId(at);
  const record = { id, at: at.toISOString(), title, body, evidence, severity, session: call.session ?? null, place: call.place ?? 'machine', from: call.from ?? null, read: null };
  store().put(SECTION, id, record);
  return said([`Report ${id} is filed (${severity}): ${title}`], 0, { id, at: record.at, severity });
}

/** The reports, unread first and then newest first. */
const ordered = (all) => Object.values(all).sort((a, b) => (a.read === null) !== (b.read === null) ? (a.read === null ? -1 : 1) : b.at.localeCompare(a.at));

/** The report an id names: whole, else the one whose id ends or starts with it. */
function found(all, id) {
  if (all[id]) return { report: all[id] };
  const wanted = id.toLowerCase();
  const near = Object.values(all).filter((each) => each.id.toLowerCase().endsWith(wanted) || each.id.toLowerCase().startsWith(wanted));
  if (near.length === 1) return { report: near[0] };
  return { problem: near.length ? `\`${id}\` names ${near.length} reports: ${near.map((each) => each.id).join(', ')}.` : `No report is named \`${id}\`.` };
}

function whole(report) {
  const lines = [`${report.id}  ${report.severity}  ${report.read === null ? 'unread' : `read ${report.read}`}`, `Title: ${report.title}`, `Filed: ${report.at}`];
  lines.push(`From: session ${report.session ?? 'none'}, place ${typeof report.place === 'string' ? report.place : `${report.place.kind} ${report.place.id}`}`, '', ...report.body.split('\n'));
  if (report.evidence) lines.push('', 'Evidence:', ...report.evidence.split('\n'));
  return lines;
}

function inbox(args) {
  const action = text(args.action) || 'list';
  const id = text(args.id);
  if (action === 'list') {
    const shown = ordered(store().all(SECTION)).filter((each) => args.unread !== true || each.read === null);
    if (!shown.length) return said([args.unread === true ? 'No report is unread.' : 'No report is filed.'], 0, { reports: [] });
    const lines = shown.map((each) => `${each.read === null ? '*' : ' '} ${each.id}  ${each.severity.padEnd(8)} ${each.title}`);
    return said(lines.slice(0, 200), 0, { reports: shown.slice(0, 50).map(({ id: key, at, severity, title, read }) => ({ id: key, at, severity, title, read })) });
  }
  if (action !== 'read' && action !== 'mark') return said([`\`action\` is one of list, read, mark.`], 2);
  if (!id) return said([`\`${action}\` needs the report's \`id\`, or the tail of it.`], 1);
  const kept = store();
  const { report, problem } = found(kept.all(SECTION), id);
  if (!report) return said([problem], 1);
  if (action === 'read') return said(whole(report), 0, report);
  if (report.read === null) kept.put(SECTION, report.id, { ...report, read: new Date().toISOString() });
  return said([`Report ${report.id} is marked read.`], 0, { id: report.id });
}

/** One round of a command; a plugin handed no private storage says so rather than writing elsewhere. */
export function run({ command, args = {}, from, session, place }) {
  if (!process.env.DAORIS_PLUGIN_PRIVATE) return said(['Daoris handed this plugin no private storage: switch its storage on in Plugins.'], 1);
  if (command === 'report') return file(args, { from, session, place });
  if (command === 'inbox') return inbox(args);
  return said([`This plugin has no command \`${command}\`.`], 2);
}
