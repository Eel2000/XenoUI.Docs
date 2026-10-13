# Installer XenoUI : packages, SDK, outil `xeno` et previewer

[Documentation](index.md) · [Xeno.SDK](xeno-sdk.md) · [Visual Studio](visual-studio.md) · [Guide développeur](developer-guide.md) · [Dépannage](troubleshooting.md)

Ce guide installe tout ce qu’il faut pour créer, construire et prévisualiser une application XenoUI **hors du dépôt**. Pour contribuer au framework lui-même, voir le [guide développeur](developer-guide.md#3-cloner-restaurer-et-valider-le-dépôt).

> **Statut : `0.1.0-preview.5`, preview locale.** Les packages ne sont pas encore publiés sur nuget.org, ni l’extension sur le Visual Studio Marketplace. Aujourd’hui, on les produit depuis un clone du dépôt (§ 3 B). Les commandes « nuget.org » (§ 3 A) s’appliqueront telles quelles dès la publication.

## 1. Ce qui s’installe, et comment

| Élément | Rôle | Installation |
|---|---|---|
| `Xeno.SDK` | SDK MSBuild de l’application : runtimes, points d’entrée, assets | **Rien à faire** : restauré par NuGet depuis `global.json` |
| `XenoUI.Core`, `XenoUI.StandardLibrary`, `XenoUI.Platforms`, `XenoUI.Media*` | Runtime et hôtes Android/iOS | **Rien à faire** : référencés par `Xeno.SDK` |
| `XenoUI.Templates` | Modèles « XenoUI App / Page / Component » (CLI et Visual Studio) | `dotnet new install` (§ 4) |
| `XenoUI.Tooling` | Outil `xeno` : appareils Android, build, installation, lancement | `dotnet tool install` (§ 6) |
| Extension **XenoUI Preview** | Aperçus `[Preview]` dans Visual Studio | Fichier `.vsix` (§ 7) |

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

## 3. Obtenir les packages

### A. Depuis nuget.org (après publication)

Aucune configuration : les commandes des sections suivantes trouvent les packages sur nuget.org. Omettez simplement `--feed`, `--add-source` et l’étape B.

### B. Depuis un clone du dépôt (aujourd’hui)

```powershell
git clone https://github.com/Eel2000/XenoUI.git
cd XenoUI
./tools/Pack-XenoSdk.ps1 -IncludeMedia -IncludeVisualStudio
```

| Option | Effet |
|---|---|
| `-IncludeMedia` | Ajoute `XenoUI.Media` et `XenoUI.Media.Android` (`<XenoEnableMedia>true</XenoEnableMedia>`) |
| `-IncludeVisualStudio` | Construit aussi l’extension Visual Studio |
| `-SkipIos` | Poste sans workload iOS : `XenoUI.Platforms` ne contient alors que la cible Android |

Résultat :

- **feed local** `artifacts/packages/` : `Xeno.SDK`, `XenoUI.Core`, `XenoUI.StandardLibrary`, `XenoUI.Platforms`, `XenoUI.Media*`, `XenoUI.Tooling`, `XenoUI.Templates` ;
- **extension** `artifacts/visualstudio/XenoUI.VisualStudio.0.1.0-preview.5.vsix`.

Dans la suite, `<feed>` désigne le chemin absolu de `artifacts/packages`, par exemple `D:/src/XenoUI/artifacts/packages`.

## 4. Installer les templates

```powershell
# nuget.org
dotnet new install XenoUI.Templates::0.1.0-preview.5
# feed local
dotnet new install <feed>/XenoUI.Templates.0.1.0-preview.5.nupkg
```

Vérification : `dotnet new list xenoui` affiche `xenoui`, `xenoui-page` et `xenoui-component`. Visual Studio les montre dans *Créer un projet* et *Ajouter › Nouvel élément* après redémarrage.

Mise à jour : réinstallez la nouvelle version (`dotnet new install ...` remplace l’ancienne). Désinstallation : `dotnet new uninstall XenoUI.Templates`.

## 5. Créer une application et utiliser le SDK

### Avec le template

```powershell
# nuget.org
dotnet new xenoui -n MyApp
# feed local : génère un NuGet.config qui pointe vers le feed
dotnet new xenoui -n MyApp --feed <feed>

cd MyApp
dotnet build -f net10.0-android
```

Dans Visual Studio : *Créer un projet* › « XenoUI App » ; les mêmes options apparaissent dans l’assistant. `--ios` ajoute `net10.0-ios`, `--appId com.example.myapp` fixe l’identifiant. Voir [Visual Studio](visual-studio.md#créer-un-projet).

### Ce que le template met en place

```text
MyApp/
├─ MyApp.csproj        <Project Sdk="Xeno.SDK">, TargetFramework, XenoAppId…
├─ global.json         { "msbuild-sdks": { "Xeno.SDK": "0.1.0-preview.5" } }
├─ NuGet.config        (seulement avec --feed) source « xeno-local »
├─ App.cs
├─ Pages/HomePage.cs   avec [Preview]
└─ Components/Counter.cs
```

Le **SDK s’installe tout seul** : au premier `restore`/`build`, MSBuild lit la version dans `global.json` et NuGet télécharge `Xeno.SDK`, qui référence à son tour les runtimes de la même version.

### Sans template (projet existant)

1. Ajoutez `global.json` à la racine de la solution :

   ```json
   { "msbuild-sdks": { "Xeno.SDK": "0.1.0-preview.5" } }
   ```

2. Remplacez l’en-tête du projet par `<Project Sdk="Xeno.SDK">` et fixez `TargetFramework` (`net10.0-android`, `net10.0-ios` ou les deux).
3. Pour le feed local, ajoutez un `NuGet.config` :

   ```xml
   <configuration>
     <packageSources>
       <add key="xeno-local" value="D:/src/XenoUI/artifacts/packages" />
     </packageSources>
   </configuration>
   ```

   La source nuget.org héritée reste nécessaire pour SkiaSharp et les autres dépendances ; n’ajoutez pas `<clear />` sans la redéclarer.

Toutes les propriétés (`XenoAppId`, `XenoAppName`, minimums OS, permissions, assets, média) sont décrites dans [Xeno.SDK](xeno-sdk.md).

### Changer de version

Modifiez la version dans `global.json`, puis `dotnet restore`. Une version déjà restaurée n’est jamais retéléchargée : les caches NuGet sont immuables par version. Un package corrigé doit donc porter un **nouveau** numéro ; ne supprimez pas le cache global pour « forcer » une mise à jour.

## 6. Installer l’outil `xeno`

```powershell
# nuget.org
dotnet tool install --global XenoUI.Tooling --version 0.1.0-preview.5
# feed local
dotnet tool install --global XenoUI.Tooling --version 0.1.0-preview.5 --add-source <feed>
```

```powershell
xeno devices
xeno run --project MyApp/MyApp.csproj --device <device-id>
```

`xeno devices` liste les appareils Android vus par `adb` (état, API, ABI). Mise à jour : `dotnet tool update --global XenoUI.Tooling --version <version> [--add-source <feed>]`. Désinstallation : `dotnet tool uninstall --global XenoUI.Tooling`. Pour un outil par dépôt plutôt que global, utilisez `dotnet new tool-manifest` puis `dotnet tool install XenoUI.Tooling` sans `--global`.

## 7. Installer le previewer (extension Visual Studio)

1. Fermez Visual Studio.
2. Double-cliquez `XenoUI.VisualStudio.0.1.0-preview.5.vsix` et validez l’installation, ou en ligne de commande :

   ```powershell
   & "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\VSIXInstaller.exe" artifacts\visualstudio\XenoUI.VisualStudio.0.1.0-preview.5.vsix
   ```

   (Adaptez `18\Community` à votre édition : `18\Professional`, `2022\Enterprise`, etc.)
3. Ouvrez Visual Studio : *Extensions › Gérer les extensions › Installées* doit lister **XenoUI Preview**.
4. Ouvrez un fichier C# d’un projet `Xeno.SDK`, puis *Affichage › Autres fenêtres › XenoUI Preview* (**Ctrl+Alt+P**). Avec le template, `Pages/HomePage.cs` affiche deux aperçus.

L’extension ne construit pas et ne lance pas le projet : elle démarre son propre serveur de rendu (`dotnet XenoUI.PreviewServer.dll`), isolé de l’IDE, à la première ouverture de la fenêtre. Fonctionnement, attribut `[Preview]` et limites : [Visual Studio](visual-studio.md).

Mise à jour : installez le nouveau `.vsix` (il remplace l’ancien). Désinstallation : *Extensions › Gérer les extensions › Installées › XenoUI Preview › Désinstaller*, puis redémarrez Visual Studio.

## 8. Vérifier l’ensemble

```powershell
dotnet new xenoui -n Check --feed <feed>   # sans --feed avec nuget.org
cd Check
dotnet build -f net10.0-android            # SDK + runtimes restaurés, APK compilé
xeno devices                               # outil installé
```

Puis ouvrez `Check/Pages/HomePage.cs` dans Visual Studio avec la fenêtre XenoUI Preview : la page s’affiche, et se met à jour quand vous modifiez un texte.

## 9. Problèmes fréquents

| Symptôme | Cause et correction |
|---|---|
| `Could not resolve SDK "Xeno.SDK"` | Version absente du feed : vérifiez `global.json`, le `NuGet.config` (`--feed`) et que `Pack-XenoSdk.ps1` a été lancé avec cette version |
| `NETSDK1147` : workload manquant | `dotnet workload install android` (ou `ios`) |
| `NU1101 XenoUI.Platforms` | Feed local produit sans ce package ou ancienne version : relancez `Pack-XenoSdk.ps1`, puis `dotnet restore` |
| Changement non pris en compte après un nouveau pack | Même numéro de version déjà en cache : incrémentez la version (`-Version 0.1.0-preview.5`) et mettez `global.json` à jour |
| `dotnet new xenoui` inconnu | Templates non installés (§ 4) ou autre ruche : `dotnet new list xenoui` |
| `xeno` introuvable | Ouvrez un nouveau terminal (PATH des outils globaux : `%USERPROFILE%\.dotnet\tools`) |
| Fenêtre XenoUI Preview : « Ce fichier n’appartient pas à un projet Xeno.SDK » | Le `.csproj` le plus proche n’utilise pas `Sdk="Xeno.SDK"` |
| Fenêtre XenoUI Preview : « Le serveur d’aperçu s’est arrêté » | Runtime .NET 10 absent du `dotnet` du PATH, ou aperçu bloqué plus de 10 s ; le serveur redémarre à la modification suivante |
| Fenêtre XenoUI Preview : « … n’est pas restauré … runtime embarqué » | Le projet n’a jamais été restauré : lancez un build ou `dotnet restore` pour que l’aperçu utilise la version de XenoUI du projet |
| Fenêtre XenoUI Preview : erreurs sur des API XenoUI récentes | Extension antérieure à la résolution du runtime du projet : installez le `.vsix` à jour |
| Aperçu de page : `NullReferenceException` avec un indice « OnPreview » | L’état lu par la page est préparé dans `OnStart`, qui ne s’exécute pas en aperçu : préparez-le dans `protected override void OnPreview()` de votre `App` ([détails](visual-studio.md#pages-et-application-en-aperçu)) |
| Fenêtre XenoUI Preview : « Aucun [Preview] dans ce fichier » | Ajoutez `[Preview]` à une méthode `static` sans paramètre retournant `XenoComponent`, ou à une `XenoPage` |

D’autres cas, par symptôme : [Dépannage](troubleshooting.md).
