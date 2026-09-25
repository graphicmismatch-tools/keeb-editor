// End-to-end check of the editor in a real browser.
//   dotnet run --project src/KeebEditor --urls http://localhost:5199   (in another terminal)
//   cd tests/e2e && npm install && npm test
// Uses an installed Chrome (CHROME=/path/to/chrome to override). Writes screenshots next to this file.
import { chromium } from 'playwright-core';
import steps from './steps.mjs';

const url = process.env.EDITOR_URL ?? 'http://localhost:5199/';
const browser = await chromium.launch({ executablePath: process.env.CHROME ?? '/usr/bin/google-chrome', args: ['--no-sandbox'] });
let failed = false;
for (const scheme of ['dark', 'light']) {
  const page = await browser.newPage({ viewport: { width: 1500, height: 1250 }, colorScheme: scheme });
  const errors = [];
  page.on('pageerror', e => errors.push(String(e)));
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
  await page.goto(url);
  await page.waitForSelector('.kb .key', { timeout: 60000 });
  try { await steps(page); } catch (e) { failed = true; console.log(String(e).split('\n')[0]); }
  await page.screenshot({ path: `editor-${scheme}.png`, fullPage: true });
  if (errors.length) { failed = true; console.log('console errors:\n' + errors.join('\n')); }
  await page.close();
}
await browser.close();
process.exit(failed ? 1 : 0);
