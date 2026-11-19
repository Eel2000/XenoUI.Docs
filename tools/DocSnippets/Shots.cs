using DocSnippets.Tutorial;
using XenoUI.Core.Preview;
using XenoUI.Core.Text;

namespace DocSnippets;

/// <summary>
/// The documentation screenshots, written as ordinary [Preview] methods: the same lightweight
/// previewer that powers Visual Studio and VS Code renders them, wrapped to their content.
/// The preview name is the PNG file name.
/// </summary>
public static class Shots
{
    private static readonly TextEditController Input = UseTextEdit();

    private static readonly List<Note> Notes =
    [
        new(1, "Shopping", "Milk, eggs, bread and something for dinner tonight"),
        new(2, "Ideas", "A tiny app that renders everything with Skia"),
        new(3, "Call Amina", "About the weekend")
    ];

    [Preview("tutorial-step1")]
    private static XenoComponent S1() => Step1.Hello().Background(0xFFF3F4F6);

    [Preview("tutorial-step2")]
    private static XenoComponent S2() => Step2.Screen();

    [Preview("tutorial-step3")]
    private static XenoComponent S3() =>
        Column(Step3.Counter(UseState(3), () => { })).Padding(16).Background(0xFFF3F4F6)
            .CrossAxis(CrossAxisAlignment.Stretch);

    [Preview("tutorial-step4-empty")]
    private static XenoComponent S4Empty() =>
        Column(Step4.AddBar(Input, () => { }),
                Text("No notes yet. Write one above.", 14).TextColor(0xFF6B7280))
            .Gap(12).Padding(16).Background(0xFFF3F4F6).CrossAxis(CrossAxisAlignment.Stretch);

    [Preview("tutorial-step4", Height = 330)]
    private static XenoComponent S4() => Step4.Screen(Input, Notes);
}
