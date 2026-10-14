# Documentation XenoUI

[Installation](installation.md) · [Guide développeur](developer-guide.md) · [Dépannage](troubleshooting.md) · [Architecture](architecture/index.md)

Cet index est le point d’entrée officiel de la documentation XenoUI. Il distingue les documents de référence actuels des notes historiques afin d’éviter qu’un ancien plan soit pris pour le comportement du code.

## Parcours recommandés

### Développeur d’application

1. [Installation](installation.md) : packages, SDK, templates, outil `xeno` et previewer Visual Studio, depuis nuget.org.
2. [Guide développeur](developer-guide.md) : création d’un projet, API, état, navigation, assets, texte, appareils et IDE.
3. [Dépannage par symptôme](troubleshooting.md) : appareil absent, build, écran vide, scroll, clavier, assets et navigation.
4. [Navigation et NavigationScaffold](navigation-scaffold.md) : piles, transitions, navigation adaptative, FAB, action centrale, formes et RTL.
5. [Xeno.SDK](xeno-sdk.md) : SDK NuGet, configuration du projet et personnalisation du build.
6. [Démonstration API](sample-api.md) : HTTP portable, liste distante, annulation et tests, sans SQLite.
7. [Texte contraint et paramètres typés](text-layout-navigation.md) : wrap, ellipsis, AutoSize, payloads de navigation et espace réservé à la BottomBar.
8. [Visual Studio](visual-studio.md) : templates de projet et d’éléments, `[Preview]` et fenêtre XenoUI Preview.
   [VS Code](vscode.md) : panneau XenoUI Preview et création de projets, pages et composants sur macOS, Linux et Windows (prochaine préversion).

### Contributeur framework

1. [Vue d’ensemble de l’architecture](architecture/index.md).
2. [Runtime, composition et rendu](architecture/runtime.md).
3. [Navigation et scènes ECS](architecture/navigation-and-scenes.md).
4. [Texte, IME et NativeTunnel](architecture/text-input-and-native.md).
5. [SDK, plateformes et outillage](architecture/tooling-and-platforms.md).
6. [Décisions, dette et trajectoire V1](architecture/decisions-v1.md).
7. [Benchmark A15](runtime-benchmark-a15.md) : référence mobile Release, CPU/thread OpenGL, allocations et startup ; [baseline PC comparative](runtime-benchmark.md).
8. [Suivi de stabilisation](runtime-critical-stabilization.md) : correctifs vérifiés et gates encore ouverts.
9. [Plan lecteurs vidéo et audio](media-playback-implementation.md) : socle portable, audio foreground Android, interop GPU et gates préalables au feed Shorts. [Contrôleur média](media-controller.md) : API, SDK, threading et limites actuelles.
10. [Reprise graphique Android](android-graphics-lifecycle.md) : perte du contexte EGL, ownership GPU et conservation des scènes après verrouillage.

## Statut des documents

| Document | Statut | Rôle |
|---|---|---|
| [Installation](installation.md) | **Canonique, preview nuget.org** | Installer et mettre à jour packages, SDK, templates, `xeno` et previewer |
| [Guide développeur](developer-guide.md) | **Canonique** | Contrat d’usage public constaté dans le code actuel |
| [Xeno.SDK](xeno-sdk.md) | **Canonique, preview nuget.org** | Configuration du SDK MSBuild |
| [Visual Studio](visual-studio.md) | **Canonique, preview** | Templates, `[Preview]` et extension XenoUI Preview |
| [VS Code](vscode.md) | **Canonique, non publiée** | Extension XenoUI pour VS Code : aperçus, templates, appareils |
| [Navigation et NavigationScaffold](navigation-scaffold.md) | **Canonique** | API de navigation, transitions, Scaffold et lifecycle |
| [Dépannage](troubleshooting.md) | **Canonique** | Diagnostic opérationnel |
| [Module d’architecture](architecture/index.md) | **Canonique** | Référence interne et guide de contribution |
| [Benchmark runtime](runtime-benchmark.md) | Mesures locales datées | Baseline CPU/allocation du checkpoint testé ; pas une garantie mobile |
| [Benchmark A15](runtime-benchmark-a15.md) | Référence mobile mesurée | CPU, thread OpenGL, startup et conditions thermiques sur SM-A155F |
| [Stabilisation runtime](runtime-critical-stabilization.md) | Suivi du checkpoint | Tests, correctifs et travail restant avant validation complète |
| [Lecteurs vidéo et audio](media-playback-implementation.md) | Audio Android validé ; vidéo native de référence | Architecture, phases et validation média avant la démo Shorts |
| [SDK et setup projet](sdk-and-project-setup.md) | Historique | Anciens imports locaux ; utiliser le guide Xeno.SDK |
| [Flutter-style text input](flutter-style-text-input.md) | Note de conception | Recherche ayant guidé le tunnel IME ; pas un contrat public |
| [Plan text input](text-input-implementation.md) | Historique | Proposition antérieure à l’implémentation actuelle |
| [Architecture historique](architecture.md) | Remplacé | Conservé pour traçabilité ; utiliser le module d’architecture |
| [Cycle de vie historique](app-lifecycle.md) | Historique | Peut contenir des détails obsolètes |
| [Roadmap V1 historique](v1-roadmap-and-architecture.md) | Historique | Intentions initiales, pas état d’avancement officiel |

## Source de vérité

En cas de contradiction, appliquer cet ordre :

1. code et tests de la branche courante ;
2. documentation canonique ;
3. notes de conception ;
4. plans et roadmaps historiques.

XenoUI est pré-V1 : un comportement observé n’est stable que s’il est couvert par un test et explicitement présenté comme contrat. Les limitations connues sont centralisées dans [Décisions et trajectoire V1](architecture/decisions-v1.md#3-dette-technique-et-risques).

## Conventions

- Les dimensions publiques sont exprimées en unités logiques : dp Android, points iOS.
- Les couleurs sont des entiers ARGB `0xAARRGGBB`.
- Les positions de texte utilisent des offsets UTF-16, comme les API natives Android/iOS.
- Les commandes sont montrées depuis le dossier de l’application (`MyApp/`, celui du `.csproj`).
- Les blocs PowerShell et Bash signalent les différences de syntaxe quand elles comptent.
