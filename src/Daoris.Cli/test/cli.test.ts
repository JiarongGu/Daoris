import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { runCli } from '../src/cli.ts';
import { makeFixture } from './_fixture.ts';

test('--help prints usage and exits 0', () => {
  const out: string[] = [];
  const code = runCli(['--help'], process.cwd(), (s) => out.push(s));
  assert.equal(code, 0);
  assert.match(out.join('\n'), /daoris <command>/);
  for (const cmd of ['init', 'sync', 'check', 'upstream', 'index', 'status']) {
    assert.match(out.join('\n'), new RegExp(`\\b${cmd}\\b`));
  }
});

test('--version prints the package version', () => {
  const out: string[] = [];
  const code = runCli(['--version'], process.cwd(), (s) => out.push(s));
  assert.equal(code, 0);
  assert.match(out.join('\n'), /^\d+\.\d+\.\d+$/m);
});

test('an unknown command is a tool error (exit 2)', () => {
  const out: string[] = [];
  const code = runCli(['frobnicate'], process.cwd(), (s) => out.push(s));
  assert.equal(code, 2);
  assert.match(out.join('\n'), /unknown command/i);
});

/**
 * AGT1: the tools a session runs are AGENTS to a person — the owner, reading `daoris harness`:
 * *"I have no idea what harness is"*. The old verb is not a second name for the new one; it says
 * where the command went, once, and fails like any unknown command.
 */
test('the agent tools are `daoris agent`, and the old verb says where it went', () => {
  const out: string[] = [];
  assert.equal(runCli(['harness', 'list'], process.cwd(), (s) => out.push(s)), 2);
  assert.match(out.join('\n'), /`daoris agent`/);

  const help: string[] = [];
  runCli(['--help'], process.cwd(), (s) => help.push(s));
  assert.match(help.join('\n'), /^ {2}agent \[verb\]/m);
  assert.doesNotMatch(help.join('\n'), /^ {2}harness /m);
});

/**
 * Exit codes are the contract, and 1 is POLICY — the code a build gate reads as "the doctrine is
 * wrong". A failure nobody anticipated (a file system that refused, a shape nobody expected) is a tool
 * error, 2, said in one line. It escaped as a stack trace and Node's own exit 1 (REV3).
 */
test('a failure nobody anticipated is a tool error (exit 2) in one line, never a stack trace', async () => {
  const fx = makeFixture('cli-unanticipated');
  mkdirSync(join(fx.root, 'daoris.json'));
  const out: string[] = [];
  const code = await runCli(['check'], fx.root, (s) => out.push(s));
  assert.equal(code, 2);
  assert.match(out.join('\n'), /^daoris: /);
  fx.cleanup();
});

test('no arguments prints usage and exits 2', () => {
  const out: string[] = [];
  assert.equal(runCli([], process.cwd(), (s) => out.push(s)), 2);
});
