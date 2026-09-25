// Drives the editor like a person would and asserts what the layout ends up as (via the JSON pane).
export default async function (page) {
  const key = (label) => page.locator(`.kb .key[aria-label="${label}"]`).first();
  const json = async () => { await page.click('button[title="Show the layout as JSON"]'); const t = await page.inputValue('.json textarea', { timeout: 5000 }); await page.click('button[title="Show the layout as JSON"]'); return JSON.parse(t); };
  const check = (cond, msg) => { if (!cond) throw new Error('FAILED: ' + msg); console.log('ok: ' + msg); };

  // Fresh start (the draft may hold an earlier session).
  await page.selectOption('select[aria-label="Start from a built-in layout"]', 'qwerty');
  await key('q').click();
  check(await page.inputValue('.fields label:has-text("Tap") input') === 'q', 'selecting q shows its tap');

  // Palette: Editing → Copy onto q.
  await page.click('.tabs .tab:has-text("Editing")');
  await page.click('.codes .code[title="@copy"]');
  let l = await json();
  check(l.layers[0].rows[0].keys?.[0]?.tap?.util === 'Copy' || JSON.stringify(l.layers[0].rows[0]).includes('"util":"Copy"'), 'palette assigned @copy to the first key');

  // Undo restores q.
  await page.click('button[title="Undo"]');
  l = await json();
  check(JSON.stringify(l.layers[0].rows[0]).includes('qwertyuiop'), 'undo restored the row');

  // Drag w onto the right half of p.
  const from = await key('w').boundingBox(), to = await key('p').boundingBox();
  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
  await page.mouse.down();
  await page.mouse.move(from.x + from.width / 2 + 20, from.y + from.height / 2, { steps: 5 });
  await page.mouse.move(to.x + to.width * 0.8, to.y + to.height / 2, { steps: 20 });
  await page.mouse.up();
  await page.waitForTimeout(300);
  l = await json();
  const row0 = JSON.stringify(l.layers[0].rows[0]);
  check(row0.includes('qertyuiopw'), 'drag moved w after p: ' + row0.slice(0, 60));

  // Key look and gestures: layout margin, a key's label size, circle and long flick land in the JSON.
  await page.fill('section:has(h2:text("Key look")) label:has-text("Margin") input', '5');
  await page.press('section:has(h2:text("Key look")) label:has-text("Margin") input', 'Tab');
  await key('e').click();
  await page.fill('fieldset:has(legend:text("Look")) .three label:has-text("Label") input', '1.5');
  await page.press('fieldset:has(legend:text("Look")) .three label:has-text("Label") input', 'Tab');
  await page.fill('label:has-text("Circle") input', 'E');
  await page.press('label:has-text("Circle") input', 'Tab');
  await page.fill('label:has-text("Long flick") input', 'N=3');
  await page.press('label:has-text("Long flick") input', 'Tab');
  l = await json();
  const e = l.layers[0].rows[0].keys.find(k => k.tap?.text === 'e');
  check(l.keyMargin === 5, 'layout margin saved: ' + l.keyMargin);
  check(e?.labelScale?.label === 1.5 && e?.circle?.text === 'E' && e?.longFlick?.N?.text === '3', 'key label size, circle and long flick saved: ' + JSON.stringify(e).slice(0, 120));

  // Test typing: h e l l o through the real KeyboardState, with suggestions.
  await page.click('button[title="Type on the preview"]');
  for (const c of 'hello') await key(c).click();
  const typed = await page.textContent('.tester .typed');
  check(typed.includes('hello'), 'test typing shows "hello": ' + typed);
  const sugg = await page.textContent('.tester .suggest');
  check(sugg.length > 0, 'suggestions appear: ' + sugg);
}
