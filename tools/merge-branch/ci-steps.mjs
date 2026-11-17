/**
 * What a CI run saves itself: the one build the web gates would each wait on.
 */
import { closeSync, openSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

/** The page package's scripts, none where it has none or does not read. */
export function pageScripts(root) {
  try {
    const { scripts } = JSON.parse(readFileSync(join(root, 'src', 'Daoris.Web', 'package.json'), 'utf8'));
    return scripts && typeof scripts === 'object' ? scripts : {};
  } catch {
    return {};
  }
}

/** The part of a command that names the root script `npm run <script>`, past its flags, or null. */
const rootScript = (text) => /^npm\s+run\s+(?:-\S+\s+)*([^\s-]\S*)/.exec(text)?.[1] ?? null;

/** The part of a command that names a script of the page's package, `npm --prefix <folder> run <script>`, or null. */
const pageScript = (text) => /^npm\s+--prefix\s+\S+\s+run\s+(?:-\S+\s+)*([^\s-]\S*)/.exec(text)?.[1] ?? null;

/**
 * The gates whose command runs a script of the page's package that has a `pre<script>` running the contracts' generator: read
 * through the root scripts a gate names, as `gateComponents` reads them. The page's pre-scripts are what builds the contracts, so
 * these are the gates that wait on it.
 */
export function contractsGates(gates, scripts, pageScripts) {
  const reaches = (command, seen) => command.split(/&&|\|\||;/).some((part) => {
    const text = part.trim();
    const root = rootScript(text);
    if (root) return !seen.has(root) && typeof scripts[root] === 'string' && reaches(scripts[root], new Set([...seen, root]));
    const page = pageScript(text);
    return page !== null && /contracts\.mjs/.test(pageScripts[`pre${page}`] ?? '');
  });
  return gates.filter((gate) => reaches(gate.run, new Set())).map((gate) => gate.name);
}

/**
 * The contracts' generator run once, `--if-stale`, as the page's pre-scripts run it, into `contracts.log` of `dir`: the answer is
 * the exit code, never a rejection. A failed step is no failure of the run: each gate it served runs its own pre-script, which says
 * what failed. `step` is `runStep`'s shape, handed the run's trees.
 */
export async function startContractsStep(root, dir, step) {
  const fd = openSync(join(dir, 'contracts.log'), 'w');
  try {
    return await step('node tools/contracts.mjs --if-stale', root, fd, {});
  } catch {
    return 1;
  } finally {
    closeSync(fd);
  }
}
