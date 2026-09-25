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

    /// <summary>Parses and validates JSON (from a file or the JSON pane). Base layouts resolve against the built-ins.</summary>
    public bool TryLoadJson(string json, bool keepHistory = true)
    {
        try
        {
            var l = Json.LoadLayout(json);
            if (l.Layers.Count == 0) throw new FormatException("the layout has no layers");
            Load(l, keepHistory);
            return true;
        }
        catch (Exception e) { Error = e.Message; Changed?.Invoke(); return false; }
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
                case "hold": key.Behavior = key.Behavior is ModTap m ? m with { Hold = action } : new ModTap(action); break;
                default: key.Flick[Enum.Parse<FlickDir>(Target)] = action; break;
            }
        });
    }

    // ---- What the canvas draws for a key ----

    public string Label(Key key) => Labels.LabelFor(key) is { Length: > 0 } s ? s : key.Tap is SpaceAction ? Layout.Name : "";
    public string Label(KeyAction a) => Labels.LabelFor(a) is { Length: > 0 } s ? s : a is SpaceAction ? "space" : a is NoneAction ? "none" : "";
    public static string? Icon(KeyAction a) => Icons.For(a);
    public static string? IconPath(string? name) => name is not null && Icons.Paths.TryGetValue(name, out var d) ? d : null;
}
