# Documentation tools

Two small .NET projects keep the documentation honest. Neither runs in the site build (CI only runs `mkdocs`); run them
locally when the XenoUI source changes and commit what they produce.

They need a checkout of the **XenoUI** repository next to this one (`../XenoUI`), or `-p:XenoSourceRoot=<path>/` for
`DocSnippets`, and the .NET 11 SDK.

## `DocSnippets` — compiled samples and screenshots

Contains every code sample of the [tutorial](../docs/en/tutorial-first-app.md) as real code (`Tutorial/Steps.cs`), compiled
against the XenoUI sources, so a documentation sample cannot silently stop compiling. `Shots.cs` and `CatalogShots.cs` are
ordinary `[Preview]` methods; the headless previewer renders them into `docs/assets/img/`.

```powershell
cd tools/DocSnippets
dotnet build                                  # fails if a sample no longer compiles
dotnet run --no-build -- ../../docs/assets/img   # regenerates the screenshots
```

To add a screenshot, add a `[Preview("my-image-name")]` method; the preview name is the PNG file name. Reference it as
`![alt](../assets/img/my-image-name.png)`.

## `ApiDocGen` — the API reference

Generates `docs/en/reference/*.md` from the compiled XenoUI assemblies and their XML documentation comments (reflection plus
the `.xml` files). Engine internals (`Doddb`, `Memory`, `Pillars`, `Components.Primary`) are skipped.

```powershell
# 1. Build the libraries with XML documentation into a scratch folder
$out = "$env:TEMP/xeno-api"
foreach ($p in "XenoUI.Core","XenoUI.StandardLibrary","XenoUI.Media") {
  dotnet build ../XenoUI/$p/$p.csproj -c Release -p:GenerateDocumentationFile=true -o $out
}
# 2. Generate (the last argument gives the generator the SkiaSharp dependencies, here from DocSnippets' output)
cd tools/ApiDocGen
dotnet run -- $out ../../docs/en/reference ../DocSnippets/bin/Debug/net11.0
```

Run it twice: once for English and once with `APIDOC_LANG=fr` into `docs/fr/reference` (French headings and table
headers; the summaries come from the English source comments and stay in English). The nav entries in `mkdocs.yml` list the generated
files: add a line when a new namespace appears.

Improve the reference by writing better `/// <summary>` comments in the XenoUI source, then regenerate.

## CI

The workflow `.github/workflows/snippets.yml` runs `DocSnippets` when `tools/DocSnippets` or the tutorial change (or by hand, with a
chosen XenoUI ref). The XenoUI repository is private, so add a fine-grained, read-only personal access token for it as the
repository secret `XENOUI_READ_TOKEN`; without it the job reports a notice and skips the check.
