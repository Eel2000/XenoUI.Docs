# Constrained text, typed navigation and Scaffold content

[Documentation](index.md) · [Sample API](sample-api.md) · [Navigation](navigation-scaffold.md)

## Text

`Text` wraps automatically when a finite width is not enough. Explicit line breaks are kept. Layout measures the paragraph at the available width, then the renderer paints its recording; no shaping runs on settled frames. Short ASCII texts without a particular constraint keep the optimised single-line path; Unicode texts using the default font go through the paragraph to benefit from glyph fallback. Paragraph resources belong to the entity and are released with it, including when lazy items are recycled.

```csharp
Text(article.Title, 18).MaxLines(2, ellipsis: true);
Text(article.Body, 16); // Every line, without shrinking the font.
Text("A long label", 18).TextWrap(false); // One line, clipped.
Text("A long label", 18).MaxLines(1, ellipsis: true).AutoSize(12);
```

`MaxLines(0)` removes the limit. `AutoSize` is optional and never goes below the minimum size. When the minimum is not enough, clipping or ellipsis applies; the framework does not shrink the text indefinitely. By default the font stays at its requested size. Clipping protects the text box; it does not replace measuring its height.

In a `Row`, the width of the other children, margins and gap are taken into account before re-measuring the text. Auto-width children can shrink; an explicit width stays fixed by default. `FlexShrink(0)` disables this shrinking, a positive factor allows it. `FlexGrow(1)` is still the way to give the text the remaining space. Scrollable horizontal lists are not shrunk to their viewport.

Size, text, width and options invalidate layout on the cold path. Following frames read the cached resources. Shaping/composition allocations are allowed; the zero-allocation requirement applies to settled frames, not to a change of text or viewport.

## Typed navigation parameters

```csharp
public sealed record ArticleDetailsArgs(Article Article);

public sealed class ArticleDetailsPage : XenoPage<ArticleDetailsArgs>
{
    public override void OnNavigatedTo(ArticleDetailsArgs arguments)
    {
        // Prepare the model on the cold path, before Body().
    }

    public override XenoComponent Body() => Text(Arguments.Article.Title);
}

// Page<T>() is unchanged and creates a persistent instance.
Navigate<ArticleDetailsPage, ArticleDetailsArgs>(
    new(article), NavTransitions.SlideHorizontal(280));
```

The page type and the parameter type are checked at compile time. Each stack entry keeps its payload; Back and restoring a destination apply the parameters of the entry they return to. `OnNavigatedTo` is a preparation callback, not a per-frame callback nor a promise that the transition will commit. A persistent page can receive several payloads over its lifetime. Two entries of the same page remain two distinct entries, with activation/deactivation on commit.

Prefer immutable records: the framework keeps the payload, it does not deep-copy a mutable object. Persistent controllers/models remain the responsibility of the page or the application; this API does not clone their whole state for each entry. The `Navigate<T>()`, `GoBack()` APIs and the existing transitions remain available.

SampleApp uses this contract to pass the article to the detail page; comments are still loaded by the portable .NET service, not by the router.

## Space reserved for the BottomBar

The Scaffold measures the standard or custom bar, margins included, then reduces the height available to pages. Scrollable and lazy containers use this height, so the last element can be reached without the bar hiding it. Opening the drawer keeps this space; the drawer stays an overlay. When the IME hides the bar, its reservation is removed and the keyboard height is not added twice.

For content deliberately behind the bar:

```csharp
new NavigationScaffoldState(/* destinations */)
{
    ContentOverlaysNavigationBar = true
};
```

In this mode, the developer takes care of the padding and accessibility of the content under the bar. The FAB stays an independent overlay.

## Automated validation

```powershell
dotnet run --project tests/XenoUI.Core.RegressionTests -c Release
dotnet run --project tests/SampleApp.Api.RegressionTests -c Release
```

The targeted regressions cover wrap, newline, the real width of Rows, line limits, minimum size, reflow after resize, zero allocation on settled multi-line frames, the height of a custom bar with margins, opening the drawer and restoring payloads on a persistent page.
