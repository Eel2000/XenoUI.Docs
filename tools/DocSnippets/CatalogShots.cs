using SkiaSharp;
using XenoUI.Core.Preview;
using XenoUI.Core.Text;

namespace DocSnippets;

/// <summary>Screenshots of the component catalogue page ([Preview] name = PNG file name).</summary>
public static class CatalogShots
{
    private const uint Page = 0xFFF3F4F6;
    private static readonly TextEditController Field = UseTextEdit();

    [Preview("catalog-text")]
    private static XenoComponent TextSample() =>
        Column(
            Text("Display / 28", 28).TextColor(0xFF111827),
            Text("Section title / 20", 20).TextColor(0xFF111827),
            Text("Body copy wraps at the available width and stays readable on any screen size.", 14)
                .TextColor(0xFF4B5563),
            Text("Truncated after one line with an ellipsis when it does not fit the row", 14)
                .TextColor(0xFF4B5563).MaxLines(1, ellipsis: true),
            Text("CAPTION", 10).TextColor(0xFF6D28D9)
        ).Gap(8).Padding(16).Background(Page).CrossAxis(CrossAxisAlignment.Stretch);

    [Preview("catalog-button")]
    private static XenoComponent ButtonSample() =>
        Column(
            Button("Default", () => { }),
            Button("Customised", () => { }, 16).Background(0xFF16A34A).CornerRadius(20).Padding(28, 14),
            TextButton("Text button", () => { }, 0xFF6D28D9)
        ).Gap(12).Padding(16).Background(Page).CrossAxis(CrossAxisAlignment.Start);

    [Preview("catalog-layout")]
    private static XenoComponent LayoutSample() =>
        Column(
            Row(Box(40, 40).Background(0xFF6D28D9).CornerRadius(10),
                Box(60, 40).Background(0xFF0EA5E9).CornerRadius(10),
                Spacer(),
                Box(30, 40).Background(0xFF16A34A).CornerRadius(10)).Gap(8),
            Row(Text("start", 11).TextColor(0xFF6B7280), Spacer(),
                Text("end", 11).TextColor(0xFF6B7280))
        ).Gap(8).Padding(16).Background(Page).CrossAxis(CrossAxisAlignment.Stretch);

    [Preview("catalog-zstack")]
    private static XenoComponent StackSample() =>
        Column(
            ZStack(
                Box(120, 120).Background(0xFF6D28D9).CornerRadius(24),
                Row(Spacer(),
                    Text("3", 11).TextColor(0xFFFFFFFF).Background(0xFFDC2626).CornerRadius(9).Padding(6, 3))
                    .AlignSelf(CrossAxisAlignment.Stretch).Padding(8)
            ).Size(120, 120)
        ).Padding(16).Background(Page);

    [Preview("catalog-elevation")]
    private static XenoComponent ElevationSample() =>
        Row(Card("0 dp", 0), Card("4 dp", 4), Card("12 dp", 12))
            .Gap(12).Padding(20).Background(Page);

    private static XenoComponent Card(string label, float elevation) =>
        Text(label, 12).TextColor(0xFF111827).Padding(18, 22)
            .Background(0xFFFFFFFF).CornerRadius(14).Elevation(elevation);

    [Preview("catalog-shape")]
    private static XenoComponent ShapeSample() =>
        Row(
            Text("TAG", 13).TextColor(0xFFFFFFFF).Padding(20, 12).Background(0xFFDC2626).Shape(Tag),
            Text("ROUND", 13).TextColor(0xFFFFFFFF).Padding(20, 12).Background(0xFF0EA5E9).CornerRadius(18)
        ).Gap(12).Padding(16).Background(Page).CrossAxis(CrossAxisAlignment.Center);

    private static readonly XenoShape Tag = XenoShape.Create((path, w, h) =>
    {
        float cut = Math.Min(14, Math.Min(w, h) / 2);
        path.MoveTo(0, 0);
        path.LineTo(w - cut, 0);
        path.LineTo(w, cut);
        path.LineTo(w, h);
        path.LineTo(0, h);
        path.Close();
    });

    [Preview("catalog-progress")]
    private static XenoComponent ProgressSample() =>
        Column(LinearProgress(UseState(0.6f), 6, 0xFFE5E7EB, 0xFF6D28D9).CornerRadius(3))
            .Padding(20).Background(Page).CrossAxis(CrossAxisAlignment.Stretch);

    [Preview("catalog-textfield")]
    private static XenoComponent TextFieldSample() =>
        Column(TextField(Field, "Your name").Height(52)).Padding(16).Background(Page)
            .CrossAxis(CrossAxisAlignment.Stretch);

    [Preview("catalog-snackbar")]
    private static XenoComponent SnackbarSample() =>
        Column(Snackbar.Body("Message deleted", new SnackbarOptions { ActionLabel = "Undo" }, () => { }))
            .Padding(16).Background(Page).CrossAxis(CrossAxisAlignment.Stretch);

    [Preview("catalog-alert")]
    private static XenoComponent AlertSample() =>
        Column(Alert.Content("Delete note?", "This cannot be undone.", new AlertOptions(), () => { })
                .Padding(24).Background(0xFFFFFFFF).CornerRadius(24))
            .Padding(16).Background(0xFFB0B3BA).CrossAxis(CrossAxisAlignment.Stretch);
}
