# Android : reprise après verrouillage et perte du contexte GPU

[Index](index.md) · [Runtime](architecture/runtime.md) · [Dépannage](troubleshooting.md)

## Contrat

La durée de vie des scènes ECS et des pages est indépendante de celle du contexte EGL. Un verrouillage, un passage en arrière-plan ou une recréation de surface ne doit ni rappeler `Body()`, ni changer la pile de navigation, ni effacer les contrôleurs texte ou le scroll. Ce contrat concerne une application dont le processus reste vivant ; la restauration après destruction du processus est une fonctionnalité distincte.

`XenoView` utilise `XenoGLSurfaceView`, un hôte `GLSurfaceView` avec un renderer détenu par XenoUI. Il ne dépend plus de la gestion des ressources internes de `SKGLSurfaceView` 3.119.4, dont `OnSurfaceCreated()` est vide.

## Ressources et threads

- Thread principal : foreground/background applicatif, `OnResume()`/`OnPause()` de la vue GL, NativeTunnel et médias.
- Thread GL, à `OnSurfaceCreated()` : abandon de l'ancien `GRContext` avant libération des wrappers de surface/target, invalidation des caches de rendu, création d'une nouvelle génération GPU. Aucune mutation structurelle ECS.
- Thread GL, à `OnSurfaceChanged()` : abandon de l'ancien render target et relecture du framebuffer, même à dimensions identiques. Le contexte peut rester valide si seule la surface a changé.
- Première frame de la génération : création du contexte Skia, du render target et de la surface. Les allocations sont sur cette frontière cold path.
- Frames suivantes : réutilisation du canvas et des ressources ; aucun tableau de framebuffer, event args ou `SKAutoCanvasRestore` créé par le renderer.
- Shutdown : attendre que le thread GL soit en pause avant de libérer les ressources graphiques et l'engine. Libération idempotente ; les callbacks tardifs du renderer sont ignorés.

Le contexte EGL n'est pas préservé pendant la pause par défaut : le chemin de récupération doit fonctionner sans cette optimisation. Activer la préservation ne dispenserait pas de gérer une perte réelle du contexte.

Les logs `XenoGL` indiquent la génération EGL et la création de sa surface. Ils sont produits seulement aux frontières de contexte/surface, jamais par frame stable. Les géométries du texte natif et de la vidéo sont republiées après récupération. La politique de lecture foreground et les limites de la sous-couche vidéo ne changent pas.

## Validation

Test Core `GraphicsRecoveryRetainsUi` : dix cycles background/foreground avec invalidation des caches, aucune recomposition ni renouvellement des entités, conservation de la navigation, du texte et du scroll, puis retour à la page précédente. Ce test utilise une surface raster : il ne prouve pas, à lui seul, la récupération EGL.

Validation physique Android :

1. Installer un APK complet reconstruit, sans fast deployment.
2. Ouvrir une page, la faire défiler, naviguer vers un détail et saisir du texte.
3. Verrouiller puis déverrouiller normalement le téléphone. Si un code est requis, son propriétaire doit le saisir ; ne jamais désactiver la protection pour tester.
4. Vérifier visuellement l'UI et les états conservés, ainsi que les interactions après la reprise.
5. Répéter le cycle et effectuer Home/retour. Vérifier les nouveaux logs `XenoGL`, le même processus/Activity et l'absence de crash.
6. Vérifier également le feed vidéo, ses contrôles superposés et le drawer.

### Résultats physiques — 8 octobre 2026

Samsung Galaxy A15 (SM-A155F), APK Release entièrement reconstruit : après plusieurs verrouillages/déverrouillages et Home/retour, l'interface revient dans le même processus (PID 7725). Les logs confirment cinq générations EGL successives, soit quatre récupérations après la création initiale, sans crash dans le journal du processus. Le compteur reste à 25 et la position de scroll de Home est conservée, sans interaction nécessaire pour déclencher le rendu. La navigation vers Studio puis Text input et l'ouverture du clavier fonctionnent après ces reprises. Le texte saisi `ResumeTest` est conservé après un déverrouillage manuel ; Retour fonctionne ensuite. Les contrôles superposés du feed et l'ouverture/fermeture du drawer fonctionnent après Home/retour.

Suites automatisées : 145/145 tests Core et 24/24 tests médias réussis. Build Android et SampleApp Release : aucune erreur. Ces tests ne constituent pas une mesure d'allocation sur appareil.

La rotation et la destruction du processus doivent être qualifiées séparément ; un test de verrouillage ne valide pas toute la restauration de session. La lecture et la reprise effective de la vidéo réseau ne sont pas validées par ces captures : le feed affichait ses contrôles mais aucune image vidéo avant le passage en arrière-plan. Aucun test iOS n'a été effectué pour ce correctif Android.
