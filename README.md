# XenoUI documentation

Source of the [XenoUI](https://github.com/Eel2000/XenoUI) documentation site, published at
**https://eel2000.github.io/XenoUI.Docs/** (English) and **/fr/** (French).

The site is built with [MkDocs Material](https://squidfunk.github.io/mkdocs-material/) and
[mkdocs-static-i18n](https://github.com/ultrabug/mkdocs-static-i18n).

## Layout

```text
docs/
├─ assets/        logo and shared files
├─ en/            English pages (default language, served at /)
└─ fr/            French pages (served at /fr/), same file names as en/
mkdocs.yml        site, theme, languages and navigation
```

A page exists under the same path in both languages. When you change one, update the other;
a page missing in `fr/` falls back to the English one.

## Preview locally

```bash
python -m venv .venv
.venv/Scripts/activate        # Windows; use `source .venv/bin/activate` elsewhere
pip install -r requirements.txt
mkdocs serve
```

Then open http://127.0.0.1:8000/. `mkdocs build --strict` runs the same checks as CI
(broken links and anchors fail the build).

## Publishing

Every push to `main` builds the site and deploys it to GitHub Pages
(`.github/workflows/deploy.yml`). Pull requests are built and checked, not deployed.
In the repository settings, *Pages › Build and deployment › Source* must be **GitHub Actions**.
