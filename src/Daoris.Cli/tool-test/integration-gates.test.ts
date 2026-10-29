import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * INTEGRATE1f part f1 (the integration design §5.5, §5.9): the environment a gate is composed with and the digest of a gate's
 * composition, held to ONE table with the driver's twin. `tools/merge-branch.mjs` (`gateEnvironment`, `GATE_REMOVED`,
 * `BESIDE_ENVIRONMENT`, `compositionDigest`) reads `fixtures/gate-environment.json` here, row for row, and the driver's
 * `GateEnvironment` and `GateComposition` read it there (`GateEnvironmentTests`). The table's `driverOnly` rows are what the driver's
 * gate is given that the tool's is not yet; this file holds the tool to nothing there. Run by the `tool-tests` gate.
 */

// The census of test units reads this file's strings as data, not as reads; what it reads is declared.
// test-map: strings name paths as data
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/gate-environment.json — INTEGRATE1f's driver twin reads the table row for row

type Environment = Record<string, string>;
interface Composition { name: string; units: string[] | null; settings: Record<string, unknown>; digest: string }

const tool = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam merge-queue.test.ts documents
  '../../../tools/merge-branch.mjs') as {
  gateEnvironment: (start?: Environment, options?: { pass?: readonly string[] }) => Environment;
  GATE_REMOVED: readonly RegExp[];
  BESIDE_ENVIRONMENT: Environment;
  compositionDigest: (units: string[] | null, settings: Record<string, unknown>) => string;
};

const TABLE = JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/gate-environment.json'), 'utf8')) as {
  beside: Environment;
  removed: string[];
  environments: { name: string; start: Environment; pass: string[]; expect: Environment }[];
  driverOnly: { holds: unknown[] };
  compositions: Composition[];
};

describe('INTEGRATE1f: the gate environment and the composition digest, row for row with the driver', () => {
  test("the build servers a gate gets and the names it never inherits are the table's, the tool's own stand-in pair aside", () => {
    assert.deepEqual(tool.BESIDE_ENVIRONMENT, TABLE.beside);
    const rules = tool.GATE_REMOVED.map((pattern) => `/${pattern.source}/${pattern.flags}`);
    assert.deepEqual(rules.slice(0, TABLE.removed.length), TABLE.removed);
    assert.equal(rules.length, TABLE.removed.length + 1, 'the tool adds its tool tests\' two stand-in variables, and nothing else');
  });

  for (const row of TABLE.environments) {
    test(row.name, () => {
      assert.deepEqual(tool.gateEnvironment(row.start, { pass: row.pass }), row.expect);
    });
  }

  for (const row of TABLE.compositions) {
    test(`a composition's digest: ${row.name}`, () => {
      assert.equal(tool.compositionDigest(row.units, row.settings), row.digest);
    });
  }

  test('the table holds its rows, and the driver-only ones are not the tool\'s to hold', () => {
    assert.ok(TABLE.environments.length >= 8 && TABLE.compositions.length >= 5 && TABLE.driverOnly.holds.length >= 3);
  });
});
