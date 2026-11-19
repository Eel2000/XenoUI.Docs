// Every sample of the "Your first app" tutorial. The pages are only compiled (they need a running
// application); the static components are also rendered into the tutorial's screenshots.
using XenoUI.Core.Text;

namespace DocSnippets.Tutorial;

public sealed record Note(int Id, string Title, string Body);

// ── Step 1: a first screen ──────────────────────────────────────────────────────────────
public static class Step1
{
    public static XenoComponent Hello() =>
        Column(
            Text("Notes", 28).TextColor(0xFF111827),
            Text("My first XenoUI screen", 14).TextColor(0xFF6B7280)
        ).Gap(6).Padding(24);
}

// ── Step 2: layout and a reusable component ─────────────────────────────────────────────
public static class NoteCard
{
    public static XenoComponent View(string title, string body) =>
        Column(
            Text(title, 16).TextColor(0xFF111827),
            Text(body, 13).TextColor(0xFF6B7280).MaxLines(2, ellipsis: true)
        ).Gap(4).Padding(16)
         .Background(0xFFFFFFFF).CornerRadius(16).Elevation(2);
}

public static class Step2
{
    public static XenoComponent Screen() =>
        Column(
            Text("Notes", 28).TextColor(0xFF111827),
            NoteCard.View("Shopping", "Milk, eggs, bread and something for dinner tonight"),
            NoteCard.View("Ideas", "A tiny app that renders everything with Skia"),
            NoteCard.View("Call Amina", "About the weekend")
        ).Gap(12).Padding(16).CrossAxis(CrossAxisAlignment.Stretch).Background(0xFFF3F4F6);
}

// ── Step 3: state ───────────────────────────────────────────────────────────────────────
public static class Step3
{
    public static XenoComponent Counter(State<int> count, Action add) =>
        Row(
            Text($"{count} notes", 16).TextColor(0xFF111827).FlexGrow(1),
            Button("Add", add).Background(0xFF6D28D9).CornerRadius(12).Padding(16, 10)
        ).Gap(12).CrossAxis(CrossAxisAlignment.Center)
         .Padding(16).Background(0xFFFFFFFF).CornerRadius(16);
}

public sealed class CounterPage : XenoPage
{
    private readonly State<int> _count = UseState(3);          // a field, never in Body()

    public override XenoComponent Body() =>
        Column(Step3.Counter(_count, () => _count.Value++))
            .Padding(16).Background(0xFFF3F4F6).CrossAxis(CrossAxisAlignment.Stretch);
}

// ── Step 4: input and a list ────────────────────────────────────────────────────────────
public static class Step4
{
    public static XenoComponent AddBar(TextEditController input, Action add) =>
        Row(
            TextField(input, "New note").Height(48).FlexGrow(1),
            Button("Add", add).Background(0xFF6D28D9).CornerRadius(12).Padding(16, 12)
        ).Gap(8).CrossAxis(CrossAxisAlignment.Center);

    public static XenoComponent Screen(TextEditController input, IReadOnlyList<Note> notes) =>
        Column(
            AddBar(input, () => { }),
            LazyColumn(notes, note => NoteCard.View(note.Title, note.Body).Margin(Spacing.Only(bottom: 10)),
                key: note => note.Id).FlexGrow(1).CrossAxis(CrossAxisAlignment.Stretch)
        ).Gap(12).Padding(16).Background(0xFFF3F4F6).CrossAxis(CrossAxisAlignment.Stretch);
}

public sealed class NotesPage : XenoPage
{
    private readonly TextEditController _input = UseTextEdit();
    private readonly List<Note> _notes = new();
    private readonly LazyListState _list = UseLazyList();
    private int _nextId;

    public override XenoComponent Body() =>
        Column(
            Row(
                TextField(_input, "New note").Height(48).FlexGrow(1),
                Button("Add", Add).Background(0xFF6D28D9).CornerRadius(12).Padding(16, 12)
            ).Gap(8).CrossAxis(CrossAxisAlignment.Center),
            _notes.Count == 0
                ? Text("No notes yet. Write one above.", 14).TextColor(0xFF6B7280)
                : LazyColumn(_notes, NoteRow, key: note => note.Id, state: _list)
                    .FlexGrow(1).CrossAxis(CrossAxisAlignment.Stretch)
        ).Gap(12).Padding(16).Background(0xFFF3F4F6).CrossAxis(CrossAxisAlignment.Stretch);

    private XenoComponent NoteRow(Note note) =>
        NoteCard.View(note.Title, note.Body).Margin(Spacing.Only(bottom: 10));

    private void Add()
    {
        string text = _input.Value.Text.Trim();
        if (text.Length == 0) return;
        _notes.Insert(0, new Note(_nextId++, text, "Tap to read"));
        _input.SetText("");
        Rebuild();                                              // the list changed shape
    }
}

// ── Step 5: a second page with a typed argument ─────────────────────────────────────────
public sealed record NoteArgs(Note Note);

public sealed class NoteDetailPage : XenoPage<NoteArgs>, ITopBarPage
{
    private Note? _note;

    public override void OnNavigatedTo(NoteArgs arguments) { _note = arguments.Note; Rebuild(); }

    public string TopBarTitle => "Note";
    public TopBarOptions? TopBarOptions => new()
    {
        OnNavigation = GoBack,
        Background = 0xFFFFFFFF,
        Foreground = 0xFF111827,
        Elevation = 0
    };

    public override XenoComponent Body() =>
        Column(
            Text(_note?.Title ?? "", 24).TextColor(0xFF111827),
            Text(_note?.Body ?? "", 16).TextColor(0xFF374151),
            Button("Back", GoBack).Background(0xFF6D28D9).CornerRadius(12).Padding(16, 12)
        ).Gap(12).Padding(24).Background(0xFFF3F4F6).CrossAxis(CrossAxisAlignment.Start);
}

public sealed class NotesApp : XenoApp
{
    public override void OnStart()
    {
        Page<NotesPage>();
        Page<NoteDetailPage>();
        Navigate<NotesPage>();
    }

    internal void Open(Note note) =>
        Navigate<NoteDetailPage, NoteArgs>(new NoteArgs(note), NavTransitions.SlideHorizontal());
}
