import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import {
  ACCOUNTS_FILE, AccountNameError, accountNames, apartWords, signedInSentence, forgetName, nameOf, newAccountId, offeredName, renameAccount, resolveAccount,
  shownAs, signInTarget,
} from '../../src/accounts/accountnames.ts';
import { readHarnessSettings } from '../../src/agents/toolchain.ts';
import { driverRows as csharpRows, driverTestFile } from '../_csharp.ts';
import { makeFixture } from '../_fixture.ts';

/**
 * An account's name and the account a person means (ACCT2, D125's ACCT2 note): `accounts.json` under the home holds the
 * name a person gave each account, by agent and its stable id, the folder's name. The CLI's half of a TWIN with the
 * driver's `AccountNames.cs`: 🔴 `AccountNamesTwinTests.cs` holds the same tables, row for row and in the same order, and
 * the last test here holds each to this one, cell for cell.
 *
 * A file is the text on disk, `null` for none; accounts are the folders on disk, as a JSON list. An `after` is the file's
 * JSON once the name is given, or `refused <kind>` with the account it collides with, where the door refuses it and writes
 * nothing.
 */

type Cell = string | null;

const list = (text: string) => JSON.parse(text) as string[];

function holding(root: string, file: Cell): void {
  if (file !== null) writeFileSync(join(root, ACCOUNTS_FILE), file, 'utf8');
}

// ——— Reading: missing or unreadable is no name; a name is text, trimmed, not blank; agents and accounts compare
// without case, as the readings compare them.

const READ_ROWS: [why: string, file: Cell, agent: string, account: string, name: Cell][] = [
  ['missing is no name', null, 'claude-code', 'account-1', null],
  ['not JSON is no name', 'not json', 'claude-code', 'account-1', null],
  ['an account named', '{"claude-code":{"account-1":{"name":"you@work.example"}}}', 'claude-code', 'account-1', 'you@work.example'],
  ['another account\'s name is not this one\'s', '{"claude-code":{"account-2":{"name":"seat"}}}', 'claude-code', 'account-1', null],
  ['another agent\'s name is not this one\'s', '{"codex":{"account-1":{"name":"seat"}}}', 'claude-code', 'account-1', null],
  ['a name is read trimmed', '{"claude-code":{"account-1":{"name":" work "}}}', 'claude-code', 'account-1', 'work'],
  ['a blank name is none', '{"claude-code":{"account-1":{"name":"  "}}}', 'claude-code', 'account-1', null],
  ['a name that is not text is none', '{"claude-code":{"account-1":{"name":7}}}', 'claude-code', 'account-1', null],
  ['an entry that is not an object is none', '{"claude-code":{"account-1":"work"}}', 'claude-code', 'account-1', null],
  ['an agent that is not an object is none', '{"claude-code":[1]}', 'claude-code', 'account-1', null],
  ['agents and accounts compare without case', '{"claude-code":{"account-1":{"name":"work"}}}', 'Claude-Code', 'ACCOUNT-1', 'work'],
  ['a name in Chinese is read as written', '{"claude-code":{"acct-3f9c2a71":{"name":"工作"}}}', 'claude-code', 'acct-3f9c2a71', '工作'],
  ['a letter whose capital is two letters is not those two: straße is not STRASSE', '{"claude-code":{"straße":{"name":"work"}}}', 'claude-code', 'STRASSE', null],
  ['a dotted capital I is not an i with a dot above', '{"claude-code":{"İzmir":{"name":"work"}}}', 'claude-code', 'i\u{307}zmir', null],
];

test('a name reads as the driver reads it (the twin\'s table)', () => {
  for (const [index, [why, file, agent, account, name]] of READ_ROWS.entries()) {
    const fx = makeFixture(`names-read-${index}`);
    holding(fx.root, file);

    assert.equal(nameOf(fx.root, agent, account), name, why);
    fx.cleanup();
  }
});

// ——— The account a person means: the one whose id it is, exactly; else the one whose name it is, in any case.

const RESOLVE_ROWS: [why: string, accounts: string, file: Cell, agent: string, given: string, id: Cell][] = [
  ['an id names its account', '["account-1","account-2"]', null, 'claude-code', 'account-2', 'account-2'],
  ['a name names its account', '["account-1","account-2"]', '{"claude-code":{"account-1":{"name":"work"}}}', 'claude-code', 'work', 'account-1'],
  ['a name names it in any case', '["account-1","account-2"]', '{"claude-code":{"account-1":{"name":"work"}}}', 'claude-code', 'WORK', 'account-1'],
  ['what is named is read trimmed', '["account-1"]', '{"claude-code":{"account-1":{"name":"work"}}}', 'claude-code', ' work ', 'account-1'],
  ['an id compares exactly, as the wiring compares it', '["account-1"]', null, 'claude-code', 'Account-1', null],
  ['an id wins over a name written by hand to match it', '["account-1","work"]', '{"claude-code":{"account-1":{"name":"work"}}}', 'claude-code', 'work', 'work'],
  ['a name whose account is gone names nothing', '["account-1"]', '{"claude-code":{"account-9":{"name":"work"}}}', 'claude-code', 'work', null],
  ['another agent\'s name names nothing here', '["account-1"]', '{"codex":{"account-1":{"name":"work"}}}', 'claude-code', 'work', null],
  ['nothing named is nothing', '["account-1"]', null, 'claude-code', 'seat', null],
  ['a letter whose capital is two letters is not those two: straße is not STRASSE', '["account-1"]', '{"claude-code":{"account-1":{"name":"straße"}}}', 'claude-code', 'STRASSE', null],
  ['a dotted capital I is not an i with a dot above', '["account-1"]', '{"claude-code":{"account-1":{"name":"İzmir"}}}', 'claude-code', 'i\u{307}zmir', null],
];

test('an account resolves as the driver resolves it (the twin\'s table)', () => {
  for (const [index, [why, accounts, file, agent, given, id]] of RESOLVE_ROWS.entries()) {
    const fx = makeFixture(`names-resolve-${index}`);
    holding(fx.root, file);

    assert.equal(resolveAccount(list(accounts), accountNames(fx.root, agent), given), id, why);
    fx.cleanup();
  }
});

// ——— Naming one (ACCT2): the person's word, kept beside the account; its own id, or none, clears it. A name is one word
// a terminal can type: no space, no backtick, no leading dash, at most 64 characters; never another account's id or
// name, in any case. What has no field is kept, and an entry or an agent left with nothing goes.

const RENAME_ROWS: [why: string, accounts: string, file: Cell, agent: string, account: string, name: Cell, after: string][] = [
  ['an account named', '["account-1","account-2"]', null, 'claude-code', 'account-1', 'work', '{"claude-code":{"account-1":{"name":"work"}}}'],
  ['a name is kept trimmed', '["account-1"]', null, 'claude-code', 'account-1', ' work ', '{"claude-code":{"account-1":{"name":"work"}}}'],
  ['a name replaced', '["account-1"]', '{"claude-code":{"account-1":{"name":"work"}}}', 'claude-code', 'account-1', 'seat', '{"claude-code":{"account-1":{"name":"seat"}}}'],
  ['its own name in another case', '["account-1"]', '{"claude-code":{"account-1":{"name":"work"}}}', 'claude-code', 'account-1', 'Work', '{"claude-code":{"account-1":{"name":"Work"}}}'],
  ['its own id clears its name, and an agent left with none goes', '["account-1"]', '{"claude-code":{"account-1":{"name":"work"}}}', 'claude-code', 'account-1', 'account-1', '{}'],
  ['none clears its name', '["account-1"]', '{"claude-code":{"account-1":{"name":"work"}},"codex":{"account-1":{"name":"seat"}}}', 'claude-code', 'account-1', null, '{"codex":{"account-1":{"name":"seat"}}}'],
  ['a clear keeps what the entry has no field for', '["account-1"]', '{"claude-code":{"account-1":{"name":"work","later":true}}}', 'claude-code', 'account-1', null, '{"claude-code":{"account-1":{"later":true}}}'],
  ['what has no field is kept', '["account-1","account-2"]', '{"codex":[1],"claude-code":{"account-1":{"name":"work","later":true},"account-2":{"name":"seat"}}}', 'claude-code', 'account-1', 'desk', '{"codex":[1],"claude-code":{"account-1":{"name":"desk","later":true},"account-2":{"name":"seat"}}}'],
  ['an entry is found in any case, and written under its own', '["account-1"]', '{"Claude-Code":{"Account-1":{"name":"work"}}}', 'claude-code', 'account-1', 'seat', '{"Claude-Code":{"Account-1":{"name":"seat"}}}'],
  ['a name in Chinese is kept as written', '["acct-3f9c2a71"]', null, 'claude-code', 'acct-3f9c2a71', '工作', '{"claude-code":{"acct-3f9c2a71":{"name":"工作"}}}'],
  ['an account not on this machine is refused', '["account-1"]', null, 'claude-code', 'account-9', 'work', 'refused missing'],
  ['a blank name is refused', '["account-1"]', null, 'claude-code', 'account-1', '  ', 'refused blank'],
  ['a name with a space is refused', '["account-1"]', null, 'claude-code', 'account-1', 'my seat', 'refused characters'],
  ['a name with a backtick is refused', '["account-1"]', null, 'claude-code', 'account-1', 'wo`rk', 'refused characters'],
  ['a name that starts with a dash is refused', '["account-1"]', null, 'claude-code', 'account-1', '-work', 'refused characters'],
  ['a name longer than 64 characters is refused', '["account-1"]', null, 'claude-code', 'account-1', 'a234567890b234567890c234567890d234567890e234567890f234567890g2345', 'refused long'],
  ['another account\'s name is refused, in any case', '["account-1","account-2"]', '{"claude-code":{"account-2":{"name":"work"}}}', 'claude-code', 'account-1', 'WORK', 'refused taken account-2'],
  ['another account\'s id is refused, in any case', '["account-1","work"]', null, 'claude-code', 'account-1', 'Work', 'refused taken work'],
  ['a name a gone account kept is free', '["account-1"]', '{"claude-code":{"account-9":{"name":"work"}}}', 'claude-code', 'account-1', 'work', '{"claude-code":{"account-9":{"name":"work"},"account-1":{"name":"work"}}}'],
  ['a file that does not read is refused and kept', '["account-1"]', 'not json', 'claude-code', 'account-1', 'work', 'refused unreadable'],
  ['a letter whose capital is two letters is not those two: straße is not STRASSE', '["account-1","account-2"]', '{"claude-code":{"account-2":{"name":"straße"}}}', 'claude-code', 'account-1', 'STRASSE', '{"claude-code":{"account-2":{"name":"straße"},"account-1":{"name":"STRASSE"}}}'],
  ['a dotted capital I is not an i with a dot above', '["account-1","account-2"]', '{"claude-code":{"account-2":{"name":"İzmir"}}}', 'claude-code', 'account-1', 'i\u{307}zmir', '{"claude-code":{"account-2":{"name":"İzmir"},"account-1":{"name":"i\\u0307zmir"}}}'],
];

test('a name is given as the driver gives it (the twin\'s table)', () => {
  for (const [index, [why, accounts, file, agent, account, name, after]] of RENAME_ROWS.entries()) {
    const fx = makeFixture(`names-rename-${index}`);
    holding(fx.root, file);
    const path = join(fx.root, ACCOUNTS_FILE);

    let said: string;
    try {
      renameAccount(fx.root, agent, list(accounts), account, name);
      said = readFileSync(path, 'utf8');
    } catch (error) {
      if (!(error instanceof AccountNameError)) throw error;
      said = `refused ${error.problem.kind}${error.problem.other ? ` ${error.problem.other}` : ''}`;
    }

    if (after.startsWith('refused')) {
      assert.equal(said, after, why);
      assert.equal(file === null ? !existsSync(path) : readFileSync(path, 'utf8') === file, true, `${why}: written`);
    } else {
      assert.deepEqual(JSON.parse(said), JSON.parse(after), why);
      assert.ok(said.endsWith('}\n') && !said.includes('\r'), `${why}: LF and a final newline`);
    }
    fx.cleanup();
  }
});

// ——— 🔴 Both twins write the same bytes for the same names.

const FILE_ROWS: [why: string, file: Cell, agent: string, account: string, name: Cell, after: string][] = [
  ['one agent, two accounts, the new one after', '{"claude-code":{"account-2":{"name":"seat"}}}', 'claude-code', 'account-1', 'work', '{"claude-code":{"account-2":{"name":"seat"},"account-1":{"name":"work"}}}'],
  ['a name goes first in its entry', '{"claude-code":{"account-1":{"later":true}}}', 'claude-code', 'account-1', 'work', '{"claude-code":{"account-1":{"name":"work","later":true}}}'],
  ['another agent first stays first', '{"codex":{"account-1":{"name":"seat"}}}', 'claude-code', 'account-2', 'work', '{"codex":{"account-1":{"name":"seat"}},"claude-code":{"account-2":{"name":"work"}}}'],
  ['the last name cleared leaves an empty file', '{"claude-code":{"account-1":{"name":"work"}}}', 'claude-code', 'account-1', null, '{}'],
];

test('both twins write the same file (the twin\'s table)', () => {
  for (const [index, [why, file, agent, account, name, after]] of FILE_ROWS.entries()) {
    const fx = makeFixture(`names-file-${index}`);
    holding(fx.root, file);

    renameAccount(fx.root, agent, ['account-1', 'account-2'], account, name);

    assert.equal(readFileSync(join(fx.root, ACCOUNTS_FILE), 'utf8'), `${JSON.stringify(JSON.parse(after), null, 2)}\n`, why);
    fx.cleanup();
  }
});

// ——— A sign-in into an account that is here (ACCT1): the one named, by its id or its name; none named is the machine's
// default; never one that is not here, and never a new one.

const TARGET_ROWS: [why: string, accounts: string, file: Cell, wiring: string, agent: string, given: Cell, reached: string][] = [
  ['an account by its id', '["account-1","account-2"]', null, '{}', 'claude-code', 'account-2', 'account-2'],
  ['an account by its name', '["account-1","account-2"]', '{"claude-code":{"account-2":{"name":"seat"}}}', '{}', 'claude-code', 'seat', 'account-2'],
  ['none named is the machine\'s default', '["account-1","account-2"]', null, '{"defaults":{"claude-code":"account-1"}}', 'claude-code', null, 'account-1'],
  ['an account named wins over the default', '["account-1","account-2"]', null, '{"defaults":{"claude-code":"account-1"}}', 'claude-code', 'account-2', 'account-2'],
  ['an account not here is refused, never made', '["account-1"]', null, '{}', 'claude-code', 'account-3', 'refused missing account-3'],
  ['none named and no default is refused', '["account-1"]', null, '{}', 'claude-code', null, 'refused none'],
  ['a default naming an account not here is refused', '["account-1"]', null, '{"defaults":{"claude-code":"account-9"}}', 'claude-code', null, 'refused missing account-9'],
  ['another agent\'s default is not this one\'s', '["account-1"]', null, '{"defaults":{"codex":"account-1"}}', 'claude-code', null, 'refused none'],
];

test('a sign-in reaches the account the driver reaches (the twin\'s table)', () => {
  for (const [index, [why, accounts, file, wiring, agent, given, reached]] of TARGET_ROWS.entries()) {
    const fx = makeFixture(`names-target-${index}`);
    holding(fx.root, file);
    writeFileSync(join(fx.root, 'harnesses.json'), wiring, 'utf8');
    const settings = readHarnessSettings(join(fx.root, 'harnesses.json'));

    const target = signInTarget(list(accounts), accountNames(fx.root, agent), settings.defaults[agent], given);

    assert.equal(target.account ?? `refused ${target.missing !== null ? `missing ${target.missing}` : 'none'}`, reached, why);
    fx.cleanup();
  }
});

// ——— The name a sign-in's end offers (ACCOUNTID1): who signed in, where no other account has it; else who with the
// organisation beside it, else with the plan, each a name the rename takes (one word: an organisation's spaces are dashes);
// else none, and the person names it. Never written: the person's rename writes it (D66 §3).

const OFFER_ROWS: [why: string, accounts: string, file: Cell, account: string, who: Cell, org: Cell, plan: Cell, offered: Cell][] = [
  ['who signed in is offered', '["acct-1","acct-2"]', null, 'acct-2', 'person@example.invalid', 'Org B', 'max', 'person@example.invalid'],
  ['an email another account is named takes its organisation beside it', '["acct-1","acct-2"]', '{"claude-code":{"acct-1":{"name":"person@example.invalid"}}}', 'acct-2', 'person@example.invalid', 'Org B', 'max', 'person@example.invalid·Org-B'],
  ['an email named in another case is named', '["acct-1","acct-2"]', '{"claude-code":{"acct-1":{"name":"Person@Example.invalid"}}}', 'acct-2', 'person@example.invalid', 'Org B', null, 'person@example.invalid·Org-B'],
  ['an organisation\'s spaces are one dash each, since a name is one word', '["acct-1","acct-2"]', '{"claude-code":{"acct-1":{"name":"person@example.invalid"}}}', 'acct-2', 'person@example.invalid', '  Org \u{9} of  B ', null, 'person@example.invalid·Org-of-B'],
  ['the plan where no organisation is said', '["acct-1","acct-2"]', '{"claude-code":{"acct-1":{"name":"person@example.invalid"}}}', 'acct-2', 'person@example.invalid', null, 'max', 'person@example.invalid·max'],
  ['the plan where the organisation\'s name is taken too', '["acct-1","acct-2","acct-3"]', '{"claude-code":{"acct-1":{"name":"person@example.invalid"},"acct-3":{"name":"person@example.invalid·Org-B"}}}', 'acct-2', 'person@example.invalid', 'Org B', 'pro', 'person@example.invalid·pro'],
  ['an organisation of nothing a name may hold is none', '["acct-1","acct-2"]', '{"claude-code":{"acct-1":{"name":"person@example.invalid"}}}', 'acct-2', 'person@example.invalid', ' ` ', 'max', 'person@example.invalid·max'],
  ['nothing tells them apart, so none is offered', '["acct-1","acct-2"]', '{"claude-code":{"acct-1":{"name":"person@example.invalid"}}}', 'acct-2', 'person@example.invalid', null, null, null],
  ['one organisation and no plan tell nothing apart', '["acct-1","acct-2","acct-3"]', '{"claude-code":{"acct-1":{"name":"person@example.invalid"},"acct-3":{"name":"person@example.invalid·Org-A"}}}', 'acct-2', 'person@example.invalid', 'Org A', null, null],
  ['an offer longer than a name may be is not made', '["acct-1","acct-2"]', '{"claude-code":{"acct-1":{"name":"a234567890b234567890c234567890d234567890e234567890f@x.invalid"}}}', 'acct-2', 'a234567890b234567890c234567890d234567890e234567890f@x.invalid', 'Org B', 'max', null],
  ['its own name is no other account\'s', '["acct-1"]', '{"claude-code":{"acct-1":{"name":"person@example.invalid"}}}', 'acct-1', 'person@example.invalid', 'Org A', 'max', 'person@example.invalid'],
  ['another agent\'s names are not this one\'s', '["acct-1","acct-2"]', '{"codex":{"acct-1":{"name":"person@example.invalid"}}}', 'acct-2', 'person@example.invalid', 'Org B', 'max', 'person@example.invalid'],
  ['nobody said is nothing offered', '["acct-1"]', null, 'acct-1', null, 'Org A', 'max', null],
];

test('a sign-in is offered the name the driver offers (the twin\'s table)', () => {
  for (const [index, [why, accounts, file, account, who, org, plan, offered]] of OFFER_ROWS.entries()) {
    const fx = makeFixture(`names-offer-${index}`);
    holding(fx.root, file);

    assert.equal(offeredName(list(accounts), accountNames(fx.root, 'claude-code'), account, who, org, plan), offered, why);
    fx.cleanup();
  }
});

// ——— What a sign-in's end says beside the email where another account already has it (ACCOUNTID1c): the organisation, else
// the plan, by the test the offer uses, so the sentence and the offered name tell the same part; none where the email is not
// another account's id or name, or where neither tells them apart.

const APART_ROWS: [why: string, accounts: string, file: Cell, account: string, who: Cell, org: Cell, plan: Cell, apart: Cell][] = [
  ['an email no other account has is said alone', '["acct-1","acct-2"]', null, 'acct-2', 'person@example.invalid', 'Org B', 'max', null],
  ['an email another account is named says the organisation after it', '["acct-1","acct-2"]', '{"claude-code":{"acct-1":{"name":"person@example.invalid"}}}', 'acct-2', 'person@example.invalid', 'Org B', 'max', 'Org B'],
  ['an email named in another case says it too', '["acct-1","acct-2"]', '{"claude-code":{"acct-1":{"name":"Person@Example.invalid"}}}', 'acct-2', 'person@example.invalid', '  Org B ', null, 'Org B'],
  ['the plan where no organisation is said', '["acct-1","acct-2"]', '{"claude-code":{"acct-1":{"name":"person@example.invalid"}}}', 'acct-2', 'person@example.invalid', null, 'max', 'max'],
  ['the plan where the organisation\'s name is taken too', '["acct-1","acct-2","acct-3"]', '{"claude-code":{"acct-1":{"name":"person@example.invalid"},"acct-3":{"name":"person@example.invalid·Org-B"}}}', 'acct-2', 'person@example.invalid', 'Org B', 'pro', 'pro'],
  ['nothing tells them apart, so nothing is said', '["acct-1","acct-2"]', '{"claude-code":{"acct-1":{"name":"person@example.invalid"}}}', 'acct-2', 'person@example.invalid', null, null, null],
  ['nobody said is nothing said', '["acct-1"]', null, 'acct-1', null, 'Org A', 'max', null],
];

test('a sign-in\'s end tells a second sign-in of an email apart as the driver does (the twin\'s table)', () => {
  for (const [index, [why, accounts, file, account, who, org, plan, apart]] of APART_ROWS.entries()) {
    const fx = makeFixture(`names-apart-${index}`);
    holding(fx.root, file);

    assert.equal(apartWords(list(accounts), accountNames(fx.root, 'claude-code'), account, who, org, plan), apart, why);
    fx.cleanup();
  }
});

test('the sign-in\'s end sentence says the organisation after the email, and "by that name" for the offered name', () => {
  assert.equal(signedInSentence('person@example.invalid', 'claude-code', null, 'acct-2', 'Org B'),
    'signed in as person@example.invalid · Org B — this machine lists it as `acct-2`.');
  assert.equal(signedInSentence('person@example.invalid', 'claude-code', 'person@example.invalid·Org-B', 'acct-2', 'Org B'),
    'signed in as person@example.invalid · Org B — this machine lists it by that name.');
  assert.equal(signedInSentence('person@example.invalid', 'claude-code', 'seat', 'acct-2', 'Org B'),
    'signed in as person@example.invalid · Org B — this machine lists it as `seat`.');
  assert.equal(signedInSentence('person@example.invalid', 'claude-code', null, 'acct-2', null),
    'signed in as person@example.invalid — this machine lists it as `acct-2`.');
});

test('a new account takes a fresh id its name never shifts from', () => {
  const drawn = ['3f9c2a71', '0b7e4d22'];

  // A drawn id an account already has is drawn again: an id is never reused, so a reading, a cool-off or a usage total kept
  // under one never lands on another account.
  assert.equal(newAccountId(['acct-3f9c2a71', 'account-1'], () => drawn.shift()!), 'acct-0b7e4d22');
  assert.match(newAccountId([]), /^acct-[0-9a-f]{8}$/);
});

test('an account reads as its name, and else its id', () => {
  const names = { 'account-1': 'work' };

  assert.equal(shownAs(names, 'account-1'), 'work');
  assert.equal(shownAs(names, 'ACCOUNT-1'), 'work');
  assert.equal(shownAs(names, 'account-2'), 'account-2');
});

test('an account removed forgets its name and no other; nothing to forget writes nothing', () => {
  const fx = makeFixture('names-forget');
  holding(fx.root, '{"claude-code":{"account-1":{"name":"work"},"account-2":{"name":"seat"}}}');

  forgetName(fx.root, 'claude-code', 'account-1');
  forgetName(fx.root, 'codex', 'account-1');

  assert.deepEqual(JSON.parse(readFileSync(join(fx.root, ACCOUNTS_FILE), 'utf8')), { 'claude-code': { 'account-2': { name: 'seat' } } });
  fx.cleanup();
});

test('each refusal says what was wrong and what is there, as the driver says it', () => {
  const fx = makeFixture('names-sentences');
  holding(fx.root, '{"claude-code":{"account-2":{"name":"work"}}}');
  const refused = (account: string, name: string, accounts = ['account-1', 'account-2']) => {
    try {
      renameAccount(fx.root, 'claude-code', accounts, account, name);
    } catch (error) {
      return (error as Error).message;
    }
    return '';
  };

  assert.match(refused('account-1', 'work'), /`work` already names `account-2`/);
  // ACCOUNTID1c: an email taken names the way out, the organisation beside it or a name of one's own; a plain name only the latter.
  assert.match(refused('account-1', 'work'), / and an id is one — give it a name of your own\.$/);
  assert.match(refused('account-1', 'person@example.invalid', ['account-1', 'person@example.invalid']),
    /name it `person@example\.invalid·<organization>`, with the organization it signed in to, or give it a name of your own\.$/);
  assert.match(refused('account-9', 'desk'), /no account `account-9`.*account-1, work \(account-2\)/);
  assert.match(refused('account-1', 'my seat', ['account-1']), /one word/);
  fx.cleanup();
});

// ——— The twin, held: the driver's tables are these tables, row for row and in this order.

const DRIVER_TABLE = driverTestFile('AccountNamesTwinTests.cs');

test('the driver’s tables are these tables, row for row and in this order', () => {
  const source = readFileSync(DRIVER_TABLE, 'utf8').replace(/\r\n/g, '\n');
  const rows = (method: string) => csharpRows(source, method, {}, 'AccountNamesTwinTests');

  assert.deepEqual(rows('A_name_reads_as_the_cli_reads_it'), READ_ROWS);
  assert.deepEqual(rows('An_account_resolves_as_the_cli_resolves_it'), RESOLVE_ROWS);
  assert.deepEqual(rows('A_name_is_given_as_the_cli_gives_it'), RENAME_ROWS);
  assert.deepEqual(rows('Both_twins_write_the_same_file'), FILE_ROWS);
  assert.deepEqual(rows('A_sign_in_reaches_the_account_the_cli_reaches'), TARGET_ROWS);
  assert.deepEqual(rows('A_sign_in_is_offered_the_name_the_cli_offers'), OFFER_ROWS);
  assert.deepEqual(rows('A_sign_in_end_tells_a_second_sign_in_apart_as_the_cli_does'), APART_ROWS);
});
