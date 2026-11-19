# Publier votre application

[Documentation](index.md) · [Xeno.SDK](xeno-sdk.md) · [Installation](installation.md) · [Capacités natives](native-capabilities.md) · [Performance](performance.md)

D’un build de débogage fonctionnel à un paquet prêt pour les magasins. Les applications XenoUI sont des applications .NET
Android et iOS standard construites via `Xeno.SDK` : le packaging utilise donc les **propriétés MSBuild standard de .NET pour
Android / iOS** ; XenoUI n’ajoute que des aides pour l’identité et les permissions. Cette page liste ce qu’il faut régler et
vérifier, et où s’appliquent les limites du framework.

!!! warning "Pré-V1"
    XenoUI est une préversion. La vérification du SDK (`tools/Test-XenoSdk.ps1`) compile un **APK Android Release sans AOT ni
    trimming** ; AOT, trimming et packaging iOS ne sont **pas validés** par le framework. Testez chaque option que vous activez
    sur un appareil réel avant de publier, et consultez la documentation de publication .NET pour Android/iOS de Microsoft pour
    les noms exacts et actuels des propriétés.

## Sommaire

1. [Liste de contrôle](#1-liste-de-contrôle)
2. [Identité et versions](#2-identité-et-versions)
3. [Permissions](#3-permissions)
4. [Android : signature et packaging](#4-android--signature-et-packaging)
5. [Publier sur Google Play](#5-publier-sur-google-play)
6. [iOS](#6-ios)
7. [Pièges des builds Release](#7-pièges-des-builds-release)
8. [Livraison continue](#8-livraison-continue)

---

## 1. Liste de contrôle

- [ ] `XenoAppId` unique (domaine inversé), nom et version fixés (§2).
- [ ] Versions minimales d’OS décidées (`XenoMinAndroidVersion`, `XenoMinIosVersion`).
- [ ] Seulement les permissions nécessaires, chacune justifiée (§3).
- [ ] Clé de signature Release créée et **sauvegardée** (§4).
- [ ] Build Release testé sur au moins deux appareils réels, dont un d’entrée de gamme.
- [ ] Retour, rotation, arrière-plan/premier plan et absence de réseau vérifiés ([Déboguer](debugging.md)).
- [ ] Aucune journalisation de données utilisateur, aucun point d’accès de test ni clé dans le paquet.
- [ ] Accessibilité, localisation et RTL revus ([Accessibilité](accessibility.md), [Localisation](localization.md)).
- [ ] Éléments de fiche et politique de confidentialité prêts.

## 2. Identité et versions

Dans votre `.csproj` (voir [Xeno.SDK](xeno-sdk.md)) :

```xml
<PropertyGroup>
  <XenoAppId>com.example.notes</XenoAppId>        <!-- devient ApplicationId -->
  <XenoAppName>Notes</XenoAppName>                <!-- devient ApplicationTitle -->
  <ApplicationVersion>12</ApplicationVersion>     <!-- versionCode Android : entier, augmente à chaque envoi -->
  <ApplicationDisplayVersion>1.4.0</ApplicationDisplayVersion>  <!-- affiché aux utilisateurs -->
  <XenoMinAndroidVersion>24</XenoMinAndroidVersion>
  <XenoMinIosVersion>13.0</XenoMinIosVersion>
</PropertyGroup>
```

`XenoAppId` est transmis à `ApplicationId` sauf si vous fixez `ApplicationId` vous-même. **Ne changez jamais l’identifiant après
publication** : un nouvel identifiant est une autre application. `ApplicationVersion` doit augmenter à chaque build envoyé.

## 3. Permissions

Déclarez ce dont l’application a besoin avec des éléments `XenoPermission` ; le SDK les fusionne dans un manifeste **dérivé**
(il ne modifie jamais votre manifeste source) :

```xml
<ItemGroup>
  <XenoPermission Include="INTERNET" />
  <XenoPermission Include="CAMERA"
                  IosKey="NSCameraUsageDescription"
                  Reason="Photographier un document choisi par l'utilisateur" />
</ItemGroup>
```

- Android accepte un nom court (`CAMERA`) ou complet et ajoute une entrée `uses-permission`, sans doublon.
- iOS n’utilise que la `IosKey` explicite et exige une `Reason` non vide montrée à l’utilisateur ; le SDK n’en invente pas.
- **Déclarer n’est pas demander.** Les permissions dangereuses (caméra, localisation…) se demandent à l’exécution, ce qui est du
  code natif : écrivez une [capacité](native-capabilities.md) qui demande et retourne la réponse.
- Demandez une permission **quand la fonctionnalité est utilisée**, expliquez pourquoi, et gérez le refus.
- Demandez-en le moins possible ; les magasins les examinent.
- Fournissez votre propre `AndroidManifest.xml` / `Info.plist` dans le `.csproj` si besoin ; ne fixez pas les chemins de manifeste
  par des propriétés globales `-p:` avec `XenoPermission` ([Xeno.SDK](xeno-sdk.md)).

## 4. Android : signature et packaging

**Créez une clé de release une fois** avec le `keytool` du JDK, et gardez le keystore et ses mots de passe en lieu sûr : perdre la
clé empêche de mettre l’application à jour (sauf si vous avez adopté Play App Signing).

```powershell
keytool -genkeypair -v -keystore notes-release.keystore -alias notes -keyalg RSA -keysize 2048 -validity 10000
```

Référencez-la dans le build **sans commiter de secrets**. Passez les mots de passe par l’environnement ou le coffre de secrets de
votre CI :

```powershell
dotnet publish NotesApp.csproj -f net11.0-android -c Release `
  -p:AndroidKeyStore=true `
  -p:AndroidSigningKeyStore=notes-release.keystore `
  -p:AndroidSigningKeyAlias=notes `
  -p:AndroidSigningKeyPass=env:KEY_PASS `
  -p:AndroidSigningStorePass=env:STORE_PASS `
  -p:AndroidPackageFormats=aab
```

(`env:NOM` fait lire le mot de passe dans une variable d’environnement ; ce sont les propriétés standard de .NET pour Android.)

| Paquet | Valeur de propriété | Usage |
|---|---|---|
| **AAB** (Android App Bundle) | `AndroidPackageFormats=aab` | Envoi à Google Play (requis pour les nouvelles applications) |
| **APK** | `AndroidPackageFormats=apk` | Installation directe, tests, autres magasins |

`-c Release` compte : les builds Debug sont plus gros, plus lents et déboguables. La vérification de XenoUI compile en Release
**sans AOT ni trimming** ; n’activez `RunAOTCompilation` ou `PublishTrimmed` qu’après avoir testé le résultat, car le trimming peut
retirer du code atteint par réflexion (JSON, par exemple : préférez les sérialiseurs générés à la compilation, comme le sample).

## 5. Publier sur Google Play

1. Créez l’application dans la Play Console et complétez la fiche, la classification, le formulaire de sécurité des données et la
   politique de confidentialité.
2. Préférez **Play App Signing** : Google garde la clé finale ; vous signez les envois avec une *clé d’envoi*.
3. Envoyez l’**AAB** sur une piste de **test interne** d’abord, puis test fermé/ouvert, puis production.
4. Augmentez `ApplicationVersion` à chaque envoi.
5. Consultez le rapport de pré-lancement (plantages, accessibilité, permissions) avant de promouvoir une version.
6. Ciblez le niveau d’API que Google exige pour les nouvelles versions : réglez l’OS pris en charge/cible de votre projet et
   gardez la plateforme du SDK Android à jour ([Installation](installation.md)).

## 6. iOS

Compiler, signer et lancer une application iOS **exige macOS et Xcode**, un compte Apple Developer, un certificat de distribution
et un profil de provisionnement. Le chemin iOS de XenoUI n’est **pas encore validé de bout en bout** ; considérez-le expérimental.

```powershell
dotnet publish NotesApp.csproj -f net11.0-ios -c Release
```

Fournissez dans `Info.plist` les descriptions d’usage de chaque permission (`XenoPermission … IosKey/Reason`). Envoyez à TestFlight
via Xcode ou Transporter, testez, puis soumettez à la revue. Suivez la documentation Microsoft de signature et d’archivage .NET
pour iOS pour les propriétés exactes.

## 7. Pièges des builds Release

| Piège | Symptôme | Correction |
|---|---|---|
| Paquet Debug publié | Lent, gros, déboguable | `-c Release` |
| Keystore perdu | Mise à jour impossible | Sauvegarde ; Play App Signing |
| Même `ApplicationVersion` | Envoi refusé | L’augmenter |
| Trimming/AOT activé sans test | Plantages seulement en Release | Tester sur appareil ; générateurs de sources ; garder ce que la réflexion atteint |
| Permission `INTERNET` absente | Le réseau marche en debug, pas en release | La déclarer avec `XenoPermission` |
| Permission déclarée jamais demandée | Fonctionnalité qui échoue sans bruit | La demander à l’exécution ([capacité native](native-capabilities.md)) |
| Point d’accès ou clé de test en dur | Fuite ou mauvais backend | Réglages par configuration ; pas de secrets dans le paquet |
| Performance mesurée en Debug | Fausses alertes | Mesurer en Release ([Performance](performance.md)) |

## 8. Livraison continue

Un pipeline minimal, avec le matériel de signature pris dans les secrets :

```yaml
- run: dotnet test MyApp.Tests -c Release
- run: >
    dotnet publish MyApp/MyApp.csproj -f net11.0-android -c Release
    -p:AndroidPackageFormats=aab -p:AndroidKeyStore=true
    -p:AndroidSigningKeyStore=$RUNNER_TEMP/release.keystore
    -p:AndroidSigningKeyAlias=notes
    -p:AndroidSigningKeyPass=env:KEY_PASS -p:AndroidSigningStorePass=env:STORE_PASS
```

Restaurez le keystore depuis un secret dans un fichier temporaire, ne le commitez jamais, et envoyez l’AAB avec l’outillage de
votre magasin. Gardez le numéro de build monotone (par exemple en dérivant `ApplicationVersion` du numéro d’exécution de la CI).
