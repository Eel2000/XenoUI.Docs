# XenoUI v1 — feuille de route et décisions d’architecture

> **Roadmap historique.** Certains points sont implémentés, modifiés ou abandonnés. La trajectoire auditée actuelle est [Décisions et trajectoire V1](architecture/decisions-v1.md).

## Résumé exécutif

XenoUI possède déjà une direction technique distinctive : une API déclarative en C#, un arbre d’entités compact, un layout maison et un rendu SkiaSharp commun à Android et iOS. Le noyau est suffisamment avancé pour justifier une v1, mais la priorité doit être la fiabilité des contrats internes plutôt que l’augmentation rapide du nombre de widgets.

La v1 doit permettre de construire et publier une application mobile de taille modeste comprenant navigation, état, listes, formulaires simples, images et thèmes. Elle doit être reproductible depuis un clone propre, fonctionner en Release avec trimming, exposer une accessibilité minimale exploitable et conserver une consommation mémoire stable lors des reconstructions et navigations répétées.

Le plan retient six jalons : build reproductible, sécurité mémoire, état et invalidation, cohérence layout/rendu/input, scroll et virtualisation, puis contrôles/accessibilité/publication. Chaque jalon possède des critères de sortie observables. La virtualisation à tailles variables possède désormais une première implémentation ; sa stabilisation et ses benchmarks restent dans le gate V1. Un système complet de hooks positionnels reste reporté.

## 1. État du framework

Le moteur stocke ses composants dans des slabs natifs parallèles. L’identifiant d’une entité correspond actuellement à l’index partagé entre styles, transformations, caches de layout, visuels, texte, interactions et images. Cette représentation est simple, compacte et favorable à l’itération séquentielle.

Le modèle de composition reconstruit l’arbre lors d’un changement structurel. Les pages conservent leurs champs entre les navigations, tandis que les données ECS sont réinitialisées puis réémises par `Body()`. Le layout suit deux phases, mesure ascendante puis arrangement descendant. Le rendu parcourt l’arbre et dessine avec SkiaSharp. L’entrée effectue son propre hit-test.

Cette architecture reste viable pour la v1. Elle doit toutefois expliciter trois contrats aujourd’hui implicites : la durée de validité d’un identifiant, le propriétaire de l’état et des abonnements, et le moment où une mutation peut toucher les données utilisées pour une frame.

Les risques principaux observés dans le dépôt sont les suivants :

| Domaine | Risque actuel | Conséquence v1 |
|---|---|---|
| Mémoire native | cycle de vie partiel, accès sans contrôle de bornes, ancien double-allocation du slab d’images | corruption ou fuite native |
| État | `UseState()` crée un objet à chaque appel de `Body()` et les abonnements ne sont pas scoppés | perte d’état et callbacks périmés |
| Layout | le cache ne représente pas clairement les contraintes d’entrée | positions obsolètes après changement d’un frère ou du viewport |
| Scroll | `.Scrollable()` et les layouts `Lazy*` n’ont pas le même contrat ; le re-fenêtrage existe mais n’est pas déclenché | zones vides et exemples trompeurs |
| SDK | tâche chargée depuis `bin/Debug`, manifests générés et consommés de façon incohérente | clone propre non reproductible |
| Release | découverte de l’application par réflexion globale avec trimming complet | démarrage cassé uniquement en Release |
| iOS | `SKGLView` est signalé obsolète par l’analyseur .NET au profit de Metal | backend à migrer avant la RC |
| Accessibilité | une surface dessinée ne fournit pas encore de hiérarchie sémantique | application inutilisable avec lecteur d’écran |

## 2. Contrat de la v1

La v1 cible Android et iOS avec une seule surface XenoUI principale par fenêtre. Elle garantit les primitives `Column`, `Row`, `ZStack`, `Box`, `Text`, `Image` et `Spacer`, des contraintes explicites, des marges et paddings, les alignements documentés et un rendu cohérent en densités différentes.

La bibliothèque standard doit fournir au minimum un bouton, une saisie texte simple, une case ou un switch, un indicateur de progression et des surfaces de liste. Les contrôles doivent partager les états `normal`, `pressed`, `focused` et `disabled` lorsque ceux-ci sont pertinents. Leur taille, leur couleur et leur contenu s’adaptent au thème et à la taille de texte configurée par le système.

La navigation inclut `push`, `back` et le remplacement de la page active. L’état de page survit à une navigation aller-retour. Les changements asynchrones demandent une frame sans interaction supplémentaire. La v1 définit un thread propriétaire du moteur : les mutations venant d’autres threads sont publiées vers celui-ci avant la construction ou le rendu suivant.

Le scroll simple s’applique à tout conteneur de flux. Les listes virtualisées acceptent une source typée, un builder optionnel et une clé stable optionnelle. Leur index de mesures apprend les tailles variables sans demander de hauteur ou largeur par défaut à l’application. Le maintien exact de l’ancre pendant toutes les mutations de source reste à stabiliser.

La compatibilité v1 signifie qu’une API publique non marquée expérimentale reste compatible dans toute la série `1.x`. Les types ECS, les slabs et les références directes vers leurs éléments restent internes. Les extensions futures passent par des abstractions documentées plutôt que par l’exposition des pointeurs ou des collections internes.

## 3. Principes retenus

### 3.1 Séparer description, instance et rendu

La reconstruction déclarative ne doit pas signifier que toute identité logique disparaît. Les frameworks à reconstruction efficace conservent une couche d’instances qui possède l’état et les objets de rendu. Flutter, par exemple, associe les enfants par type et clé et réutilise les éléments correspondants ; cette réutilisation conserve précisément l’état et les informations de layout.^1

Pour XenoUI v1, une réconciliation générale n’est pas obligatoire. En revanche, les pages, états, abonnements et entrées virtualisées doivent avoir une identité explicite. Un composant reconstruit ne doit jamais réutiliser silencieusement l’identité d’un autre composant créé au même index lors d’une reconstruction ultérieure.

### 3.2 Les contraintes font partie du cache de layout

Une mesure n’est valide que pour les contraintes qui l’ont produite. Un cache de taille désirée doit donc mémoriser, directement ou par version, les contraintes maximales reçues. Une nouvelle contrainte invalide le sous-arbre même lorsque le style et le contenu n’ont pas changé.

L’arrangement suit la même règle : un enfant doit recevoir sa nouvelle position lorsque le parent change la distribution de l’espace. Le fait que le contenu de l’enfant soit propre ne permet pas d’ignorer l’écriture de ses nouvelles limites. Il peut seulement éviter de recalculer ses descendants si leurs contraintes et leur origine restent identiques.

### 3.3 Propriété déterministe des ressources

Le moteur crée les slabs, caches Skia, images et polices ; il est donc responsable de leur libération. La vue plateforme possède le moteur et doit le libérer avec son propre cycle de vie. Microsoft recommande que `Dispose()` soit idempotent : les appels suivants ne doivent rien faire.^2

Les références retournées par un slab sont des emprunts courts. Elles ne doivent pas survivre à `Push`, `Clear`, `Reset` ou `Dispose`, car un redimensionnement peut déplacer le bloc natif. La v1 doit documenter cette règle et limiter ces références aux internals.

À terme, un handle d’entité doit combiner index et génération. Lorsqu’un slot est réutilisé, sa génération change ; toute opération portant un ancien handle échoue de manière déterministe. Cette propriété est plus importante que le maintien d’un handle de huit octets.

### 3.4 Une frame est une transaction

La construction, le layout, le hit-test et le rendu doivent observer une version cohérente de l’arbre. Les mutations de l’application sont collectées puis appliquées avant une frame. Une callback ne réinitialise pas les slabs pendant qu’ils sont parcourus.

Ce contrat simplifie les futures animations, les tâches asynchrones et le multi-touch. Il rend aussi les tests reproductibles : une mutation produit au plus une invalidation planifiée, puis une frame observable.

### 3.5 Mesurer les performances

La formule « zero-GC » est trop large pour décrire le framework : pages, chaînes, callbacks, dictionnaires, état réactif et décodage des assets utilisent légitimement le heap managé. La v1 emploie des objectifs mesurables : aucune allocation managée récurrente dans une frame stable de rendu, aucune croissance mémoire non bornée pendant un scénario défini, et un temps de frame compatible avec 60 Hz sur les appareils de référence.

## 4. Feuille de route

### Jalon 1 — Build reproductible et laboratoire de validation

Le SDK doit construire ses tâches avant leur utilisation ou les embarquer comme assets d’un package. Aucun projet consommateur ne doit dépendre d’un chemin `bin/Debug`. Les propriétés, targets, entrées plateformes, manifests et assets doivent être placés dans des packages NuGet testables localement. Les projets SDK-style disposent nativement de la cible `pack`, et Microsoft recommande de tester l’installation du package dans un projet consommateur réel.^3

La génération des manifests est séparée par plateforme. Les permissions Android sont placées au niveau valide du document, sans préfixer une valeur déjà qualifiée. Les fichiers produits sont explicitement reliés aux propriétés `AndroidManifest` ou `ApplicationManifest` concernées. Une application d’intégration consomme uniquement les packages générés, jamais les références de projet du dépôt.

Le pipeline exécute les tests du noyau, construit Debug et Release, produit les packages, crée l’application d’intégration, publie avec trimming puis lance au minimum des smoke tests sur Android. Le build iOS est exécuté sur un runner macOS.

Le backend iOS passe de `SKGLView` à la vue Metal supportée par SkiaSharp. Cette migration doit conserver les conversions pixels/logical units, le scheduler de frames et le cycle de libération des ressources natives.

**Gate J1**

- clone propre, restore et build sans artefact préexistant ;
- package installable dans un projet vide ;
- démarrage Android et iOS en Release ;
- manifests inspectés et identités d’application correctes ;
- versions .NET et dépendances épinglées.

### Jalon 2 — Mémoire, handles et cycle de vie

Le premier lot de ce jalon rend le slab borné et idempotent, supprime une allocation perdue du slab d’images et rattache `XenoEngine.Dispose()` à la destruction des vues plateformes. La suite introduit un handle générationnel, retire les identifiants `int` de l’API publique et ajoute une vérification d’appartenance au moteur.

Les tables de chaînes et callbacks doivent réutiliser ou remplacer leurs entrées lors des mises à jour fines. Les abonnements sont possédés par un scope et sont retirés lorsque ce scope est démonté. Les caches Skia ont une politique de taille ou de purge afin qu’une application générant des couleurs ou ombres dynamiques ne crée pas une croissance permanente.

**Gate J2**

- tests de bornes, croissance, reset et double-dispose ;
- ancien handle refusé après reconstruction ;
- 1 000 navigations et reconstructions sans croissance native persistante ;
- création/destruction répétée de la vue sans callback vers un moteur libéré ;
- profil mémoire conservé comme artefact CI.

### Jalon 3 — État, invalidation et cycle de frame

La v1 adopte en premier lieu un état explicitement détenu par une page ou un composant. Une API telle que `State<T> CreateState(T initial)` enregistre l’état dans le scope propriétaire et fournit une subscription jetable. `UseState()` ne doit rester public que lorsqu’une identité de slot stable existe ; sinon il est retiré ou marqué expérimental.

Chaque changement appelle un scheduler de frame commun aux plateformes. Le scheduler fusionne les demandes multiples et exécute la mutation sur le thread du moteur. Les callbacks de données asynchrones n’accèdent pas directement aux slabs.

**Gate J3**

- état conservé après rebuild et navigation aller-retour ;
- mise à jour asynchrone visible sans événement tactile ;
- aucune notification d’un composant démonté ;
- plusieurs changements synchrones produisent une seule frame planifiée ;
- exceptions des callbacks rapportées sans corrompre l’arbre courant.

### Jalon 4 — Cohérence layout, rendu et input

Le cache de layout mémorise les contraintes et une version du contenu. `Auto` est représenté séparément de la valeur numérique zéro. `AlignSelf` possède un état `Auto` distinct d’un alignement `Start` explicite. Le moteur teste les layouts vides, contraints, surcontraints, imbriqués et redimensionnés.

Le hit-test réutilise l’ordre de peinture ou un index produit par la même traversée. Il applique les clips et offsets de scroll. Un geste `Cancel` annule l’interaction sans invoquer la callback. Le texte est mesuré et dessiné avec le même typeface, les mêmes métriques et une politique explicite de wrapping et d’ellipsis.

**Gate J4**

- snapshots ou assertions géométriques sur les primitives ;
- modification d’un frère repositionnant les éléments suivants ;
- hit-test correct avec superposition, clipping et scroll ;
- aucun clic après `Cancel` ;
- texte latin, RTL, emoji et changement d’échelle système couverts.

### Jalon 5 — Scroll et listes virtualisées

Le trait `Scrollable` décrit le comportement de viewport ; il ne dépend pas de la valeur de `LayoutDirection`. Les conteneurs non virtualisés scrollent tout leur contenu. `LazyColumn<T>` et `LazyRow<T>` prennent une source, un builder typé optionnel, une clé stable optionnelle et un état optionnel. Aucune taille d’item n’est requise.

Le viewport calcule les indices visibles à partir de l’offset et de la taille d’item. Les enfants sont positionnés à leur coordonnée globale, y compris lorsque le premier enfant créé ne porte pas l’index zéro. Le franchissement de l’overscan déclenche le re-fenêtrage à la frontière de frame. Les éléments retirés libèrent leurs scopes.

La vitesse est exprimée par unité de temps et intégrée avec le delta réel entre frames. Le comportement reste ainsi proche à 60, 90 et 120 Hz. Les changements de source clampent l’offset et conservent si possible l’item ancre.

**Gate J5**

- liste de 10 000 éléments parcourue dans les deux sens sans zone vide ;
- nombre d’entités borné par viewport et overscan ;
- insertion, suppression et réorganisation avec clés stables ;
- fling comparable à plusieurs fréquences de rafraîchissement ;
- scroll imbriqué explicitement supporté ou rejeté avec diagnostic.

### Jalon 6 — Contrôles, accessibilité et publication

Une surface entièrement dessinée doit fournir sa propre arborescence sémantique. Android recommande une hiérarchie virtuelle pour les vues contenant plusieurs éléments logiques et exige qu’une action tactile personnalisée soit aussi exposée aux services d’accessibilité, notamment via `performClick()` pour le clic.^4 Le pont Android peut s’appuyer sur `ExploreByTouchHelper`; iOS doit exposer un conteneur et des éléments d’accessibilité correspondants.^5

La saisie texte nécessite le clavier système, la composition IME, la sélection, le presse-papiers et l’autofill. La v1 privilégie un pont vers les services ou contrôles natifs invisibles plutôt qu’un éditeur de texte complet implémenté dans Skia.

Une application de référence vérifie navigation, liste, recherche, formulaire, chargement, erreur et thème. Elle consomme les packages produits. La documentation décrit uniquement les fonctionnalités garanties et donne leur statut plateforme.

**Gate J6**

- parcours de référence terminé sur Android et iOS ;
- contrôles utilisables avec TalkBack et VoiceOver ;
- focus, clavier, taille du texte et état désactivé vérifiés ;
- packages signés et versionnés en SemVer ;
- guide de migration, politique de compatibilité et limitations publiés.

## 5. Décisions d’API

| Sujet | Décision v1 | Motivation |
|---|---|---|
| Identité | handle interne index + génération | bloque les références périmées |
| État | propriété explicite et subscriptions jetables | comportement prévisible avant un système complet de hooks |
| Reconstruction | page complète pour les changements structurels | conserve la simplicité actuelle |
| Mise à jour fine | réservée aux valeurs ayant un scope vivant | évite les callbacks vers des slots recyclés |
| Dimensions | type ou sentinel `Auto`, zéro reste une dimension valide | supprime l’ambiguïté actuelle |
| Liste v1 | taille mesurée automatiquement, source typée, clé stable recommandée | virtualisation vérifiable et performante |
| Saisie | pont natif | IME et accessibilité réalistes |
| Extensibilité | internals fermés, factories et modifiers publics | permet d’évoluer sans figer les slabs |
| Performance | budgets mesurés par scénario | engagements vérifiables |

## 6. Stratégie de test

Les tests unitaires couvrent les structures mémoire, le calcul des contraintes, l’arrangement, la navigation et l’état. Ils évitent de reproduire ligne par ligne l’implémentation et vérifient des invariants : slabs alignés, handles invalidés, géométrie finie, ordre stable et absence de callback après démontage.

Les tests d’intégration montent un moteur avec un backend de rendu contrôlé, exécutent des séquences d’entrée et inspectent l’arbre final. Les tests visuels rendent un petit corpus d’écrans à densités fixes et comparent les images avec une tolérance adaptée à Skia et aux polices.

Les tests plateformes démarrent l’application packagée, naviguent, scrollent, saisissent du texte et activent les nœuds d’accessibilité. Une application publiée avec trimming est nécessaire : Microsoft indique que l’analyse d’une bibliothèque seule ne dispose pas des implémentations de toutes ses dépendances et recommande aussi une application de test autonome trimée.^6

Les benchmarks publient au minimum : temps médian et p95 de build/layout/render, allocations managées par frame stable, mémoire native après stabilisation, nombre d’entités d’une liste virtualisée et temps de démarrage Release. Les seuils initiaux sont fixés après une baseline, puis toute régression supérieure à la tolérance définie bloque le jalon concerné.

## 7. Séquencement et versions

| Version | Contenu | Condition de promotion |
|---|---|---|
| `0.1-foundations` | J1 et J2 | build propre et mémoire sûre |
| `0.2-state-layout` | J3 et J4 | contrats état/layout/input couverts |
| `0.3-scroll` | J5 | grande liste stable et bornée |
| `1.0.0-beta.1` | début J6 | application de référence fonctionnelle |
| `1.0.0-rc.1` | J6 complet | packages et parcours plateformes validés |
| `1.0.0` | API gelée | aucun blocker et documentation alignée |

Une release n’est pas promue sur la seule base d’une compilation réussie. Tous les gates du jalon doivent produire une preuve conservée : rapport de tests, package, application publiée, profil mémoire ou résultat d’accessibilité selon le cas.

## 8. Hors périmètre v1

La v1 ne promet pas le desktop, le web, le multi-fenêtre avancé, un moteur d’animations général, la conservation keyed complète des sous-arbres virtualisés, un éditeur riche, une compatibilité CSS, un designer visuel ou un hot reload spécifique au framework. Ces travaux peuvent commencer de manière expérimentale, mais ils ne conditionnent pas la sortie et ne doivent pas élargir les contrats publics avant que les fondations soient stabilisées.

## 9. Sources

1. Flutter. “[Inside Flutter](https://docs.flutter.dev/resources/inside-flutter),” sections *Linear reconciliation* et *Tree surgery*. Consulté en septembre 2026.
2. Microsoft. “[Implement a Dispose method](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose).” Consulté en septembre 2026.
3. Microsoft. “[Create a NuGet package using MSBuild](https://learn.microsoft.com/en-us/nuget/create-packages/creating-a-package-msbuild).” Consulté en septembre 2026.
4. Android Developers. “[Make custom views more accessible](https://developer.android.com/guide/topics/ui/accessibility/views/custom-views).” Consulté en septembre 2026.
5. Apple Developer. “[UIAccessibilityContainer](https://developer.apple.com/documentation/uikit/uiaccessibilitycontainer).” Consulté en septembre 2026.
6. Microsoft. “[Prepare .NET libraries for trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming).” Consulté en septembre 2026.
