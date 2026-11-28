using Keeb.Core;
using Keeb.Render;

namespace KeebEditor;

/// <summary>
/// Editor state shared by every panel: the layout being edited, the visible layer, the selected key, undo/redo.
/// All layout logic lives in Keeb.Core (LayoutEdit, ActionSyntax, Json, Themes), the same code the phone runs.
/// </summary>
public sealed class Editor
{
    public Layout Layout { get; private set; } = Json.BuiltinLayout("qwerty");
    public string LayerId { get; set; } = "base";
    public Key? Selected { get; private set; }
    /// <summary>Where a palette click goes: "tap", "long", "hold" (mod-tap), or a flick direction name.</summary>
    public string Target { get; set; } = "tap";
    public string PreviewTheme { get; set; } = Themes.Auto;
    public bool SystemDark { get; set; } = true;
    public string? Error { get; private set; }
    /// <summary>The built-in layout this one started from (or names as its base), for "changes from base" and override downloads.</summary>
    public string? BaseName { get; set; }
    public KeyboardState Labels { get; private set; } = null!;

    readonly Stack<string> undo = new(), redo = new();
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;

    /// <summary>Raised after every change; the page re-renders and autosaves.</summary>
    public event Action? Changed;

    public Editor() => Refresh();

    public Layer Layer => Layout.Find(LayerId) ?? Layout.Layers[0];
    public List<string> Problems => Layout.Validate();
    public Theme Theme => Themes.Resolve(Layout, new Settings { ThemeName = PreviewTheme }, SystemDark);
    public string SaveJson() => Json.Save(Layout);

    public void Load(Layout layout, bool keepHistory = false)
    {
        if (keepHistory) Snapshot(); else { undo.Clear(); redo.Clear(); }
        Layout = layout;
        if (Layout.Find(LayerId) is null) LayerId = Layout.Layers[0].Id;
        Selected = null;
        Refresh();
    }

    /// <summary>
    /// Parses and validates JSON (from a file or the JSON pane). Base layouts resolve against the built-ins.
    /// Anything without layers is tried as keyboard-layout-editor.com data.
    /// </summary>
    public bool TryLoadJson(string json, bool keepHistory = true)
    {
        try
        {
            bool keeb = json.Contains("\"layers\"") || json.Contains("\"base\"");
            var l = keeb ? Json.LoadLayout(json) : Kle.Import(json);
            if (keeb && BaseOf(json) is { } b) BaseName = b;
            if (l.Layers.Count == 0) throw new FormatException("the layout has no layers");
            Load(l, keepHistory);
            return true;
        }
        catch (Exception e) { Error = e.Message; Changed?.Invoke(); return false; }
    }

    static string? BaseOf(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json, new() { AllowTrailingCommas = true, CommentHandling = System.Text.Json.JsonCommentHandling.Skip });
            return doc.RootElement.TryGetProperty("base", out var b) && b.GetString() is { Length: > 0 } name && Json.BuiltinLayoutNames.Contains(name) ? name : null;
        }
        catch (System.Text.Json.JsonException) { return null; }
    }

    /// <summary>The layout as an override of BaseName (only what differs), or null when it can't be one.</summary>
    public Layout? Override
    {
        get
        {
            if (BaseName is not { } b || !Json.BuiltinLayoutNames.Contains(b)) return null;
            var key = b + "\n" + Json.Save(Layout); // every panel asks on each render: work it out once per version
            if (overrideFor != key) (overrideFor, overrideOf) = (key, LayoutEdit.AsOverride(Layout, Json.BuiltinLayout(b), b));
            return overrideOf;
        }
    }
    string? overrideFor;
    Layout? overrideOf;

    /// <summary>What differs from the base, one line each, for the Changes panel.</summary>
    public List<string> Changes()
    {
        if (BaseName is not { } b || !Json.BuiltinLayoutNames.Contains(b)) return [];
        if (Override is not { } o) return ["Some base rows, layers or extras were removed or reordered, so this can only be saved as a whole layout."];
        var baseLayout = Json.BuiltinLayout(b);
        var lines = new List<string>();
        void Layers(List<Layer>? layers, string what)
        {
            foreach (var l in layers ?? [])
            {
                var rows = l.Rows.Select((r, i) => (r, i)).Where(x => x.r is not null).Select(x => x.i + 1).ToList();
                bool isNew = (what == "" ? baseLayout.Find(l.Id) : baseLayout.Landscape?.Find(x => x.Id == l.Id)) is null;
                lines.Add(isNew ? $"New {what}layer {l.Id}" : $"{what}{l.Id}: row{(rows.Count == 1 ? "" : "s")} {string.Join(", ", rows)}");
            }
        }
        Layers(o.Layers, "");
        Layers(o.Landscape, "landscape ");
        if (o.Accents.Count > 0) lines.Add("Accents: " + string.Join(' ', o.Accents.Keys));
        if (o.Shifted.Count > 0) lines.Add("Shift overrides: " + string.Join(' ', o.Shifted.Keys));
        if (o.Combos.Count + o.Chords.Count + o.Leaders.Count > 0) lines.Add($"Added {o.Combos.Count} combos, {o.Chords.Count} chords, {o.Leaders.Count} leader sequences");
        if (o.Composer is not null) lines.Add("Input method: " + o.Composer);
        if (o.Theme is not null) lines.Add("Own colors");
        if (o.Font is not null) lines.Add("Font: " + o.Font);
        if (o.Language != baseLayout.Language) lines.Add("Language: " + o.Language);
        return lines;
    }

    public void Select(Key? key) { Selected = key; Error = null; Changed?.Invoke(); }

    public void ShowLayer(string id) { LayerId = id; Selected = null; Changed?.Invoke(); }

    /// <summary>Runs an edit with undo. If it throws (bad input), the layout is restored and the message shown.</summary>
    public void Change(Action edit)
    {
        var before = Json.Save(Layout);
        var slot = Slot();
        try
        {
            edit();
            undo.Push(before);
            redo.Clear();
            Error = null;
        }
        catch (Exception e)
        {
            Layout = Json.LoadLayout(before);
            Reselect(slot);
            Error = e.Message;
        }
        Refresh();
    }

    float resizeFrom;
    /// <summary>Dragging a key's right edge: one undo step for the whole drag, snapped by LayoutEdit.ResizeWidth.</summary>
    public void BeginResize(Key key) { Snapshot(); resizeFrom = key.Width; }
    public void Resize(Key key, float dragUnits) { key.Width = LayoutEdit.ResizeWidth(resizeFrom, dragUnits); Changed?.Invoke(); }
    public void EndResize(Key key) { if (key.Width == resizeFrom) undo.Pop(); Changed?.Invoke(); }

    public void Undo() => Step(undo, redo);
    public void Redo() => Step(redo, undo);

    void Step(Stack<string> from, Stack<string> to)
    {
        if (from.Count == 0) return;
        var slot = Slot();
        to.Push(Json.Save(Layout));
        Layout = Json.LoadLayout(from.Pop());
        if (Layout.Find(LayerId) is null) LayerId = Layout.Layers[0].Id;
        Reselect(slot);
        Refresh();
    }

    void Snapshot() { undo.Push(Json.Save(Layout)); redo.Clear(); }

    (int Row, int Index)? Slot() =>
        Selected is not null && LayoutEdit.Locate(Layer, Selected) is var (row, i) ? (Layer.Rows.IndexOf(row), i) : null;

    void Reselect((int Row, int Index)? slot) =>
        Selected = slot is var (r, i) && r < Layer.Rows.Count && i < Layer.Rows[r].Keys.Count ? Layer.Rows[r].Keys[i] : null;

    void Refresh()
    {
        Labels = new KeyboardState([Layout], new Settings(), _ => null, _ => null);
        Changed?.Invoke();
    }

    /// <summary>Puts a palette action where Target says, on the selected key.</summary>
    public void Assign(KeyAction action)
    {
        if (Selected is not { } key) { Error = "Select a key first."; Changed?.Invoke(); return; }
        Change(() =>
        {
            switch (Target)
            {
                case "tap": key.Tap = action; break;
                case "long": key.LongPress.Add(action); break;
                case "circle": key.Circle = action; break;
                case "hold": key.Behavior = key.Behavior is ModTap m ? m with { Hold = action } : new ModTap(action); break;
                default: key.Flick[Enum.Parse<FlickDir>(Target)] = action; break;
            }
        });
    }

    // ---- What the canvas draws for a key ----

    public string Label(Key key) => Labels.LabelFor(key) is { Length: > 0 } s ? s : key.Tap is SpaceAction ? Layout.Name : "";
    public string Label(KeyAction a) => Labels.LabelFor(a) is { Length: > 0 } s ? s : a is SpaceAction ? "space" : a is NoneAction ? "none" : "";
    public static string? Icon(KeyAction a) => Icons.For(a);
    public static string? IconPath(string? name) =>
        name is not null && Icons.Paths.TryGetValue(IsMirrored(name) ? name[..^Icons.Mirrored.Length] : name, out var d) ? d : null;
    /// <summary>Icons.Mirrored names draw their base icon flipped; this is the SVG transform for that, else null.</summary>
    public static string? IconTransform(string? name) => name is not null && IsMirrored(name) ? "matrix(-1 0 0 1 960 0)" : null;
    static bool IsMirrored(string name) => name.EndsWith(Icons.Mirrored);
}
