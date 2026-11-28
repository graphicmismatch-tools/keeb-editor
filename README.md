# keeb editor

**Use it: https://graphicmismatch.github.io/keeb-editor/**

A browser layout editor for [keeb](https://github.com/graphicmismatch-tools/keeb), the fully customizable on-screen keyboard, in the spirit of VIA for QMK. It runs anywhere a modern browser does (desktop or tablet) and needs no install.

- **Visual editing.** Drag keys to reorder them, including across rows, and drag the selected key's right edge to change its width (in quarter-key steps). Click a key to edit its tap, label, width, long-press list, all 8 flick directions plus out-and-back, per-direction flick-and-back and long flicks, a circle action, flick angles, mod-tap or tap-dance behavior, and swipeability.
- **Key look.** Margin, padding and label sizes (legend, flick characters, hint, popup, preview), for the whole layout or one key. The preview draws them the way the phone does.
- **Keycode palette.** Pick actions VIA-style, grouped as Basic, Keys, Layers (MO/TG/TO/OSL for every layer), Editing (cut/copy/paste, undo/redo, word jumps, Caps Word, case toggle), Media, System (Repeat, dynamic macros, hide or switch keyboard) and Special. You can also type any action in keeb's short syntax (`@macro:h|i|@enter`).
- **Layout-wide settings.**
  - Name, language, input method (Hangul, transliteration, Telex, kana→kanji, pinyin…) and font.
  - Layers: add, rename (every reference follows) and delete.
  - Accents, Shift overrides (`,=;`), flick angles, combos, chords (braille and other chorded layouts) and leader sequences.
  - The layout's own colors, with WCAG contrast checks.
- **Test typing.** This runs the real keeb engine, including autocorrect, suggestions, composers and layer switching, so what you type here is what the phone does.
- **Import from keyboard-layout-editor.com.** Open its Raw data (saved as a .txt) or downloaded JSON: widths and gaps carry over, legends become keys, and a top legend like `!` over `1` becomes a Shift override.
- **Changes from the base.** For a layout started from a built-in, a list of what differs (layers and rows, accents, Shift overrides, extras), and **As override of …**, which downloads only those changes with `base` set, so fixes to the built-in still reach it on the phone.
- **Safety net.** Undo/redo, a raw JSON view you can edit, an autosaved draft in the browser, and a warning whenever the layout wouldn't load on the phone.
- **Getting it to the phone.** Download the `.json`, then on the phone go to keeb → Layouts → Import.

## How it's built

Blazor WebAssembly. All layout logic comes from keeb itself through the `keeb` git submodule, so the editor can never disagree with the keyboard about the format:
- `Keeb.Core`: the layout model and JSON, geometry, action syntax, `LayoutEdit`, the keycode palette, themes and the test-typing engine.
- `Keeb.Render`: key icons (Material Symbols, Apache-2.0).

The only JavaScript is about 15 lines in `wwwroot/index.html`, for file download, the localStorage draft and the system color scheme.

| File | What it is |
|---|---|
| `src/KeebEditor/Editor.cs` | Editor state: the layout, visible layer, selection and undo/redo |
| `Components/Keyboard.razor` | The layer drawn from `Geometry`, with drag-and-drop and test typing |
| `Components/Inspector.razor` | The selected key |
| `Components/Palette.razor` | The keycode palette |
| `Components/LayoutPanel.razor` | Layout-wide settings, layers and colors |
| `Pages/Home.razor` | Toolbar, panes and open/download |

## Run

It needs a .NET 10 SDK.

```sh
git clone --recurse-submodules <this repo>    # or: git submodule update --init
dotnet run --project src/KeebEditor           # → http://localhost:5000 (the port is printed)
```

Every push to `master` publishes it to GitHub Pages (`.github/workflows/pages.yml`). To host it anywhere else static, publish it:
```sh
dotnet publish src/KeebEditor -c Release -o out   # serve out/wwwroot
```

The browser test clicks through the editor: select a key, assign from the palette, undo, drag a key, and test-type with suggestions. It runs in dark and light mode.
```sh
dotnet run --project src/KeebEditor --urls http://localhost:5199 &
cd tests/e2e && npm install && npm test
```

When keeb changes, pull its latest version with `git submodule update --remote keeb`.

## License

MIT © graphicmismatch. Bundled keeb data keeps its own licenses; see `keeb/THIRD_PARTY_NOTICES.md`.
