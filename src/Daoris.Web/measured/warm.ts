import { chromium, type FullConfig } from '@playwright/test';

// Vite compiles the page's modules on their first request, and on a loaded machine that took past a test's minute: in the CI
// beside on 3aeb4b81a (2026-10-20) the first story of each spec timed out at its navigation while the other 56 checks passed.
// One load here, before any check, with a bound of its own, pays that compile once (FIX-LOG, FLOWCHART1e).
export default async function warm(config: FullConfig): Promise<void> {
  const baseURL = config.projects[0]?.use.baseURL;
  if (!baseURL) throw new Error('the measured config names no baseURL: the warm-up has no page to load');
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    await page.goto(`${baseURL}/measured/flow.html?story=flow/Lifecycle`, { timeout: 300_000 });
    await page.waitForFunction(() => (window as unknown as { flowReady?: boolean }).flowReady === true, undefined, { timeout: 300_000 });
  } finally {
    await browser.close();
  }
}
