// An account's own settings — its model and its effort — in the tool's own file (AGT6, D98).
//
// Claude Code keeps an account's defaults in `settings.json` in its configuration home: the user tier
// of its settings cascade, and the file its ACP adapter reads at `CLAUDE_CONFIG_DIR/settings.json`
// (`claude-agent-acp` 0.84.0, `dist/settings.js`). A Daoris account IS such a home (D49 §4), so the
// file is the account's, and the tool's: Daoris changes the keys a person asked it to change —
// `model`, `effortLevel`, and an effort per model under `modelSettings` — and nothing else in it.
//
// The CLI's twin of the driver's `AgentSettings`. They share no code; the FILE is the contract, and the
// same seven rules are asserted on both sides (`agentsettings.test.ts`, `AgentSettingsTests.cs`):
//
//   1. The file is the tool's own `settings.json`, in the account's configuration home.
//   2. No file is the tool's defaults: nothing set, and reading creates nothing.
//   3. A file that is not a JSON object is a sentence to a read and a refusal to a write, which leaves
//      it exactly as it was.
//   4. A write changes only what it names; every other key keeps its place and its value.
//   5. An effort is low, medium, high or xhigh — `max` is refused, since the tool keeps it for one
//      session only; a model is one word, and the tool's own aliases are offered first.
//   6. Clearing removes the key; an entry per model left empty goes, and so does an empty
//      `modelSettings`.
//   7. A new file holds exactly what was written, in the tool's formatting: two-space indent, LF, and
//      a final newline.
//
// It edits one file and nothing else: no spawn, no network.

import { existsSync } from 'node:fs';
import { DaorisError } from './errors.ts';
import { readText, writeTextAtomic } from './fsx.ts';

/** The tool's own settings file, in the account's configuration home. The driver's `AgentSettings.FileName`. */
export const AGENT_SETTINGS_FILE = 'settings.json';

/**
 * The model names the tool itself accepts as aliases, offered before a free field for a full id: the
 * Agent SDK's list at 0.3.284 (the one `claude-agent-acp` 0.84.0 carries), with `default`, the tool's
 * own default. The driver's `AgentSettings.Models` is the other copy.
 */
export const AGENT_MODELS: readonly string[] = [
  'default', 'sonnet', 'opus', 'haiku', 'fable', 'best', 'sonnet[1m]', 'opus[1m]', 'fable[1m]', 'opusplan',
];

/**
 * The efforts the tool's settings keep: its schema's `effortLevel`. `max` exists for one session and is
 * never written to a file, so it is not offered here. The driver's `AgentSettings.Efforts` is the other copy.
 */
export const AGENT_EFFORTS: readonly string[] = ['low', 'medium', 'high', 'xhigh'];

/** One model's own effort, which the tool reads before the account's for that model. */
export interface ModelEffort {
  model: string;
  effort: string;
}

/** What the account's file says, or why it could not be read. Null is "not set": the tool's own default. */
export interface AgentSettingsRead {
  model: string | null;
  effort: string | null;
  perModel: ModelEffort[];
  problem: string | null;
}

/** What a write changes: a key absent is untouched, a value sets it, and null clears it. */
export interface AgentSettingsEdit {
  model?: string | null;
  effort?: string | null;
  /** An effort per model, by the tool's canonical model name. */
  perModel?: Record<string, string | null>;
}

type Json = Record<string, unknown>;

const isObject = (value: unknown): value is Json =>
  !!value && typeof value === 'object' && !Array.isArray(value);

/** The file as an object, or why it is not one. Absent is an empty object and no problem. */
function load(file: string): { root: Json | null; text: string | null; problem: string | null } {
  if (!existsSync(file)) return { root: {}, text: null, problem: null };
  const text = readText(file);
  let parsed: unknown;
  try {
    parsed = JSON.parse(text);
  } catch (error) {
    return { root: null, text, problem: `\`${file}\` could not be read (${(error as Error).message})` };
  }
  return isObject(parsed)
    ? { root: parsed, text, problem: null }
    : { root: null, text, problem: `\`${file}\` is not a JSON object` };
}

/** What the account's file says about its model and effort (rules 1–3). */
export function readAgentSettings(file: string): AgentSettingsRead {
  const { root, problem } = load(file);
  if (root === null) return { model: null, effort: null, perModel: [], problem };

  const perModel: ModelEffort[] = [];
  if (isObject(root.modelSettings)) {
    for (const [model, entry] of Object.entries(root.modelSettings)) {
      if (isObject(entry) && typeof entry.effortLevel === 'string') perModel.push({ model, effort: entry.effortLevel });
    }
  }

  return {
    // The tool ignores a model that is not text, so it is not set as far as anyone can tell.
    model: typeof root.model === 'string' ? root.model : null,
    effort: typeof root.effortLevel === 'string' ? root.effortLevel : null,
    perModel,
    problem: null,
  };
}

/** A model name the tool could be handed: one word, with no space in it and nothing unprintable. */
export function judgeModel(value: string, what = 'model'): string {
  const trimmed = value.trim();
  if (!trimmed || /[\s\p{Cc}]/u.test(trimmed) || trimmed.length > 200) {
    throw new DaorisError(
      `\`${value}\` is not a ${what} name the tool could read — one word, such as ${AGENT_MODELS.slice(1, 4).join(', ')} `
      + 'or a full model id.');
  }
  return trimmed;
}

/** An effort the tool's settings keep (rule 5). */
export function judgeEffort(value: string): string {
  const trimmed = value.trim();
  if (trimmed === 'max') {
    throw new DaorisError(
      '`max` is an effort the tool keeps for one session only — its settings never hold it, so it cannot be '
      + `an account's default. Choose it for one conversation instead, or one of ${AGENT_EFFORTS.join(', ')}.`);
  }
  if (!AGENT_EFFORTS.includes(trimmed)) {
    throw new DaorisError(`\`${value}\` is not an effort the tool's settings keep — one of ${AGENT_EFFORTS.join(', ')}.`);
  }
  return trimmed;
}

/**
 * Change the named keys in the account's file and nothing else (rules 3–7), then read it back.
 *
 * @remarks
 * Every value is judged before anything is written, so a refused edit leaves the file as it was. A
 * write that would change nothing writes nothing: the file is the tool's, and a rewrite that only
 * re-spaced it would still be a change to somebody else's file.
 *
 * Numbers round-trip through `JSON.parse`, as the trust flag's writer notes: an integer beyond 2^53
 * would lose precision. The keys the tool keeps here are none of that kind.
 */
export function writeAgentSettings(file: string, edit: AgentSettingsEdit): AgentSettingsRead {
  const model = edit.model === undefined || edit.model === null ? edit.model : judgeModel(edit.model);
  const effort = edit.effort === undefined || edit.effort === null ? edit.effort : judgeEffort(edit.effort);
  const perModel = Object.entries(edit.perModel ?? {}).map(([name, value]) =>
    [judgeModel(name), value === null ? null : judgeEffort(value)] as const);

  const { root, text, problem } = load(file);
  if (root === null) throw unwritable(problem!);

  if (model === null) delete root.model;
  else if (model !== undefined) root.model = model;

  if (effort === null) delete root.effortLevel;
  else if (effort !== undefined) root.effortLevel = effort;

  for (const [name, value] of perModel) {
    if (root.modelSettings === undefined) {
      if (value === null) continue;
      root.modelSettings = {};
    }
    const settings = root.modelSettings;
    if (!isObject(settings)) throw unwritable(`its \`modelSettings\` in \`${file}\` is not an object`);

    if (settings[name] === undefined) {
      if (value === null) continue;
      settings[name] = {};
    }
    const entry = settings[name];
    if (!isObject(entry)) throw unwritable(`its entry for \`${name}\` in \`${file}\` is not an object`);

    if (value === null) {
      delete entry.effortLevel;
      if (Object.keys(entry).length === 0) delete settings[name];
    } else {
      entry.effortLevel = value;
    }
    if (Object.keys(settings).length === 0) delete root.modelSettings;
  }

  // The tool's own formatting: two-space indent, LF, and the final newline it had (a new file gets one).
  let next = JSON.stringify(root, null, 2);
  if (text === null || text.endsWith('\n')) next += '\n';
  if (next !== text) writeTextAtomic(file, next);

  return readAgentSettings(file);
}

function unwritable(problem: string): DaorisError {
  return new DaorisError(
    `${problem}, so nothing was written to it — fix it, or change the setting with the tool itself.`);
}
