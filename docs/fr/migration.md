# Guide de migration

[Documentation](index.md) · [Notes de version](release-notes.md) · [Installation](installation.md) · [Dépannage](troubleshooting.md)

Mises à niveau pas à pas entre versions de XenoUI. Chaque section liste ce qu’il faut changer, ce qu’il faut vérifier, et les
erreurs possibles. XenoUI est pré-V1 : lisez les [notes de version](release-notes.md) avant chaque mise à niveau.

## Sommaire

1. [Vers .NET 11 et CoreCLR](#1-vers-net-11-et-coreclr)
2. [Vers preview.6 (depuis preview.5 ou avant)](#2-vers-preview6-depuis-preview5-ou-avant)
3. [Liste de contrôle générale](#3-liste-de-contrôle-générale)

---

## 1. Vers .NET 11 et CoreCLR

### Ce qui change (depuis .NET 10 et Mono)

| Avant | Après |
|---|---|
| `net10.0-android`, `net10.0-ios`, `net10.0` | `net11.0-android`, `net11.0-ios`, `net11.0` |
| SDK .NET 10, packs du workload Android 36 | SDK .NET 11, packs du workload et plateforme Android 37 |
| Runtime Mono sur Android | **CoreCLR** (`UseMonoRuntime=false`, fixé par `Xeno.SDK`) |

### Étapes

1. **Installez le SDK .NET 11** et vérifiez : `dotnet --version`.
2. **Installez le workload et la plateforme Android** :
   ```powershell
   dotnet workload restore MyApp.csproj
   dotnet build MyApp.csproj -t:InstallAndroidDependencies -f net11.0-android -p:AndroidSdkDirectory=<android-sdk>
   ```
   (`InstallAndroidDependencies` télécharge la plateforme API 37 et vous demande d’accepter les licences du SDK Android.)
3. **Reciblez** chaque projet. Dans les `.csproj` :
   ```xml
   <TargetFrameworks>net11.0-android;net11.0-ios</TargetFrameworks>
   ```
   Les bibliothèques qui n’utilisent que du code portable deviennent `net11.0`. Cherchez `net10.0` dans le dépôt — scripts, fichiers
   de CI, `launch.json`, documentation et chemins comme `bin/Release/net10.0-android/…`.
4. **Mettez à jour `global.json`** si vous épinglez le SDK ou la version de `Xeno.SDK`, et passez aux paquets `Xeno.SDK`/`XenoUI.*`
   qui ciblent .NET 11 (gardez tous les paquets `XenoUI.*` sur la **même version**).
5. **Restaurez et recompilez** depuis un état propre : `dotnet clean`, supprimez `bin`/`obj` si besoin, `dotnet build`.
6. **Lancez sur un appareil** et parcourez vos flux principaux.

### CoreCLR sur Android

`Xeno.SDK` active CoreCLR pour vous. Pour rester sur Mono pour l’instant :

```xml
<PropertyGroup>
  <UseMonoRuntime>true</UseMonoRuntime>
</PropertyGroup>
```

À vérifier après le changement :

- **Temps de démarrage et mémoire** sur un appareil d’entrée de gamme (ils peuvent différer entre runtimes) ;
- **Builds Release** avec vos options (trimming, AOT) : retestez, ne supposez pas ;
- **Paquets tiers** et bibliothèques natives : la plupart des paquets `net10.0` fonctionnent sur `net11.0` ; les ressources natives
  doivent exister pour les ABI Android que vous livrez ;
- **Code par réflexion** (sérialiseurs) : préférez ceux générés à la compilation ;
- **Chiffres de performance** : les benchmarks publiés ont été mesurés sur .NET 10 avec Mono ; remesurez sur votre cible
  ([Performance](performance.md)).

### Erreurs possibles

| Message | Cause | Correction |
|---|---|---|
| `NETSDK1147` — les workloads suivants doivent être installés : android | Packs du workload API 37 absents | `dotnet workload restore <projet>` |
| `XA5207` — android.jar introuvable pour l’API 37 | Plateforme du SDK Android 37 absente | `-t:InstallAndroidDependencies` (commande ci-dessus) |
| `NETSDK1045` — le SDK courant ne prend pas en charge .NET 11 | Un SDK plus ancien est sélectionné | Installer le SDK 11 ou corriger `global.json` |
| Erreur de restauration sur un paquet `net10.0` seulement | Une dépendance n’a pas de ressource `net11.0` | La mettre à jour ; la plupart des paquets compatibles se restaurent sur 11 |
| Visual Studio n’affiche pas le workload Android 37 | IDE plus ancien que le workload | Mettre l’IDE à jour, ou compiler en CLI |

## 2. Vers preview.6 (depuis preview.5 ou avant)

preview.6 ajoute les overlays et la barre du haut du scaffold. Rien ne casse les pages existantes, mais deux choses sont bonnes à
savoir :

- Le SDK importe désormais implicitement `XenoUI.StandardLibrary.Overlays` et `XenoUI.StandardLibrary.Shell`. Si vous avez défini
  votre propre type `Snackbar`, `Alert`, `Popup`, `BottomSheet` ou `TopBar`, qualifiez-le ou renommez-le. Sur Android, `Dialog` est
  un alias vers celui de XenoUI : utilisez `Android.App.Dialog` explicitement si vous voulez celui de la plateforme.
- Pour adopter la barre du haut, fixez `TopBar = TopBars.ForActivePage` sur l’état du scaffold et implémentez `ITopBarPage` sur les
  pages qui en veulent une. Les pages qui ne l’implémentent pas n’ont pas de barre.
- Le retour de navigation est désormais le miroir de l’aller ; si vous compensiez l’ancien comportement avec des transitions de
  retour personnalisées, revoyez-les.

## 3. Liste de contrôle générale

- [ ] Lire les [notes de version](release-notes.md) de chaque version entre la vôtre et la cible.
- [ ] Mettre à niveau **tous** les paquets `XenoUI.*`, les templates, l’outil `xeno` et les extensions d’IDE **ensemble**.
- [ ] Mettre à jour `global.json` si vous épinglez le SDK.
- [ ] `dotnet clean`, puis restaurer et compiler pour chaque cible.
- [ ] Lancer vos tests, y compris les tests de rendu headless ([Tester](testing.md)) ; ne régénérez les images **golden** qu’après
      avoir examiné les différences.
- [ ] Lancer sur au moins un appareil réel : navigation, retour, rotation, arrière-plan/premier plan, clavier.
- [ ] Remesurer la performance en Release si le runtime ou le SDK a changé.
- [ ] Commiter la mise à niveau à part, pour pouvoir l’annuler proprement.

Si une mise à niveau casse quelque chose que la documentation décrit comme un contrat, c’est un bug à signaler
([Déboguer](debugging.md)).
