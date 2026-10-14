# Guide développeur XenoUI

[Index docs](index.md) · [Installation](installation.md) · [Dépannage](troubleshooting.md) · [Architecture](architecture/index.md)

Ce guide décrit l’usage public de XenoUI pour construire une application. Les projets utilisent [`Xeno.SDK`](https://www.nuget.org/packages/Xeno.SDK), un SDK MSBuild publié sur nuget.org en preview (`{{ xeno_version }}`). Pour installer templates, outil `xeno` et previewer, suivez le [guide d’installation](installation.md) ; le [guide SDK](xeno-sdk.md) détaille la configuration du build.

## 1. Périmètre et maturité

XenoUI fournit aujourd’hui :

- un runtime déclaratif C# et un moteur de layout flex-like interne ;
- un rendu SkiaSharp commun à Android et iOS ;
- une navigation par pile, avec bouton Retour natif Android ;
- du scrolling, des images, polices, icônes, boutons et effets ;
- un éditeur de texte dessiné par XenoUI, connecté à l’IME natif ;
- l’outil `xeno` de découverte et de lancement sur appareils Android.

Android est le parcours de référence validé. iOS nécessite macOS/Xcode et n’est pas encore validé de bout en bout. `LazyColumn<T>` et `LazyRow<T>` ne construisent que le viewport et son overscan.

## 2. Prérequis

### Commun

- SDK .NET 10 (`dotnet --version`) ;
- workload correspondant à la cible ;
- un IDE facultatif : Visual Studio ou VS Code avec C# Dev Kit.

```powershell
dotnet workload install android
dotnet workload install ios
dotnet workload list
```

N’installez `ios` que sur une machine capable de construire iOS. La compilation et l’exécution iOS exigent macOS, Xcode, les simulateurs/runtime compatibles et, pour un appareil physique, la configuration Apple de signature.

### Android

- Android SDK, platform-tools et au moins une plateforme Android ;
- `adb` accessible par `ANDROID_SDK_ROOT`, `ANDROID_HOME`, l’emplacement SDK utilisateur standard ou le `PATH` ;
- un émulateur démarré ou un téléphone avec Options développeur et Débogage USB.

Vérification (après [installation de `xeno`](installation.md#5-installer-loutil-xeno)) :

```powershell
adb devices -l
xeno devices
```

Un téléphone `unauthorized` doit être déverrouillé ; acceptez ensuite l’empreinte RSA « Autoriser le débogage USB ».

## 3. Installer les outils

```powershell
dotnet new install XenoUI.Templates::{{ xeno_version }}
dotnet tool install --global XenoUI.Tooling --version {{ xeno_version }}
```

Détails, mise à jour et previewer Visual Studio : [Installation](installation.md).

## 4. Créer une application

```powershell
dotnet new xenoui -n MyApp
cd MyApp
```

Les commandes suivantes s’exécutent depuis le dossier `MyApp/`.

Le projet généré utilise le SDK MSBuild `Xeno.SDK`, restauré depuis nuget.org. Voir [Xeno.SDK](xeno-sdk.md) pour configurer un projet existant et centraliser la version dans `global.json`.

```xml
<Project Sdk="Xeno.SDK/{{ xeno_version }}">

  <PropertyGroup>
    <TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <XenoAppId>com.example.myapp</XenoAppId>
    <XenoAppName>My Xeno App</XenoAppName>
    <ApplicationVersion>1</ApplicationVersion>
    <ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
  </PropertyGroup>

</Project>
```

Valeurs par défaut surchargeables :

| Propriété | Défaut | Rôle |
|---|---:|---|
| `XenoAppId` | `com.xenoui.unnamed` | Identifiant d’application, utilisé comme `ApplicationId` |
| `XenoAppName` | nom du projet | Nom affiché |
| `XenoMinAndroidVersion` | `24` | Version Android minimale |
| `XenoMinIosVersion` | `13.0` | Version iOS minimale |

Le SDK injecte les références XenoUI, les `using` globaux, les points d’entrée natifs, les assets et les propriétés de plateforme. Il n’est normalement pas nécessaire de créer `MainActivity.cs` ou `Main.cs`.

### Structure conseillée

```text
MyApp/
├── MyApp.csproj
├── App.cs
├── Pages/
│   └── HomePage.cs
└── Assets/
    ├── Images/
    └── Fonts/
```

### Application et première page

```csharp
public sealed class App : XenoApp
{
    public override void OnStart()
    {
        Page<HomePage>();
        Page<DetailsPage>();
        Navigate<HomePage>();
    }
}

public sealed class HomePage : XenoPage
{
    public override XenoComponent Body() =>
        Column(
            Text("Bonjour XenoUI", 28),
            Button("Ouvrir", () => Navigate<DetailsPage>())
        ).Padding(24).Gap(16);
}
```

`OnStart()` doit naviguer vers une page initiale. Une page non enregistrée dans `OnStart()` provoque une `InvalidOperationException` lors de `Navigate<TPage>()`.

## 5. Construire et lancer

### Android

Lister les cibles :

```powershell
xeno devices
xeno devices --json
xeno devices --watch
```

Construire puis installer/lancer :

```powershell
xeno run `
  --project MyApp.csproj `
  --device <device-id> `
  --framework net10.0-android `
  --configuration Debug
```

Le CLI choisit le RID depuis l’ABI de la cible (`android-arm64`, `android-x64`, etc.), construit l’APK signé de debug, exécute `adb install -r -t`, puis lance l’activité. `--no-build` réutilise un APK déjà produit pour le même RID.

### iOS

Sur macOS :

```bash
dotnet restore MyApp.csproj -p:TargetFramework=net10.0-ios
dotnet build MyApp.csproj -f net10.0-ios
```

Le CLI `xeno` n’a pas encore de provider iOS. Utilisez les outils .NET/iOS et Xcode pour sélectionner, signer et lancer la cible. Ne considérez pas une compilation iOS depuis Windows comme une validation runtime.

## 6. Modèle de composition

Une page retourne un `XenoComponent`. Les factories créent des entités dans le moteur ; les modificateurs ajustent leurs composants de style, rendu ou interaction.

### Primitives

| Factory | Usage |
|---|---|
| `Column(children)` | Empilement vertical |
| `Row(children)` | Empilement horizontal |
| `Box(width, height)` | Rectangle ou conteneur |
| `ZStack(children)` | Superposition selon l’ordre des enfants |
| `Text(value, size)` | Texte statique ou réactif |
| `Spacer()` | Espace flexible (`FlexGrow(1)`) |
| `Image(asset)` | Image embarquée |
| `Icon(icon, size)` | Glyphe de police |
| `LazyColumn(items, item => ...)` | Liste verticale virtualisée et mesurée automatiquement |
| `LazyRow(items, item => ...)` | Liste horizontale virtualisée et mesurée automatiquement |
| `TextField(...)` | Champ mono-ligne custom-painted |
| `TextArea(...)` | Champ multi-ligne custom-painted |

### Layout et centrage

```csharp
Row(
    Box(48, 48).Background(0xFF6D28D9).CornerRadius(12),
    Column(
        Text("Titre", 18),
        Text("Sous-titre", 13).TextColor(0xFF6B7280)
    ),
    Spacer(),
    Text("42")
)
.Gap(12)
.Padding(16)
.CrossAxis(CrossAxisAlignment.Center)
.Background(0xFFFFFFFF);
```

Modificateurs principaux :

- taille/espace : `Width`, `Height`, `Size`, `Padding`, `Margin`, `Gap`, `FlexGrow` ;
- alignement : `MainAxis`, `CrossAxis`, `AlignSelf`, `CenterContent` ;
- visuel : `Background`, `CornerRadius`, `Elevation`, `Shadow` ;
- texte : `TextColor`, `FontSize`, `FontFamily`, `TextAlign` ;
- interaction : `Clickable`, `Scrollable` ;
- image : `ScaleMode`.

Les couleurs sont ARGB : `0xFF6D28D9` est opaque ; `0x336D28D9` est translucide. Les dimensions sont logiques, pas des pixels physiques.

### Boutons et effets

```csharp
Button("Enregistrer", Save, fontSize: 16, textColor: 0xFFFFFFFF)
    .Background(0xFF2563EB)
    .CornerRadius(16)
    .Padding(20, 14)
    .Elevation(4)
    .FlexGrow(1);
```

Le bouton standard fournit un feedback pressé par assombrissement. Il retourne un composant normal : tous les modificateurs restent disponibles.

## 7. État, mises à jour et reconstruction

### Mise à jour fine du texte

```csharp
private readonly State<int> _count = UseState(0);

public override XenoComponent Body() =>
    Column(
        Text($"Total : {_count}"),
        Text(_count, 28),
        Button("+1", () => _count.Value++)
    );
```

`Text(State<T>)` et les chaînes interpolées réactives s’abonnent à l’état. Une nouvelle valeur marque seulement le texte concerné pour mesure/rendu ; le framework ne reconstruit pas automatiquement tout l’arbre.

### Changement structurel

Pour ajouter/retirer des composants ou modifier une branche conditionnelle, conservez l’état dans un champ de page et appelez `Rebuild()` :

```csharp
private bool _expanded;

public override XenoComponent Body() =>
    Column(
        Button(_expanded ? "Réduire" : "Développer", () =>
        {
            _expanded = !_expanded;
            Rebuild();
        }),
        _expanded ? Text("Détails") : XenoComponent.None
    );
```

Une reconstruction remet à zéro l’arbre ECS transitoire et rappelle `Body()` sous verrou. Elle ne diff pas l’ancien et le nouvel arbre. Les objets persistants (`State`, `ScrollState`, `TextEditController`) doivent être des champs de page, jamais des variables recréées dans `Body()`.

### Déclarer sans `new`

Le DSL ne demande jamais `new` : chaque objet persistant ou option a sa fabrique, importée par `using static` avec le SDK.

| Besoin | Fabrique | Type créé |
|---|---|---|
| État réactif | `UseState(valeur)`, `UseStates(n, valeur)` | `State<T>`, `State<T>[]` |
| Scroll | `UseScroll()` | `ScrollState` |
| Liste lazy / pager | `UseLazyList()` | `LazyListState` |
| Champ de texte | `UseTextEdit("initial")` | `TextEditController` |
| Clavier/IME | `InputOptions(...)` | `TextInputConfiguration` |
| Sélection | `Selection(a, b)`, `Caret(i)` | `TextSelection` |
| Icône | `Glyph(glyphe, police)` | `IconDef` |
| Action de barre | `NavigationAction.For(icône, tap) with { ... }` | `NavigationAction` |
| Style de barre | `DockedNavigationBarStyle.Default with { ... }` | `DockedNavigationBarStyle` |
| Lecteur média | `Native.CreatePlayer(MediaKind.Video)`, `Configure(loop: true)` | `MediaController` |

Une fabrique `Use…` crée un objet neuf à chaque appel : appelez-la dans l’initialiseur d’un champ, jamais dans `Body()`. Seul `NavigationScaffoldState`, configuré une fois dans l’`App`, garde un initialiseur d’objet.

## 8. Navigation et Retour natif

Les pages sont instanciées une seule fois lors de `Page<T>()`, puis réutilisées. Leurs champs conservent donc leur valeur entre les navigations.

```csharp
public override void OnStart()
{
    Page<HomePage>();
    Page<SettingsPage>();
    Navigate<HomePage>();
}
```

Dans une page :

```csharp
Button("Réglages", () => Navigate<SettingsPage>());
Button("Retour", GoBack);
```

Sur Android 7–12, l’hôte intercepte `OnBackPressed`. À partir d’Android 13, il enregistre un callback `OnBackInvokedDispatcher` seulement lorsque `CanGoBack` est vrai. `TryGoBack()` dépile une page ; à la racine, l’OS reprend son comportement normal.

Sur iOS, utilisez pour l’instant une action visible `GoBack()`. L’intégration du geste interactif natif n’est pas encore équivalente.

`Navigate<TPage>(transition, popTransition)` définit séparément l’aller et le retour, par exemple `Navigate<DetailsPage>(NavTransitions.SlideVertical(320), NavTransitions.Fade(220))` ; `NavigationOptions` reste accepté. Pour une navigation principale adaptative avec une pile par destination, BottomBar, Rail, Drawer permanent et FAB, consultez [Navigation et NavigationScaffold](navigation-scaffold.md).

## 9. Scrolling et listes

### Conteneur scrollable

```csharp
private readonly ScrollState _scroll = UseScroll();

public override XenoComponent Body() =>
    Column(
        Text("Section 1"),
        Text("Section 2"),
        Text("Section 3")
    )
    .CrossAxis(CrossAxisAlignment.Stretch)
    .Scrollable(_scroll);
```

Le moteur borne l’offset au contenu, arrête le mouvement lorsque le contenu tient dans le viewport, applique inertie/rebond et clippe rendu et hit-testing. Si le contenu rétrécit, l’offset est re-clampé.

### Liste

```csharp
private readonly LazyListState _peopleState = UseLazyList(); // optionnel

public override XenoComponent Body() =>
    LazyColumn(_people, person =>
        Row(
            Text(person.Name),
            Spacer(),
            Text(person.Status)
        ).Padding(16),
        key: person => person.Id,
        state: _peopleState
    ).FlexGrow(1);
```

Il suffit de fournir les items. Le builder est optionnel (`ToString()` est alors affiché), tout comme `key` et `state`. Le moteur construit une petite fenêtre autour du viewport, mesure chaque item rencontré et affine son index d’étendue. Les items peuvent donc avoir des hauteurs ou largeurs différentes sans valeur par défaut imposée par l’API.

Utilisez une clé stable lorsque la collection peut être réordonnée ou remplacée : les mesures connues suivent alors l’identité logique de l’item. Conservez explicitement un `LazyListState` seulement pour observer le scroll ou partager son identité hors de la composition ; sinon XenoUI en retient un automatiquement par point d’appel, sans fuite d’offset entre deux pages ou deux listes. Les anciennes surcharges `LazyColumn(ScrollState, count, estimatedHeight, builder)` et `LazyRow(...)` restent compatibles, mais matérialisent tous les items et sont réservées à la migration.

## 10. Assets, images, icônes et polices

Tout fichier sous `Assets/**/*` devient une ressource embarquée avec le préfixe logique `XenoAsset/`.

```text
Assets/
├── Images/avatar.png
└── Fonts/Inter-Regular.ttf
```

```csharp
Image("avatar")
    .Size(96, 96)
    .ScaleMode(ImageScaleMode.Cover)
    .CornerRadius(48);

Text("Bonjour", 24).FontFamily("Inter-Regular");
```

Modes image : `Cover`, `Fit`, `Fill`, `None`. L’image utilise sa taille naturelle si aucune taille n’est imposée. Les fichiers `.ttf` et `.otf` sont chargés et mis en cache par le runtime.

Le nom court sans extension est pratique, mais deux assets de même nom peuvent entrer en collision ; le premier trouvé gagne. Utilisez un chemin relatif explicite si nécessaire.

`Icon(IconDef)` utilise les définitions de la bibliothèque standard. `Icon(glyph, fontName, size)` accepte aussi une police d’icônes embarquée.

## 11. Saisie de texte et IME

XenoUI suit une architecture hybride proche de Flutter : le système d’exploitation reste propriétaire de la session d’entrée (IME, langue, composition, dictée, autocorrection, emoji), tandis que XenoUI dessine le texte, le curseur, la sélection et le fond. Le contrôle natif est caché et synchronise un snapshot complet.

### Champ mono-ligne

```csharp
private readonly TextEditController _email = UseTextEdit();

TextField(
    _email,
    "nom@example.com",
    InputOptions(
        NativeKeyboardType.Email,
        NativeInputAction.Next,
        autoCorrect: false,
        autofill: NativeAutofillHint.Email,
        label: "Adresse e-mail"),
    onAction: action => HandleAction(action)
).Height(54);
```

### Champ multi-ligne

```csharp
private readonly TextEditController _note = UseTextEdit("Texte initial");

TextArea(
    _note,
    "Écrire une note…",
    InputOptions(
        NativeKeyboardType.Multiline,
        NativeInputAction.None,
        label: "Note")
).Height(160);
```

### Mot de passe personnalisable

```csharp
private readonly TextEditController _password = UseTextEdit();
private bool _reveal;

Column(
    TextField(
        _password,
        "Mot de passe",
        InputOptions(
            NativeKeyboardType.Password,
            secure: true,
            autofill: NativeAutofillHint.Password),
        obscureText: !_reveal),
    Button(_reveal ? "Masquer" : "Afficher", () =>
    {
        _reveal = !_reveal;
        Rebuild();
    })
);
```

`secure` configure la sémantique native ; `obscureText` décide séparément du rendu masqué. Cette séparation permet un bouton afficher/masquer sans abandonner l’IME natif.

### Contrôleur et édition

`TextEditController.Value` contient texte, sélection et plage de composition. Les index sont des offsets UTF-16. API utiles :

- `SetText` (curseur en fin), `SelectAll`, `SetValue`, `SetSelection`, `ReplaceSelection` ;
- `CommitText`, `SetComposingText`, `FinishComposition` ;
- `DeleteBackward`, `DeleteSurroundingText`, `DeleteSurroundingCodePoints` ;
- `GetTextBeforeSelection`, `GetTextAfterSelection`, `GetSelectedText` ;
- `Undo`, `Redo`, `CanUndo`, `CanRedo`, `BeginBatchEdit` ;
- événement `Changed` avec origine et version.

```csharp
_note.Changed += (_, change) =>
    Console.WriteLine($"v{change.Version}: {change.Value.Text}");

_note.Undo();
_note.Redo();
_note.SelectAll();
_note.SetSelection(Caret(0));
```

Le moteur gère la composition, les paires de substitution, la suppression par points de code, les emoji/grappes, la sélection de mot, les poignées, la barre contextuelle et le scrolling interne multi-ligne. Les champs sécurisés n’exposent pas Copier/Couper.

### Limites texte

- style interne encore peu paramétrable : taille, padding et couleurs de base sont partiellement fixés ;
- arbre d’accessibilité global incomplet malgré l’éditeur natif caché ;
- matériel clavier, dictée et toutes les langues doivent encore être couverts par une matrice de tests appareils ;
- la validité de la géométrie curseur/sélection dépend du snapshot atomique layout-paragraphe ; ne contournez pas le contrôleur.

## 12. NativeTunnel

`XenoApp.Native` expose un registre typé de capacités fournies par l’hôte (`Get<T>()` pour une capacité optionnelle, `Require<T>()` pour un besoin obligatoire).

Le registre actuel contient principalement `ITextInputCapability` et `IClipboardCapability`, consommés par le runtime. Attention : `XenoPage.App` est encore interne ; une page applicative ne possède donc pas aujourd’hui d’accès public propre au tunnel. L’exposition/injection de capacités aux pages fait partie du contrat public à finaliser avant V1. Ne contournez pas cette limite par un singleton plateforme.

Pour une nouvelle capacité (batterie, notification, audio, appel, réseau spécialisé), définissez un contrat `INativeCapability` sans type plateforme, implémentez-le dans chaque hôte, puis enregistrez-le avant le premier build. N’introduisez pas de types Android/UIKit dans le code partagé. Le contrat détaillé (threading, durée de vie, annulation, permissions) est décrit dans [Texte, IME et NativeTunnel](architecture/text-input-and-native.md#9-nativetunnel).

## 13. Visual Studio

1. Ouvrez la solution de votre application.
2. Laissez la restauration NuGet se terminer.
3. Définissez le projet XenoUI comme projet de démarrage.
4. Sélectionnez le TFM Android et une cible Android standard de Visual Studio.
5. Construisez/déployez avec F5.

Le SDK expose les capacités `XenoUI`, `Android` et `AndroidApplication` nécessaires à l’évaluation de projet. Le previewer **XenoUI Preview** est décrit dans [Visual Studio](visual-studio.md).

En cas d’absence de cible, validez d’abord `adb devices -l`, puis `xeno devices`. Si le CLI voit le téléphone mais pas Visual Studio, rechargez la solution après avoir choisi le TFM Android et vérifiez l’installation du workload Android de Visual Studio.

## 14. VS Code

Avec C# Dev Kit, construisez et lancez depuis le terminal intégré avec `xeno run` (§ 5). L’extension **XenoUI** pour VS Code (panneau XenoUI Preview, création de projets/pages/composants, sélection d’appareil et lancement) est publiée en pré-version sur le Marketplace VS Code : voir [VS Code](vscode.md). Sans l’extension, le terminal intégré et `xeno run` suffisent.

## 15. Debugging

### Build détaillé

```powershell
dotnet build MyApp.csproj `
  -f net10.0-android -v:diagnostic
```

### Logs Android

```powershell
adb logcat
adb shell pidof com.example.myapp
adb logcat --pid=<pid>
```

### Inspection MSBuild

```powershell
dotnet msbuild MyApp.csproj `
  -p:TargetFramework=net10.0-android `
  -getProperty:ApplicationId
```

Un écran vide ou un crash de navigation doit être recherché dans l’ordre suivant : exception de `Body()`, page non enregistrée, objet persistant créé dans `Body()`, accès à une entité d’un arbre précédent, puis erreur layout/render. Voir le [dépannage](troubleshooting.md).

## 16. Valider son application

Validez sur appareil ou émulateur les parcours concernés par vos changements :

- démarrage et retour racine ;
- navigation répétée avant/arrière ;
- scrolling jusqu’aux deux bornes, contenu court et contenu qui rétrécit ;
- rotation/redimensionnement si concerné ;
- saisie accents/emoji/CJK/RTL, composition, sélection, undo/redo ;
- clavier affiché/caché et changement de champ ;
- reconstruction pendant une session texte.

Validez iOS sur macOS avant d’annoncer la compatibilité iOS de votre application.

## 17. Versionnage et compatibilité

XenoUI est publié en preview sur nuget.org (`{{ xeno_version }}`) ; aucune version stable n’existe encore. Jusqu’à V1 :

- épinglez la version de `Xeno.SDK` dans `global.json` et mettez-la à jour volontairement ;
- ne comptez pas sur une compatibilité source ou binaire entre deux previews ;
- tous les packages XenoUI d’une application doivent porter la même version, ce que `Xeno.SDK` garantit ;
- validez Android sur au moins une cible arm64 réelle avant une release de votre application.

Une V1 apportera une API publique relue, une politique de support OS/.NET et SemVer. La trajectoire est décrite dans [Décisions et trajectoire V1](architecture/decisions-v1.md).

## 18. Contribuer au framework

1. Lisez le [module d’architecture](architecture/index.md).
2. Identifiez la frontière correcte : Core, StandardLibrary, hôte plateforme, BuildTasks, Tooling ou IDE.
3. Ajoutez d’abord une régression reproductible.
4. Gardez les types natifs hors de `XenoUI.Core`.
5. Ne stockez jamais une référence vers une entrée de slab au-delà d’une mutation/reset.
6. Assurez l’atomicité snapshot/layout/render et ignorez les callbacks d’une ancienne version d’arbre.
7. Mettez à jour la documentation canonique et la section de dette si le contrat évolue.

Les changements à risque élevé sont : allocation/libération native, reconstruction, texte/IME, calcul des offsets UTF-16, scrolling animé, cycle de vie hôte et génération MSBuild.

## 19. Références suivantes

- [Dépannage par symptôme](troubleshooting.md)
- [Architecture du runtime](architecture/runtime.md)
- [Architecture texte et NativeTunnel](architecture/text-input-and-native.md)
- [Architecture SDK, plateformes et tooling](architecture/tooling-and-platforms.md)
- [Décisions et trajectoire V1](architecture/decisions-v1.md)
