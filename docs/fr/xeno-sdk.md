# Xeno.SDK — créer et configurer une application

[Documentation](index.md) · [Installation](installation.md) · [Guide développeur](developer-guide.md) · [Architecture du tooling](architecture/tooling-and-platforms.md)

[`Xeno.SDK`](https://www.nuget.org/packages/Xeno.SDK) est un SDK MSBuild distribué sur nuget.org. Il importe `Microsoft.NET.Sdk`, référence les packages runtime correspondant à la plateforme, fournit les points d'entrée natifs et embarque les assets.

## Statut et prérequis

Version actuelle : `{{ xeno_version }}`, publiée sur nuget.org. Il faut .NET 10 et le workload de la plateforme ciblée. Le SDK ne télécharge pas silencieusement de workload, Android SDK ou Xcode. Un build/signature/exécution d'application iOS exige toujours macOS et Xcode ; évaluer un projet iOS sur Windows n'est pas une validation de l'application iOS.

Les packages publiés partagent tous la même version :

| Package | Contenu |
|---|---|
| `Xeno.SDK` | SDK MSBuild : propriétés, targets, points d'entrée, tâche de build |
| `XenoUI.Core`, `XenoUI.StandardLibrary` | Runtime portable |
| `XenoUI.Platforms` | Hôtes natifs Android et iOS dans un seul package multi-cible (`lib/net10.0-android`, `lib/net10.0-ios`) ; NuGet choisit celui du TFM de l'application |
| `XenoUI.Media`, `XenoUI.Media.Android` | Module média optionnel (`XenoEnableMedia`) |
| `XenoUI.Templates` | Templates `dotnet new` / Visual Studio |
| `XenoUI.Tooling` | Outil .NET `xeno` |

Une version publiée est immuable : une correction porte toujours un nouveau numéro de version.

## Utiliser le SDK

Le plus simple est le template `dotnet new xenoui`, qui génère le projet et `global.json` : voir le [guide d’installation](installation.md#4-créer-une-application-et-utiliser-le-sdk). À la main, créez `MyApp.csproj` :

```xml
<Project Sdk="Xeno.SDK/{{ xeno_version }}">
  <PropertyGroup>
    <TargetFramework>net10.0-android</TargetFramework>
    <XenoAppId>com.example.myapp</XenoAppId>
    <XenoAppName>My App</XenoAppName>
    <ApplicationVersion>1</ApplicationVersion>
    <ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
  </PropertyGroup>
</Project>
```

Pour Android et iOS : `TargetFrameworks=net10.0-android;net10.0-ios`, avec les workloads correspondants installés. La version SDK peut aussi être centralisée dans `global.json` :

```json
{ "msbuild-sdks": { "Xeno.SDK": "{{ xeno_version }}" } }
```

Dans ce cas utilisez `<Project Sdk="Xeno.SDK">`, sans répéter la version. Il ne faut pas ajouter `Xeno.SDK` comme une simple `PackageReference` : le SDK intervient dès l'évaluation du projet. Aucun `NuGet.config` n'est nécessaire : la source nuget.org par défaut fournit le SDK, les runtimes, SkiaSharp et les autres dépendances.

Ajoutez votre `XenoApp`, vos `XenoPage` et `Assets/`, puis `dotnet restore` et `dotnet build -f net10.0-android`. L'identifiant, les versions et les options de signature demeurent la responsabilité de l'application.

Pour passer à une nouvelle version, modifiez `global.json` (ou l'attribut `Sdk`) puis `dotnet restore`. Ne supprimez pas le cache NuGet global pour mettre le SDK à jour.

## Configuration et extensions

| Propriété | Défaut | Usage |
|---|---|---|
| `XenoAppId` | `com.xenoui.unnamed` | Identité, transmise à `ApplicationId` si non explicitement définie |
| `XenoAppName` | nom du projet | Nom affiché Android et plist iOS généré |
| `XenoMinAndroidVersion` | `24` | Minimum Android |
| `XenoMinIosVersion` | `13.0` | Minimum iOS |
| `XenoGenerateEntryPoint` | `true` | Mettre `false` pour fournir sa propre Activity ou son entrée iOS |
| `XenoEnableDefaultAssets` | `true` | Collecte automatique de `Assets/**/*` |
| `XenoEnableImplicitUsings` | `true` | Imports XenoUI et factories ; désactivables sans toucher aux références |
| `XenoEnableMedia` | `false` | Module média optionnel ; audio foreground et sortie vidéo native de référence Media3 sur Android. Voir les [capacités et limites](media-controller.md). |

Les propriétés natives explicitement définies, notamment `ApplicationId`, `ApplicationTitle` et `SupportedOSPlatformVersion`, restent prioritaires. Dans un projet multi-cible, conditionnez les overrides par TFM. Vos autres `PackageReference`, composants C# et extensions NativeTunnel restent libres : le SDK n'impose pas une liste fermée de contrôles.

Les fichiers `Platforms/ios/**/*.cs` sont exclus d'Android et inversement. Avec `XenoGenerateEntryPoint=false`, vos fichiers de la plateforme active restent compilés. Les fichiers liés du SDK sont visibles à l'évaluation design-time sous `XenoGenerated`.

## Assets et permissions

Le chemin `Assets/Images/logo.png` devient la ressource `XenoAsset/Images/logo.png`. Désactivez la collecte automatique pour gérer vous-même les items `XenoAsset`, sans empêcher d'autres ressources natives.

```xml
<ItemGroup>
  <XenoPermission Include="CAMERA"
                  IosKey="NSCameraUsageDescription"
                  Reason="Photographier un document choisi par l'utilisateur" />
  <XenoPermission Include="android.permission.RECORD_AUDIO" />
</ItemGroup>
```

Android accepte un nom court (`CAMERA`) ou le nom qualifié et ajoute `uses-permission` à la racine du manifest, sans doublon. iOS utilise exclusivement la clé `IosKey` explicite et exige une `Reason` non vide ; il n'invente pas de justification utilisateur. Déclarer une permission n'effectue pas sa demande runtime : celle-ci relève du code natif/NativeTunnel.

`AndroidManifest.xml` et `Info.plist` fournis par l'application sont prioritaires, ou peuvent être sélectionnés via `AndroidManifest` et `AppBundleManifest`. Quand une fusion est nécessaire, elle produit un dérivé dans `obj` et ne modifie jamais le fichier source. Sans plist personnalisé, le SDK génère son défaut dans `obj`. Android reçoit des chemins relatifs au projet, conformément à son workload. Les tâches de génération ne s'exécutent pas en `DesignTimeBuild`.

Lorsque vous utilisez `XenoPermission`, déclarez les chemins de manifests personnalisés dans le `.csproj`, pas avec `-p:AndroidManifest=...` ou `-p:AppBundleManifest=...` : ces propriétés globales MSBuild verrouilleraient le chemin source et empêcheraient sa substitution par le dérivé fusionné.

## Vérifier un projet

```powershell
dotnet msbuild MyApp.csproj -p:TargetFramework=net10.0-android `
  -getProperty:ApplicationId -getProperty:AndroidApplication
dotnet build MyApp.csproj -f net10.0-android
```

La présence d'un SDK MSBuild ne garantit pas à elle seule le sélecteur d'appareils ou le debugger d'un IDE : pour lancer sur un appareil, utilisez `xeno run` ([installation](installation.md#5-installer-loutil-xeno)) ou les outils Android de votre IDE ; voir aussi [le tooling](architecture/tooling-and-platforms.md).
