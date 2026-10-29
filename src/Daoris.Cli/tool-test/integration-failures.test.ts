import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * INTEGRATE1f part f1 (D207, the integration design §5.3): the failure reader and the signature, held to ONE table with the driver's
 * twin. `tools/test-failures.mjs` reads `fixtures/failure-signatures.json` here row for row, and the driver's `FailureReader` reads it
 * there (`FailureReadingTests`), so a change to one reader without the other fails its own suite. The table's expected values were read
 * from this tool; the driver's reader must give the same. Run by the `tool-tests` gate.
 */

// The census of test units reads this file's strings as data, not as reads; what it reads is declared.
// test-map: strings name paths as data
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/failure-signatures.json — INTEGRATE1f's driver twin reads the table row for row

interface Frame { method: string; file: string | null; line: number | null }
interface Failure { runner: string; unit: string | null; test: string; exception: string; message: string; frames: Frame[] }
interface Signature { test?: string; exception?: string; location?: { method: string; text: string; ordinal: number }; message?: string; gate?: string; step?: string; errors?: string[] }
interface Report { name: string; kind: 'trx' | 'vitest' | 'tap'; text?: string; json?: unknown; failures: Failure[] }

const f = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam merge-queue.test.ts documents
  '../../../tools/test-failures.mjs') as {
  normalise: (line: string) => string;
  dotnetMethod: (method: string) => string;
  dotnetFrames: (stack: string) => Frame[];
  v8Frames: (stack: string) => Frame[];
  exceptionOf: (message: string, named?: string | null) => string;
  repositoryPath: (file: string | null, root?: string | null) => string | null;
  readReport: (text: string, unit?: string | null) => Failure[];
  signatureOf: (failure: Failure, read?: ((path: string) => string | null) | null, root?: string | null) => Signature;
  gateSignature: (gate: string, step: string, output: string, root?: string | null) => Signature;
  signatureKey: (signature: Signature) => string;
  sameSignature: (a: Signature, b: Signature) => boolean;
};

const TABLE = JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/failure-signatures.json'), 'utf8')) as {
  normalise: { in: string; out: string }[];
  methods: { in: string; out: string }[];
  dotnetFrames: { stack: string; frames: Frame[] };
  v8Frames: { stack: string; frames: Frame[] };
  exceptions: { message: string; named: string | null; out: string }[];
  paths: { file: string | null; root: string | null; out: string | null }[];
  reports: Report[];
  sources: Record<string, string>;
  signatures: { name: string; failure: Failure; root?: string; signature: Signature; key: string }[];
  gates: { name: string; gate: string; step: string; output: string; root: string | null; signature: Signature; key: string }[];
};

const read = (path: string) => TABLE.sources[path] ?? null;

describe('INTEGRATE1f: the failure reader, row for row with the driver', () => {
  test('a line is normalised: GUIDs, hex runs, paths and numbers replaced, its spaces collapsed', () => {
    for (const row of TABLE.normalise) assert.equal(f.normalise(row.in), row.out, row.in);
  });

  test("a .NET method loses its compiler's wrapping, and its frames are read from a stack", () => {
    for (const row of TABLE.methods) assert.equal(f.dotnetMethod(row.in), row.out, row.in);
    assert.deepEqual(f.dotnetFrames(TABLE.dotnetFrames.stack), TABLE.dotnetFrames.frames);
    assert.deepEqual(f.v8Frames(TABLE.v8Frames.stack), TABLE.v8Frames.frames);
  });

  test("an exception type comes from the message's first line, or from the name it is given", () => {
    for (const row of TABLE.exceptions) assert.equal(f.exceptionOf(row.message, row.named), row.out, JSON.stringify(row.message));
  });

  test('a path is repository-relative from its root, else from its first src, tools, examples or canon part', () => {
    for (const row of TABLE.paths) assert.equal(f.repositoryPath(row.file, row.root), row.out, `${row.file} under ${row.root}`);
  });

  test("a report's failures are read by its kind: a trx, vitest's JSON or node --test's TAP", () => {
    for (const report of TABLE.reports) {
      const text = report.kind === 'vitest' ? JSON.stringify(report.json) : report.text!;
      assert.deepEqual(f.readReport(text, null), report.failures, report.name);
    }
    assert.throws(() => f.readReport('nothing a reader knows'), /not a trx, a vitest JSON report or a node --test TAP report/);
  });

  test("a failure's signature is its test, exception and location, or its normalised message, and its key is one string", () => {
    for (const row of TABLE.signatures) {
      const signature = f.signatureOf(row.failure, read, row.root ?? null);
      assert.deepEqual(signature, row.signature, row.name);
      assert.equal(f.signatureKey(signature), row.key, row.name);
    }
  });

  test("a gate that names no failing test is signed by its step: a build by the set of its errors, else its first error line", () => {
    for (const row of TABLE.gates) {
      const signature = f.gateSignature(row.gate, row.step, row.output, row.root);
      assert.deepEqual(signature, row.signature, row.name);
      assert.equal(f.signatureKey(signature), row.key, row.name);
    }
  });

  test('the table holds its rows', () => {
    assert.ok(TABLE.normalise.length >= 6 && TABLE.signatures.length >= 10 && TABLE.gates.length >= 9);
    assert.equal(TABLE.reports.map((report) => report.kind).join(), 'trx,vitest,tap');
  });
});
