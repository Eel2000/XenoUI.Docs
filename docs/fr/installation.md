# Installer XenoUI : packages, SDK, outil `xeno` et previewer

[Documentation](index.md) · [Xeno.SDK](xeno-sdk.md) · [Visual Studio](visual-studio.md) · [Guide développeur](developer-guide.md) · [Dépannage](troubleshooting.md)

Ce guide installe tout ce qu’il faut pour créer, construire et prévisualiser une application XenoUI. Tous les packages viennent de [nuget.org](https://www.nuget.org/packages?q=XenoUI) : aucun feed ni build local n’est nécessaire.

> **Statut : `{{ xeno_version }}`, preview publiée sur nuget.org.** L’API peut encore changer avant V1. L’extension Visual Studio **XenoUI Preview** arrive bientôt sur le Visual Studio Marketplace.

## 1. Ce qui s’installe, et comment

| Élément | Rôle | Installation |
|---|---|---|
| [`Xeno.SDK`](https://www.nuget.org/packages/Xeno.SDK) | SDK MSBuild de l’application : runtimes, points d’entrée, assets | **Rien à faire** : restauré par NuGet depuis `global.json` |
| [`XenoUI.Core`](https://www.nuget.org/packages/XenoUI.Core), [`XenoUI.StandardLibrary`](https://www.nuget.org/packages/XenoUI.StandardLibrary), [`XenoUI.Platforms`](https://www.nuget.org/packages/XenoUI.Platforms) | Runtime et hôtes Android/iOS | **Rien à faire** : référencés par `Xeno.SDK` |
| [`XenoUI.Media`](https://www.nuget.org/packages/XenoUI.Media), [`XenoUI.Media.Android`](https://www.nuget.org/packages/XenoUI.Media.Android) | Module média optionnel (audio, vidéo) | `<XenoEnableMedia>true</XenoEnableMedia>` dans le projet ; `Xeno.SDK` les référence alors ([détails](media-controller.md)) |
| [`XenoUI.Templates`](https://www.nuget.org/packages/XenoUI.Templates) | Modèles « XenoUI App / Page / Component » (CLI et Visual Studio) | `dotnet new install` (§ 3) |
| [`XenoUI.Tooling`](https://www.nuget.org/packages/XenoUI.Tooling) | Outil `xeno` : appareils Android, build, installation, lancement | `dotnet tool install` (§ 5) |
| Extension **XenoUI Preview** | Aperçus `[Preview]` dans Visual Studio | Bientôt sur le Visual Studio Marketplace (§ 6) |

Les runtimes ne s’ajoutent jamais en `PackageReference` : `Xeno.SDK` choisit les bons packages et l’hôte de la plateforme (`XenoUI.Platforms`) d’après `TargetFramework`.

## 2. Prérequis

| Pour… | Il faut |
|---|---|
| Tout | SDK .NET 10 (`dotnet --version` ≥ 10.0) |
| Android | `dotnet workload install android`, un Android SDK avec platform-tools, un émulateur ou un téléphone en débogage USB |
| iOS | `dotnet workload install ios` ; construire, signer et lancer une app exige **macOS + Xcode** |
| Previewer | Visual Studio 2022 17.14+ ou Visual Studio 2026 (Windows x64/arm64) ; le runtime .NET 10 (fourni par le SDK .NET 10) |

```powershell
dotnet --version
dotnet workload install android
dotnet workload list
```

## 3. Installer les templates

```powershell
dotnet new install XenoUI.Templates::{{ xeno_version }}
```

Vérification : `dotnet new list xenoui` affiche `xenoui`, `xenoui-page` et `xenoui-component`. Visual Studio les montre dans *Créer un projet* et *Ajouter › Nouvel élément* après redémarrage.

Mise à jour : réinstallez la nouvelle version (`dotnet new install ...` remplace l’ancienne). Désinstallation : `dotnet new uninstall XenoUI.Templates`.

## 4. Créer une application et utiliser le SDK

### Avec le template

```powershell
dotnet new xenoui -n MyApp
cd MyApp
dotnet build -f net10.0-android
```

Dans Visual Studio : *Créer un projet* › « XenoUI App » ; les mêmes options apparaissent dans l’assistant. `--ios` ajoute `net10.0-ios`, `--appId com.example.myapp` fixe l’identifiant. Voir [Visual Studio](visual-studio.md#créer-un-projet).

### Ce que le template met en place

```text
MyApp/
├─ MyApp.csproj        <Project Sdk="Xeno.SDK">, TargetFramework, XenoAppId…
├─ global.json         { "msbuild-sdks": { "Xeno.SDK": "{{ xeno_version }}" } }
├─ App.cs
├─ Pages/HomePage.cs   avec [Preview]
└─ Components/Counter.cs
```

Le **SDK s’installe tout seul** : au premier `restore`/`build`, MSBuild lit la version dans `global.json` et NuGet télécharge `Xeno.SDK` depuis nuget.org, qui référence à son tour les runtimes de la même version.

### Sans template (projet existant)

1. Ajoutez `global.json` à la racine de la solution :

   ```json
   { "msbuild-sdks": { "Xeno.SDK": "{{ xeno_version }}" } }
   ```

2. Remplacez l’en-tête du projet par `<Project Sdk="Xeno.SDK">` et fixez `TargetFramework` (`net10.0-android`, `net10.0-ios` ou les deux).

Aucun `NuGet.config` n’est nécessaire : la source nuget.org par défaut suffit. Toutes les propriétés (`XenoAppId`, `XenoAppName`, minimums OS, permissions, assets, média) sont décrites dans [Xeno.SDK](xeno-sdk.md).

### Changer de version

Modifiez la version dans `global.json`, puis `dotnet restore`. Les versions disponibles sont listées sur [nuget.org](https://www.nuget.org/packages/Xeno.SDK#versions-body-tab). Une version déjà restaurée n’est jamais retéléchargée : les caches NuGet sont immuables par version.

## 5. Installer l’outil `xeno`

```powershell
dotnet tool install --global XenoUI.Tooling --version {{ xeno_version }}
```

```powershell
xeno devices
xeno run --project MyApp.csproj --device <device-id>
```

`xeno devices` liste les appareils Android vus par `adb` (état, API, ABI). Mise à jour : `dotnet tool update --global XenoUI.Tooling --version <version>`. Désinstallation : `dotnet tool uninstall --global XenoUI.Tooling`. Pour un outil par dépôt plutôt que global, utilisez `dotnet new tool-manifest` puis `dotnet tool install XenoUI.Tooling` sans `--global`.

## 6. Le previewer (extension Visual Studio)

L’extension **XenoUI Preview** affiche les `[Preview]` de vos pages et composants pendant la frappe. Elle arrive bientôt sur le Visual Studio Marketplace ; une fois publiée :

1. Dans Visual Studio : *Extensions › Gérer les extensions*, recherchez **XenoUI Preview** et installez-la, puis redémarrez Visual Studio.
2. Ouvrez un fichier C# d’un projet `Xeno.SDK`, puis *Affichage › Autres fenêtres › XenoUI Preview* (**Ctrl+Alt+P**). Avec le template, `Pages/HomePage.cs` affiche deux aperçus.

L’extension ne construit pas et ne lance pas le projet : elle démarre son propre serveur de rendu, isolé de l’IDE, à la première ouverture de la fenêtre. Fonctionnement, attribut `[Preview]` et limites : [Visual Studio](visual-studio.md).

Les attributs `[Preview]` sont déjà utilisables dans votre code : ils n’ont aucun effet à l’exécution.

## 7. Vérifier l’ensemble

```powershell
dotnet new xenoui -n Check
cd Check
dotnet build -f net10.0-android            # SDK + runtimes restaurés depuis nuget.org, APK compilé
xeno devices                               # outil installé
xeno run --project Check.csproj --device <device-id>
```

## 8. Problèmes fréquents

| Symptôme | Cause et correction |
|---|---|
| `Could not resolve SDK "Xeno.SDK"` | Version inconnue sur nuget.org ou source NuGet inaccessible : vérifiez la version dans `global.json` et que nuget.org est bien une source active (`dotnet nuget list source`) |
| `NETSDK1147` : workload manquant | `dotnet workload install android` (ou `ios`) |
| `NU1101` sur un package XenoUI | Versions désalignées : `Xeno.SDK` et les runtimes doivent porter la même version ; corrigez `global.json`, puis `dotnet restore` |
| `dotnet new xenoui` inconnu | Templates non installés (§ 3) ou autre ruche : `dotnet new list xenoui` |
| `xeno` introuvable | Ouvrez un nouveau terminal (PATH des outils globaux : `%USERPROFILE%\.dotnet\tools`) |
| Fenêtre XenoUI Preview : « Ce fichier n’appartient pas à un projet Xeno.SDK » | Le `.csproj` le plus proche n’utilise pas `Sdk="Xeno.SDK"` |
| Fenêtre XenoUI Preview : « Le serveur d’aperçu s’est arrêté » | Runtime .NET 10 absent du `dotnet` du PATH, ou aperçu bloqué plus de 10 s ; le serveur redémarre à la modification suivante |
| Fenêtre XenoUI Preview : « … n’est pas restauré … runtime embarqué » | Le projet n’a jamais été restauré : lancez un build ou `dotnet restore` pour que l’aperçu utilise la version de XenoUI du projet |
| Aperçu de page : `NullReferenceException` avec un indice « OnPreview » | L’état lu par la page est préparé dans `OnStart`, qui ne s’exécute pas en aperçu : préparez-le dans `protected override void OnPreview()` de votre `App` ([détails](visual-studio.md#pages-et-application-en-aperçu)) |
| Fenêtre XenoUI Preview : « Aucun [Preview] dans ce fichier » | Ajoutez `[Preview]` à une méthode `static` sans paramètre retournant `XenoComponent`, ou à une `XenoPage` |

D’autres cas, par symptôme : [Dépannage](troubleshooting.md).
