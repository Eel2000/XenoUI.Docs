# Localization

[Documentation](index.md) · [Layouts §10](layouts.md) · [Accessibility](accessibility.md) · [Native capabilities](native-capabilities.md)

XenoUI does **not** ship a string-resource system: no `.resx` loader, no `R.string`, no `{Binding}` to a translation
table. What it handles is the part that is hard to get right in a custom renderer — **text shaping, bidirectional text and
right-to-left layout**. This page shows both: what the framework does for you, and a simple, testable pattern for the
strings.

## What the framework handles

| Concern | Support |
|---|---|
| **Complex scripts** | Text is shaped by the engine (HarfBuzz): accents, emoji, CJK and Arabic render correctly. |
| **Bidirectional text** | Runs are ordered by the first strong letter; Latin inside Arabic and the reverse display correctly. |
| **Right-to-left layout** | `FlowDirection(RightToLeft)` on a subtree mirrors it; rows start at the right, the left padding/margin is the *start* side, `TextAlign(Start)` aligns right, hit-testing follows. |
| **Device direction** | With `FlowDirection.Inherit` (the default) the app lays out right-to-left when the device language is, through `ILayoutDirectionCapability`. |
| **IME and input** | Native keyboard and language input for every script. |

Use `Start`/`End` and `Spacing.Directional(start, top, end, bottom)` instead of left/right, and check every component with
`[Preview("…", RightToLeft = true)]` ([Layouts §10](layouts.md)).

```csharp
// Force or follow the device
FlowDirection = FlowDirection.RightToLeft;       // on XenoApp: the whole app
// Or on a subtree:
Column(...).FlowDirection(FlowDirection.RightToLeft)
```

## What you provide: the strings

Keep strings out of the UI code behind one small abstraction. A minimal pattern:

```csharp
public sealed class Strings
{
    private static readonly Dictionary<string, Dictionary<string, string>> Tables = new()
    {
        ["en"] = new() { ["notes.title"] = "Notes", ["notes.add"] = "Add", ["notes.count"] = "{0} notes" },
        ["fr"] = new() { ["notes.title"] = "Notes", ["notes.add"] = "Ajouter", ["notes.count"] = "{0} notes" },
        ["ar"] = new() { ["notes.title"] = "ملاحظات", ["notes.add"] = "إضافة", ["notes.count"] = "{0} ملاحظات" },
    };

    public static string Language { get; private set; } = "en";
    public static void Use(string language) => Language = Tables.ContainsKey(language) ? language : "en";

    public static string Get(string key, params object[] args)
    {
        string text = Tables[Language].TryGetValue(key, out var value) ? value : Tables["en"][key];
        return args.Length == 0 ? text : string.Format(text, args);
    }
}
```

Used in components:

```csharp
Text(Strings.Get("notes.title"), 28)
Button(Strings.Get("notes.add"), Add)
Text(Strings.Get("notes.count", _notes.Count), 16)
```

For larger apps, load tables from embedded JSON or `.resx` files with the standard .NET `ResourceManager`, and read
`CultureInfo.CurrentUICulture` once at startup.

### Changing language at run time

Strings are read **at composition**, so a language change needs a rebuild:

```csharp
// In your XenoApp subclass (the SampleApp's SetArabicNavigation does the same):
internal void UseLanguage(string language, bool rightToLeft)
{
    Strings.Use(language);
    FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.Inherit;
    _navigation.RefreshChrome();            // rebuilds the tab bar / rail / drawer slots
}

// From a page:
((App)Application).UseLanguage("ar", rightToLeft: true);
Rebuild();
```

The scaffold's slots (tab bar, rail, drawer) are rebuilt with `RefreshChrome()`
([sample](sample-app.md)). A `State<string>` bound to a `Text(...)` updates without a
rebuild but only suits a few strings.

## Formatting numbers, dates and plurals

Use .NET's culture-aware formatting; the engine only draws the resulting string:

```csharp
Text(price.ToString("C", culture))        // currency
Text(date.ToString("D", culture))         // long date
```

Plurals differ per language ("1 note" / "2 notes", and more forms in Arabic or Russian): use a library that implements CLDR
plural rules, or add a plural key per form in your tables. Do not build sentences by concatenation: use whole translated
strings with placeholders (`"{0} notes"`), because word order changes between languages.

## Testing

- Render each language and RTL variant headlessly: `[Preview("Notes (ar)", RightToLeft = true)]`, or a test that sets
  `Strings.Use("ar")` then calls `PreviewRenderer.Render` ([Testing](testing.md)).
- Test with **long** strings (German, Finnish) and **short** scripts (CJK): a layout built with `FlexGrow` and `MaxLines`
  survives both.
- Test with large system fonts ([Accessibility](accessibility.md)).

## Checklist

- [ ] No user-visible string in a component: everything goes through `Strings.Get`.
- [ ] `Start`/`End` and `Spacing.Directional`, never absolute left/right.
- [ ] Sentences are whole strings with placeholders.
- [ ] Culture-aware formatting for numbers and dates.
- [ ] A preview per language and per direction.
- [ ] Layout checked with long text.
