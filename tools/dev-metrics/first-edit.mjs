import { isWorkEdit, readTranscript } from '../subagent-usage.mjs';
import { clockOf } from '../subagent-time.mjs';
import { missing } from './records.mjs';
import { CLASSES, classOf } from './read-class.mjs';

const lengthOf = content => typeof content === 'string'
  ? Buffer.byteLength(content)
  : Array.isArray(content) ? content.reduce((n, part) => n + Buffer.byteLength(typeof part?.text === 'string' ? part.text : ''), 0) : 0;

/** The bytes every tool result returned before the first edit of the work, in all and by the class of what was read, and its timestamp: null where no edit came. */
export function preEditContext(lines) {
  let bytes = 0;
  let libraryReads = 0;
  const byClass = Object.fromEntries(CLASSES.map(c => [c, 0]));
  const kinds = new Map();
  for (const line of lines) {
    const content = Array.isArray(line.message?.content) ? line.message.content : [];
    if (line.type === 'assistant' && content.some(isWorkEdit)) return { bytes, byClass, libraryReads, firstEditAt: line.timestamp ?? null };
    if (line.type === 'assistant') for (const part of content) if (part.type === 'tool_use') kinds.set(part.id, classOf(part.input));
    if (line.type === 'user') for (const part of content) if (part.type === 'tool_result') {
      const size = lengthOf(part.content);
      const kind = kinds.get(part.tool_use_id) ?? 'code';
      bytes += size;
      byClass[kind] += size;
      if (kind === 'library') libraryReads++;
    }
  }
  return null;
}

/** One builder transcript's text to the measures of its change; the first-edit ones are null with a note where it made no edit. */
export function builderMeasures(text, id) {
  const lines = [];
  for (const raw of text.split('\n')) {
    if (!raw.trim()) continue;
    try { lines.push(JSON.parse(raw)); } catch { continue; }
  }
  const read = readTranscript(text);
  const first = preEditContext(lines);
  const noEdit = missing(`the transcript of ${id} holds no edit of the work`);
  return {
    id, model: read.model || null, wallMinutes: Math.round(clockOf(lines).total / 6000) / 10,
    tokens: { input: read.input, output: read.output, cacheRead: read.cacheRead, cacheWrite: read.cacheCreate },
    turns: read.turns,
    preEditBytes: first ? first.bytes : null,
    preEditByClass: first ? first.byClass : null,
    libraryReads: first ? first.libraryReads : null,
    preEditTokens: first ? read.editContext - read.firstContext : null,
    startupTokens: read.firstContext,
    firstEditAt: first ? first.firstEditAt : null,
    ...(first ? {} : { missing: noEdit }),
  };
}
