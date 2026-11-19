using DocSnippets;
using XenoUI.Core.Preview;

// Renders the documentation screenshots with the headless previewer.
// Usage: dotnet run -- <output directory>   (default: ../../docs/assets/img)
string output = args.Length > 0 ? args[0] : Path.Combine("..", "..", "docs", "assets", "img");
Directory.CreateDirectory(output);

int failures = 0;
foreach (var entry in PreviewRenderer.Discover(typeof(Shots)).Concat(PreviewRenderer.Discover(typeof(CatalogShots))))
{
    var image = PreviewRenderer.Render(entry, scale: 2f);
    if (!image.Succeeded)
    {
        Console.Error.WriteLine($"{entry.DisplayName}: {image.Error?.Message} {image.Hint}");
        failures++;
        continue;
    }
    File.WriteAllBytes(Path.Combine(output, entry.DisplayName + ".png"), image.Png!);
    Console.WriteLine($"{entry.DisplayName}.png  {image.PixelWidth}x{image.PixelHeight}");
}
return failures;
