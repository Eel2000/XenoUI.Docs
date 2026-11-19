# Capacités natives (`NativeTunnel`)

[Documentation](index.md) · [Architecture d’une application](app-architecture.md) · [Texte, IME et NativeTunnel](architecture/text-input-and-native.md) · [Guide développeur](developer-guide.md)

XenoUI dessine ses propres pixels, mais certaines choses appartiennent au système : le clavier, le presse-papiers, le décodage
vidéo, la langue de l’appareil et la taille de la fenêtre. On y accède par des **capacités** : de petites interfaces typées
détenues par `NativeTunnel`. Ce guide montre comment **utiliser** celles qui existent et comment **écrire les vôtres**. (La
conception interne est dans [Texte, IME et NativeTunnel](architecture/text-input-and-native.md).)

## Utiliser une capacité

Les pages et l’application y accèdent par `Native` :

```csharp
// Obligatoire : lève une exception si la plateforme ne la fournit pas
var ui = Native.Require<IUiDispatcherCapability>();

// Facultative : null si absente (aperçus, plateforme non prise en charge)
bool reduced = Native.Get<IMotionPreferencesCapability>()?.ReducedMotion == true;
```

Utilisez `Get<T>()` si votre fonctionnalité peut se dégrader proprement, `Require<T>()` sinon. Les deux s’appellent **depuis la
couche interface** ; transmettez le résultat aux modèles et services sous forme de simples délégués
([Architecture d’une application](app-architecture.md)).

## Capacités fournies

| Interface | Fournit | Usage typique |
|---|---|---|
| `IUiDispatcherCapability` | `Post(Action)` vers le thread UI | Ramener des résultats de fond ([Effets de bord](side-effects.md)) |
| `IWindowMetricsCapability` | `Current` (`Width`, `Height`, `Density`, zones sûres) et `Changed` | Décisions adaptatives, marges de zone sûre |
| `ILayoutDirectionCapability` | `IsRightToLeft`, `Changed` | RTL ([Localisation](localization.md)) |
| `IMotionPreferencesCapability` | `ReducedMotion` | Respecter le mouvement réduit ([Animations](animations.md)) |
| `ITextInputCapability` | Sessions de saisie natives | Utilisée par `TextField` / `TextArea` |
| `IClipboardCapability` | Lire et écrire le presse-papiers | Copier / coller |
| `INavigationPlatformCapability` | Conventions de navigation de la plateforme | Gestion du retour |
| `IMediaPlaybackCapability`, `IMediaPreloadCapability`, `IMediaThumbnailCapability`, `INativeVideoOutputCapability` | Lecture audio/vidéo, préchargement, miniatures, surface vidéo native | `Native.CreatePlayer(MediaKind.Video)` ([Média](media-controller.md)) |

Exemple, réagir à la taille de la fenêtre :

```csharp
var metrics = Native.Get<IWindowMetricsCapability>();
bool wide = metrics is { Current.Width: >= 600 };
metrics!.Changed += m => { if (IsActive) Rebuild(); };
```

## Écrire sa propre capacité

On suit la recette des notes d’architecture, avec un exemple : une capacité de *retour haptique*.

**1. Définir le contrat** dans du code portable, sans type de plateforme :

```csharp
public interface IHapticsCapability : INativeCapability
{
    bool IsSupported { get; }
    void Tap();
}
```

**2. Décider si l’absence est acceptable.** Les haptiques sont facultatives : les consommateurs appellent
`Native.Get<IHapticsCapability>()`.

**3. L’implémenter par plateforme**, dans les dossiers de plateforme de votre projet (`Platforms/android`, `Platforms/ios`)
ou dans une bibliothèque :

```csharp
// Platforms/android/AndroidHaptics.cs
public sealed class AndroidHaptics(Android.Views.View view) : IHapticsCapability
{
    public bool IsSupported => true;
    public void Tap() => view.PerformHapticFeedback(Android.Views.FeedbackConstants.VirtualKey);
}
```

**4. L’enregistrer avant la première composition**, là où l’hôte crée le `NativeTunnel` :

```csharp
native.Register<IHapticsCapability>(new AndroidHaptics(hostView));
```

`Register<T>` garde une seule implémentation par interface.

**5. Définir les règles du contrat** : quel thread l’appelle (en général le thread UI), sa durée de vie, si elle exige une
**permission** (la déclarer dans le manifeste n’est pas la demander : voir [Publier](publishing.md)), et comment elle remonte
les erreurs.

**6. L’utiliser :**

```csharp
Native.Get<IHapticsCapability>()?.Tap();
```

**7. La tester** avec une fausse implémentation enregistrée dans un test (le contrat n’est qu’une interface).

### Règles de conception

- Le travail **asynchrone** prend un `CancellationToken` et retourne un résultat typé, jamais une commande opaque.
- **Aucun type de plateforme** dans l’interface (`Android.Views.View`, `UIView`) : restez portable.
- Les **événements** (`Changed`) sont levés sur le thread UI, ou documentés autrement.
- Les **permissions et interactions utilisateur** (caméra, notifications) demandent un flux de demande explicite et attendu ;
  une capacité qui « marche » sans permission échouera sur appareil.
- Préférez une interface **petite et précise** à une interface générale.
- Dites précisément ce que chaque plateforme prend en charge, comme le font les contrats du framework.

### Limites

`NativeTunnel` n’a pas encore de convention générale pour les permissions, les flux asynchrones ou le cycle de vie
activité/scène. Modélisez-les explicitement dans votre propre contrat.
