#!/usr/bin/env node
/**
 * A stand-in for a deployment's harness, for the question bench's tests (KNOWUSE2). It reads the check's prompt on
 * stdin and answers from a replies file: the first entry whose `when` the prompt contains. `{cwd}` in a reply is
 * replaced by the folder it was started in, and `{env:NAME}` by that variable or `(unset)`; `sleep` waits that many
 * milliseconds first; `code` is its exit code.
 * It starts no model and reads no account.
 *
 *   node tools/question-bench-fixtures/stub-harness.mjs <replies.json>
 */
import { readFileSync } from 'node:fs';

const [repliesFile] = process.argv.slice(2);
let input = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', (chunk) => { input += chunk; });
process.stdin.on('end', () => {
  const replies = JSON.parse(readFileSync(repliesFile, 'utf8'));
  const reply = replies.find((r) => input.includes(r.when));
  if (!reply) {
    process.stderr.write('the stub has no reply for this prompt\n');
    process.exit(4);
  }
  const answer = () => {
    process.stdout.write(String(reply.reply ?? '').replaceAll('{cwd}', process.cwd())
      .replace(/\{env:(\w+)\}/g, (_, name) => process.env[name] ?? '(unset)'));
    process.exitCode = reply.code ?? 0;
  };
  if (reply.sleep) setTimeout(answer, reply.sleep);
  else answer();
});
