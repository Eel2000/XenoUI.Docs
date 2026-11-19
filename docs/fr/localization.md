# Localisation

[Documentation](index.md) · [Mises en page](layouts.md) · [Accessibilité](accessibility.md) · [Capacités natives](native-capabilities.md)

XenoUI ne fournit **pas** de système de ressources de chaînes : pas de chargeur `.resx`, pas de `R.string`, pas de liaison vers une
table de traductions. Ce qu’il gère, c’est la partie difficile d’un moteur de rendu maison : la **mise en forme du texte, le texte
bidirectionnel et la mise en page droite à gauche**. Cette page montre les deux : ce que le framework fait pour vous, et un motif
simple et testable pour les chaînes.

## Ce que le framework gère

| Sujet | Prise en charge |
|---|---|
| **Écritures complexes** | Le texte est mis en forme par le moteur (HarfBuzz) : accents, emoji, CJK et arabe s’affichent correctement. |
| **Texte bidirectionnel** | Les séquences sont ordonnées selon la première lettre forte ; latin dans de l’arabe et l’inverse s’affichent correctement. |
| **Mise en page RTL** | `FlowDirection(RightToLeft)` sur un sous-arbre le met en miroir ; les rangées démarrent à droite, le côté gauche du padding/marge est le côté *début*, `TextAlign(Start)` aligne à droite, le hit-testing suit. |
| **Direction de l’appareil** | Avec `FlowDirection.Inherit` (défaut), l’application se met en droite-à-gauche quand la langue de l’appareil l’est, via `ILayoutDirectionCapability`. |
| **IME et saisie** | Clavier natif et saisie de langue pour toutes les écritures. |

Utilisez `Start`/`End` et `Spacing.Directional(start, top, end, bottom)` plutôt que gauche/droite, et vérifiez chaque composant
avec `[Preview("…", RightToLeft = true)]` ([Mises en page](layouts.md)).

```csharp
// Forcer ou suivre l’appareil
FlowDirection = FlowDirection.RightToLeft;       // sur XenoApp : toute l’application
// Ou sur un sous-arbre :
Column(...).FlowDirection(FlowDirection.RightToLeft)
```

## Ce que vous fournissez : les chaînes

Gardez les chaînes hors du code d’interface, derrière une petite abstraction. Un motif minimal :

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

Utilisé dans les composants :

```csharp
Text(Strings.Get("notes.title"), 28)
Button(Strings.Get("notes.add"), Add)
Text(Strings.Get("notes.count", _notes.Count), 16)
```

Pour une application plus grande, chargez les tables depuis du JSON embarqué ou des fichiers `.resx` avec le
`ResourceManager` standard de .NET, et lisez `CultureInfo.CurrentUICulture` une fois au démarrage.

### Changer de langue à l’exécution

Les chaînes sont lues **à la composition** : un changement de langue demande une reconstruction :

```csharp
// Dans votre sous-classe de XenoApp (le SetArabicNavigation du SampleApp fait de même) :
internal void UseLanguage(string language, bool rightToLeft)
{
    Strings.Use(language);
    FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.Inherit;
    _navigation.RefreshChrome();            // reconstruit les slots barre / rail / tiroir
}

// Depuis une page :
((App)Application).UseLanguage("ar", rightToLeft: true);
Rebuild();
```

Les slots du scaffold (barre d’onglets, rail, tiroir) se reconstruisent avec `RefreshChrome()`. Un `State<string>` lié à un
`Text(...)` se met à jour sans reconstruction mais ne convient que pour quelques chaînes.

## Formater nombres, dates et pluriels

Utilisez le formatage .NET sensible à la culture ; le moteur ne fait que dessiner la chaîne obtenue :

```csharp
Text(price.ToString("C", culture))        // monnaie
Text(date.ToString("D", culture))         // date longue
```

Les pluriels varient selon la langue (« 1 note » / « 2 notes », et plus de formes en arabe ou en russe) : utilisez une
bibliothèque qui implémente les règles de pluriel CLDR, ou ajoutez une clé par forme. Ne construisez pas de phrases par
concaténation : utilisez des chaînes traduites entières avec des marqueurs (`"{0} notes"`), car l’ordre des mots change.

## Tester

- Rendez chaque langue et variante RTL en headless : `[Preview("Notes (ar)", RightToLeft = true)]`, ou un test qui appelle
  `Strings.Use("ar")` puis `PreviewRenderer.Render` ([Tester](testing.md)).
- Testez avec des chaînes **longues** (allemand, finnois) et des écritures **courtes** (CJK) : une mise en page avec `FlexGrow`
  et `MaxLines` survit aux deux.
- Testez avec de grandes polices système ([Accessibilité](accessibility.md)).

## Liste de contrôle

- [ ] Aucune chaîne visible dans un composant : tout passe par `Strings.Get`.
- [ ] `Start`/`End` et `Spacing.Directional`, jamais gauche/droite absolus.
- [ ] Les phrases sont des chaînes entières avec marqueurs.
- [ ] Formatage sensible à la culture pour nombres et dates.
- [ ] Une preview par langue et par direction.
- [ ] Mise en page vérifiée avec du texte long.
