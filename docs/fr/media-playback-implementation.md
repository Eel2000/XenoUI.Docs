# Lecteurs vidéo et audio — plan d’implémentation

[Documentation](index.md) · [Architecture](architecture/index.md) · [NativeTunnel](architecture/text-input-and-native.md)

**Statut : socle portable, audio et parcours vidéo native de référence Android validés sur A15.** Recherche et checkpoint du 6 octobre 2026, branche `codex/media-playback`. Les gates iOS/GPU/Shorts restent ouvertes.

La page Shorts demandée dans SampleApp est reportée après validation des vrais lecteurs. Les correctifs clavier/TextInput sont publiés séparément dans le commit `175054c`. Aucun résultat de performance d’un décodeur vidéo/audio n’est encore mesuré.

### Checkpoint du socle portable

- `XenoUI.Media` optionnel, indépendant d’Android/iOS ; contrats `IMediaPlaybackCapability` et `IMediaPlaybackSession` dans NativeTunnel.
- Contrôleur persistant avec intention utilisateur distincte de la suspension, snapshots de structs mis en cache, handle générationnel et révision de source.
- Play/pause/stop/seek/configuration et dispose idempotent ; commandes limitées au thread propriétaire, sans callbacks développeur ni polling natif dans la lecture du snapshot.
- 23/23 tests média avec backend simulé ; 122/122 tests Core. Le test de 100 000 lectures/synchronisations après warm-up mesure **0 octet managé sur le thread testé**. Ce résultat ne couvre ni JNI/ObjC, ni décodeur, ni rendu vidéo.
- Opt-in SDK `XenoEnableMedia` : module portable et adaptateur `XenoUI.Media.Android` ; aucun adaptateur Android dans les références iOS. Huit évaluations SDK source/package, activé/désactivé, Android/iOS passent.
- Media3 1.11.1 : sources HTTPS et fichiers locaux, extraction bornée d’assets embarqués, play/pause/stop/seek, volume/vitesse/loop, focus géré par Media3, arrêt noisy et arbitrage d’une session audible.
- Une pompe de contrôle native à 250ms par host, runnable préalloué ; les libellés SampleApp suivent les secondes et états par bindings réactifs, sans reconstruire la page. La pompe s’arrête lorsque les sessions ne demandent plus de suivi ou lorsque le host passe en arrière-plan.
- AudioLab ajouté aux destinations communes BottomBar/Drawer/Rail, contrôleur créé à la première activation seulement. Piste PCM locale générée au clic et MP3 HTTPS officiel de démonstration ; aucun autoplay.
- Le test Android a révélé un commit de navigation/lifecycle sur GLThread. Les hosts dispatchent maintenant les commits différés et les actions structurelles du drawer vers le thread de contrôle ; les anciennes entrées/dispose invalident les commits en attente.
- Le test de destruction d'une seconde Activity a aussi révélé une libération prématurée du peer Java de `XenoView`. `Shutdown()` libère le moteur après arrêt du thread GL, de façon idempotente, sans invalider la vue encore détenue par Android pendant le détachement de la fenêtre. Les callbacks tardifs ne touchent plus les ressources libérées.

**Gates ouvertes :** validation iOS sur appareil réel et en Release (l’adaptateur AVPlayer est fusionné le 14 octobre 2026 et validé sur simulateur, voir [contrôleur média](media-controller.md#module-optionnel)), interruptions physiques/écouteurs/Bluetooth, benchmark complet du pump/JNI et du handoff de navigation, transport vidéo vers les slabs ECS, sorties vidéo/GPU et service background. Les phases 0 et 1 ne sont donc pas déclarées terminées sur les deux plateformes. Voir le [contrat et les règles d’usage](media-controller.md).

### Incrément vidéo native de référence

- `MediaView.Video(controller, height)` réserve un viewport ECS rectangulaire fixe. Le contrôleur `MediaKind.Video` reste persistant, avec les commandes et politiques de suspension du socle commun.
- Android : une `TextureView` non interactive au-dessus du host GL, contrôles XenoUI hors viewport, Media3 directement relié à la texture. Les pixels ne passent pas par C# ou Skia. Deux sessions média maximum, dont une seule vidéo.
- Placement en structs, mailbox borné, coalescence ; application des changements de géométrie sur le thread principal, aucun appel natif de placement dans le renderer. Le rectangle suit les contraintes du layout et les dimensions vidéo servent à l'aspect-fit.
- Sortie masquée durant les transitions/gestes de drawer, ainsi que sous les ancêtres scrollables/arrondis ou matrices non identitaires. La frame stabilisée republiant le rectangle est demandée une seule fois. Ce fallback ne promet pas les effets du futur backend GPU : pas d'overlay dans le clip, de feed scrollable, de shared elements ou de clipping animé.
- `VideoLabPage` et destination Video partagée par BottomBar/Drawer/Rail. Démo MP4 officielle Android, load sans autoplay, Play/Pause/Stop/Seek, mute/repeat et reconstruction explicite. Viewport fixe, contrôles scrollables séparés.
- Tests ajoutés : placement, suppression, contraintes non supportées, coalescence et allocations de frames stables ; API vidéo rejetant un contrôleur audio. Compilation de l'adaptateur : zéro warning et erreur.
- Checkpoint suites vidéo : 123/123 Core, 24/24 média et huit évaluations SDK source/package Android/iOS. Ces tests ne mesurent pas le décodage ou les allocations JNI.

La première image effectivement présentée, les posters versionnés, source remplacée sans flash de l'ancienne image et pertes de surface/contexte GPU restent à durcir. La phase 2 n'est pas déclarée complète ; la phase 3 et la page TikTok sont toujours ouvertes.

Validation vidéo A15 : APK final entièrement reconstruit, zéro warning/erreur. Images MP4 réellement visibles, état Playing 1280×720, durée 10s. Pause et seek +2s, reconstruction explicite puis Home→Video conservant la pause à 2s. Drawer ouvert sans vidéo au-dessus du menu, sélection Video et rattachement de la surface après fermeture. Stop revient à Idle avec viewport noir et sortie native cachée. Aucun nouveau crash pour le processus de ce parcours (`logcat -b crash`). Le chargement réseau initial a subi du buffering ; aucune mesure de débit/latence ou garantie A/V n'est déduite de ces captures. Les warnings Mali de chroma siting observés sont un sujet de validation couleur distinct.

### Validation physique Android — 6 octobre 2026

Appareil : Samsung Galaxy A15 SM-A155F, Android 16. APK Release complet, sans fast deployment, build final : zéro warning et zéro erreur. Tests effectués avec la compétence `android-cli`, captures inspectées visuellement et logs de crash consultés.

- Piste locale PCM de 20s : lecture, pause, seek +5s ; retour via onglet et drawer conservant la position et la pause explicite.
- Passage à Home pendant une lecture locale : AudioTrack observé en pause dans `dumpsys audio`, puis reprise de l'intention Play au retour sur l'activité originale.
- MP3 HTTPS Google : état Playing et durée 59s observés ; commandes mute et vitesse 1,5× acceptées et affichées. Ce test n'est pas une mesure acoustique ni une validation de toutes les routes audio.
- Deux reconstructions explicites de l'UI : source HTTPS, pause à 41s et réglages conservés ; commandes Restart/Play puis Stop encore utilisables.
- Reproduction de la création puis destruction d'une seconde Activity : retour à AudioLab, même processus, aucun nouveau crash dans son buffer ; ancien crash de détachement corrigé.
- Retour depuis Home via intent MAIN/LAUNCHER : tâche existante remise au premier plan, sans créer d'activité supplémentaire. Piste arrêtée à la fin des essais.

Les captures sont des artefacts locaux ignorés, pas une dépendance de navigation documentaire GitHub. Cette vérification courte ne remplace pas un soak test, les interruptions réelles ni les benchmarks d'allocation natifs.

## 1. Décisions structurantes

- Réutiliser les moteurs natifs, sans réécrire les codecs : AndroidX Media3/ExoPlayer sur Android, AVFoundation/AVPlayer sur iOS. Même socle de lecture pour les sources audio et vidéo ; l’audio seul ne crée aucune surface vidéo.
- Conserver les contrôles visuels dans XenoUI : boutons, progression, buffering, volume, sous-titres et overlays remplaçables. Ne pas imposer `PlayerView` ou `AVPlayerViewController` comme interface publique.
- Séparer le contrôleur persistant, la session native et l’entité visuelle ECS. Recycler une carte ou reconstruire `Body()` ne doit pas recréer son lecteur ni perdre sa position.
- Faire passer les commandes/capacités par NativeTunnel ; ne pas introduire de types Android, UIKit, Media3 ou AVFoundation dans Core ni dans les applications.
- Ne pas présenter « zéro GC » comme une propriété des codecs : mesurer séparément .NET, Java/ART, mémoire native, GPU, réseau et décodage.
- La sortie vidéo composable dans Skia est une gate préalable au feed animé, pas un détail à repousser après la réalisation de la page.

Media3 permet de brancher la vidéo sur une surface indépendamment des contrôles et recommande de ne pas créer de surface pour l’audio seul. Le choix SurfaceView/TextureView possède des compromis réels de consommation, synchronisation et sortie protégée. [Surfaces Media3](https://developer.android.com/media/media3/ui/surface).

AVPlayer est le socle proposé pour les contenus audiovisuels ; AVPlayerItemVideoOutput fournit une voie de récupération des images pour une composition personnalisée. [Lecture AVFoundation](https://developer.apple.com/documentation/avfoundation/media-playback).

## 2. Périmètre V1

### Commun

- Assets embarqués, fichiers locaux autorisés, URL HTTPS.
- MP4 H.264/AAC pour la vidéo de référence ; MP3 et AAC/M4A pour l’audio de référence.
- Play, pause, stop, seek, reprise, boucle, volume, mute, vitesse selon capacités natives.
- Position, durée connue ou inconnue, intervalle seekable, buffering, fin et erreurs typées.
- En-têtes HTTP, timeout, annulation et retries bornés, sans secrets dans les logs.
- Streaming HLS après validation de la lecture progressive ; formats/capacités interrogés, aucune promesse « tous codecs/tous appareils ».
- Sources avec identité stable et requêtes versionnées ; changement de source pendant prepare/seek/dispose supporté.

Les conteneurs et codecs doivent être distingués : une extension MP4 ne garantit pas le décodage de toutes ses pistes sur tous les appareils. [Formats Media3](https://developer.android.com/media/media3/exoplayer/supported-formats).

### Vidéo

- Poster conservé jusqu’à la première image réellement présentée, pas seulement jusqu’à `Ready`.
- Modes fit/fill/crop, orientation, ratio, clipping et superposition des contrôles.
- Gestion du plein écran par l’architecture de navigation, sans second lecteur.
- Changement/recréation de surface et perte de contexte GPU.
- Un clip actif audible ; préparation du suivant sous budget explicite.
- Sous-titres WebVTT simples, avec piste optionnelle et rendu personnalisable ; typographie/bidi via le moteur de paragraphe. Ne pas décoder/recomposer les cues dans la frame.

### Audio

- Lecteur sans entité vidéo : musique/podcast local et distant.
- Playlist, previous/next, repeat none/one/all et seek relatif.
- Arbitrage entre musique, vidéo et autres applications ; politique explicite de pause, ducking ou mix.
- Interruption par appel, perte de focus, retrait d’écouteurs, changement de route Bluetooth et verrouillage.
- Lecture en arrière-plan optionnelle, contrôles écran verrouillé et métadonnées système.
- Le feed et le lecteur musical ne deviennent pas deux propriétaires concurrents du focus audio.

L’arrière-plan nécessite une session et un service média sur Android, et une configuration audio/background adaptée sur iOS ; ce n’est pas un simple booléen dans le renderer. [MediaSessionService](https://developer.android.com/media/media3/session/background-playback), [configuration audio iOS](https://developer.apple.com/documentation/avfoundation/configuring-your-app-for-media-playback).

## 3. Séparation des responsabilités

| Couche | Responsabilité | Interdit |
|---|---|---|
| Module portable `XenoUI.Media` proposé | Sources, contrôleurs, playlists, politiques, modèle de présentation | Dépendance à une plateforme, décodage |
| Core / ECS-DOD | Slabs d’état, handles générationnels, géométrie, composition et hit-testing | Player natif, HTTP/décodage dans la frame |
| NativeTunnel | Contrats de lecture, sorties et capacités ; transports de commandes/snapshots | Types natifs dans l’API publique |
| Adaptateur Android | Media3, surfaces, audio focus, session/service | UI de contrôle imposée |
| Adaptateur iOS | AVPlayer, sortie vidéo, AVAudioSession, Now Playing | Deux sessions audio globales concurrentes |
| SampleApp | Composition des contrôles et scénarios de pression | Accès privé aux slabs, orchestration native ad hoc |

`XenoUI.Media` doit rester optionnel. Étendre Xeno.SDK pour inclure ses dépendances et configurations Android/iOS seulement si la fonction est activée. La registry NativeTunnel peut rester un dictionnaire cold path ; ses capacités sont résolues une fois lors de l’ouverture, jamais à chaque frame.

Contrats à concevoir avant leur implémentation :

- `MediaSource` : asset/file/HTTPS, identité, métadonnées et référence aux options réseau.
- `MediaController` : objet persistant contrôlant une session ; `VideoController` et `AudioController` comme façades spécialisées si cela améliore l’API.
- `IMediaPlaybackCapability` / `IMediaPlaybackSession` : prepare/play/pause/seek/configure/release.
- `IVideoOutputCapability` : acquisition/attachement/détachement d’une sortie, avec capacités documentées.
- `MediaHandle` : index + génération ; `SourceRevision` pour les événements d’une ancienne source sur une session réutilisée.
- `MediaSnapshot` : struct de lecture, position, durée, buffer, dimensions, état effectif et identifiant d’erreur.
- `AudioPolicy` : intention musicale/vidéo, arrière-plan, interruption et arbitrage.

Le nom exact des API sera arrêté après le prototype de sortie. L’utilisation simple doit rester : créer un contrôleur, fournir une source, composer `Video(...)` ou `AudioControls(...)`. Les slots personnalisés sont évalués à la composition, pas par frame.

## 4. Vidéo et rendu : prototype obligatoire

Les hosts actuels utilisent SKGLSurfaceView / SKGLView. Ne pas supposer qu’une texture Media3 ou un pixel buffer Apple est directement importable par SkiaSharp.

### A. Sortie native — référence et fallback explicite

Android SurfaceView/TextureView et iOS AVPlayerLayer permettent d’abord de valider décodage, son, seek et première image. Tester dans la vraie hiérarchie XenoUI : surface transparente, z-order, overlays, scroll, IME, drawer et deux scènes pendant une transition.

Cette voie doit exposer ses limites de clipping, transformations et effets. Un fallback ne doit pas silently supprimer les effets demandés. Un contenu protégé pourra nécessiter une sortie sécurisée plutôt qu’une texture importable.

### B. Sortie GPU composable — cible pour Shorts

- Android : Surface/SurfaceTexture vers texture externe, puis import ou blit GPU vers texture 2D compatible Skia.
- iOS : AVPlayerItemVideoOutput vers CVPixelBuffer, puis interop avec le backend réel ; OpenGL pour le host existant si viable, Metal seulement avec un backend compatible, pas par hypothèse.
- Prototype natif minimal si les bindings .NET créent des wrappers par image ; transport de handles opaques, pas de nouvelles instances managées à 30/60 Hz.
- UV, rotation, conversion YUV/RGB, gamme/couleur SDR, timestamps et synchronisation A/V validés.
- Pas de conversion en `byte[]`, SKBitmap ni copie CPU des pixels à chaque frame.
- « Pas de copie CPU » n’est pas « zero-copy » : mesurer les blits, synchronisations et coûts GPU.
- Images détenues par leases bornés, libérées après la fence/fin de consommation ; pas à la destruction immédiate d’une scène.

Livrable : décision documentée par plateforme sur le backend retenu, matrice d’effets réellement supportés, mesures et limitations. Si la composition GPU échoue, livrer seulement la sortie native limitée et bloquer explicitement la promesse d’un feed entièrement animable.

## 5. Cold path, hot path et threads

### Cold / control path

Résolution des sources, création du contrôleur/lecteur, configuration, métadonnées, connexions réseau, chargement du poster, binding des commandes, création des slots et changements structurels. Les allocations sont autorisées, bornées et mesurées.

Les commandes natives sont exécutées sur le thread exigé par chaque backend, jamais directement depuis le thread OpenGL. Listeners préenregistrés ; pas de nouvelles closures/tasks par image. Les callbacks développeur passent sur le dispatcher applicatif hors du rendu.

### Hot path

Lire un snapshot publié, interpoler une progression numérique, parcourir les slabs, appliquer les mêmes matrices que l’InputSystem et composer la dernière image disponible. Aucun `Body()`, layout stable, accès HTTP, résolution de capability, formatting de texte ou callback développeur par frame.

- Files bornées de structs pour commandes/événements, avec ownership explicite des producteurs. Ne pas choisir SPSC si plusieurs threads natifs écrivent directement.
- Coalescer position/buffering et les images obsolètes ; conserver l’ordre des commandes critiques. Stop/dispose/error ne doivent jamais être perdus lorsque la file est pleine.
- Le renderer ne traite ni erreurs textuelles ni changements structurels ; un consumer control-path publie les états dans l’ECS.
- SourceRevision et génération rejettent les événements tardifs après recyclage, changement de source ou dispose.
- Pas de nouveau timer par carte. Utiliser le scheduler existant pour l’UI et les signaux de nouvelles images.
- Vidéo 30 Hz sur écran 120 Hz : réutiliser l’image, ne pas importer quatre fois la même frame.
- Audio sans UI/progression visible : ne demander aucune frame UI pour entretenir la lecture.
- Les libellés de temps sont actualisés hors rendu uniquement à la précision affichée, pas formatés à 60/120 Hz.

Gate : zéro allocation managée attribuable au rendu/interpolation média après warm-up. Mesurer aussi le contrôle/event path sur une fenêtre complète ; ne pas cacher des allocations régulières sous l’étiquette « cold ». Les allocations internes du lecteur natif restent mesurées séparément.

## 6. Lifecycle et ownership

États : Idle, Preparing, Ready, Buffering, Playing, Paused, Seeking, Ended, Failed, Disposed. Conserver séparément l’intention utilisateur et l’état effectif : un manque de buffer ou une interruption ne signifie pas que l’utilisateur a demandé Pause.

- `XenoPage.OnDeactivated` : vidéo de feed suspendue et sortie détachée ; position retenue.
- Une reconstruction/recomposition change l’entité, pas l’identité du contrôleur.
- Une carte virtualisée possède un lease de sortie, pas un lecteur propre permanent.
- `OnBackground` : feed arrêté ; audio continue seulement avec opt-in et configuration système valide.
- Au retour, reprendre seulement si l’intention est toujours Play, la source valide, la page/politique éligible et le focus disponible.
- Fin d’interruption : pas de reprise après une pause explicite intervenue entre-temps.
- `OnLowMemory` : libérer préchargement, posters non utilisés et caches optionnels avant le lecteur actif.
- Dispose idempotent, suppression des observers, invalidation des handles, libération native et GPU ordonnée.
- Une session musicale peut appartenir à l’application/service et survivre à une page ; elle ne survit pas par hasard à une référence oubliée.

L’arbitrage natif ne doit pas être doublé : laisser Media3 gérer l’audio focus quand configuré ainsi, puis refléter ses décisions dans le modèle commun. Sur iOS, un coordinateur configure l’unique AVAudioSession en fonction des usages actifs. [Audio focus Android](https://developer.android.com/media/optimize/audio-focus), [interruptions iOS](https://developer.apple.com/documentation/avfaudio/handling-audio-interruptions).

## 7. Budget du feed, cache et réseau

- 10 000 publications = données/identités, pas 10 000 lecteurs, textures ou téléchargements.
- Un lecteur audible ; pool maximal initial de deux sessions vidéo, réduit à une selon les ressources et les capacités de décodage. Le second lecteur n’est pas une garantie sur tous appareils.
- Préparer le prochain clip selon visibilité et direction ; ne pas décoder tout l’overscan de LazyColumn.
- Priorités : visible > suivant > précédent ; annuler/coalescer les demandes lors de swipes rapides.
- Préchargement de données compressées distinct du pré-décodage ; budgets explicites pour RAM, GPU, buffers et disque.
- Cache borné avec éviction et identités tenant compte de l’autorisation ; aucune fuite entre comptes.
- Différencier arrêt réseau, source invalide, format non supporté, HTTP refusé et decoder unavailable.
- Le réseau média reste sous la responsabilité du backend de lecture ; ne pas télécharger un flux complet en C# pour simuler une source locale.
- Snapping/pager et orchestration des likes/stagger sont des travaux UI distincts, pas des fonctions du décodeur.

## 8. Phases et gates

| Phase | Livrable | Condition de passage |
|---|---|---|
| 0 — Contrats et dépendances | États, ownership, capacités, structure des modules, versions .NET/Media3 compatibles | Build Android ; validation de disponibilité des API iOS avec SDK/workload cible |
| 1 — Audio foreground | MP3/M4A asset et HTTPS, play/pause/seek/volume/loop, focus et interruptions | Tests Core avec backend simulé + lecture réelle sur les deux plateformes |
| 2 — Vidéo native de référence | MP4 asset et HTTPS, poster/first-frame, ratio et son synchronisé | Recréation de surface, navigation/Back, lifecycle sans crash ni son fantôme |
| 3 — Interop GPU | Backend composable Skia et leases ; fallback documenté | Overlays/clip/transitions fiables + absence de copie CPU et allocations par frame mesurées |
| 4 — Streaming et contrôle UI | HLS, buffering, erreurs/retry, seekable ranges, pistes et slots accessibles | Tests de serveurs déterministes, réseau dégradé et commandes rapides |
| 5 — Audio background | Session/service Android ; audio session, background modes et commandes distantes iOS | Verrouillage, écouteurs, Bluetooth, interruptions et arrêt utilisateur validés |
| 6 — Feed Shorts et AudioLab | Entrées communes tabbar/drawer/rail, vrai clip et vraie piste audio | Virtualisation + pool borné + aucune lecture hors politique de visibilité |
| 7 — Stabilisation | Docs API, scénario pression, traces et mesures reproductibles | Gates runtime/mémoire validées ; limitations explicitement publiées |

Implémenter les phases par incréments reviewables sur `codex/media-playback`. Ne pas prendre l’absence d’un appareil/toolchain iOS pour une validation iOS : rapporter séparément les gates ouvertes.

### Configuration SDK à prévoir

Bindings Media3 officiels du dépôt .NET Android, versions cohérentes entre modules common/exoplayer/HLS/session, sans inclure `media3-ui` si non nécessaire. [Inventaire .NET Android](https://github.com/dotnet/android-libraries/blob/main/docs/artifact-list-with-versions.md).

Xeno.SDK gère de manière opt-in les assets média, permissions réseau, service/foreground-service mediaPlayback et sa déclaration Android, ainsi que les modes background iOS. Les métadonnées artwork restent bornées et hors rendu. Contrôles système iOS via Now Playing et RemoteCommandCenter. [Commandes distantes Apple](https://developer.apple.com/documentation/mediaplayer/remote-command-center-events).

## 9. Tests et mesures obligatoires

### Contrats Core / fake backend

- Double dispose, source remplacée pendant prepare, seek success/error après changement de source.
- Événements d’une ancienne génération ; lecture pause/play rapide ; queue saturée sans perte de stop.
- Arbitrage vidéo/audio, perte de focus, pause utilisateur pendant interruption, reprise conditionnelle.
- Controller conservé après rebuild, sortie relâchée après recyclage, resource fence respectée.
- Source live/durée inconnue sans division par zéro ; seek hors plage ; metadata/error nullable.
- Pas de `Body()`/layout/formatter/callback de slot dans une frame stable.

### Appareils physiques

- A15, Note20 et tablette Android quand connectés ; iPhone/iPad avec environnement de build disponible.
- Portrait/paysage, retour natif, drawer, transitions annulées, onglets, IME sur commentaires.
- Feed actif → autre onglet → arrière-plan → verrouillage → foreground.
- Écouteurs/Bluetooth retirés, focus pris par une autre app et interruption réelle avec accord utilisateur.
- Fichier corrompu, 404, timeout, réseau coupé/repris, HLS adaptatif, surface/context loss.
- Audio background : notification/écran verrouillé et commandes pause/next cohérents, pas de relance non sollicitée.

### Pressure test et publication des résultats

- 10 000 identités ; 1 000 changements de clip et 100 allers-retours navigation/rebuild.
- Mesurer nombre de sessions/décodeurs/surfaces vivants, mémoire .NET/ART/native/GPU et stabilité après warm-up.
- Allocations/frame et nombre de GC par heap ; allocations des callbacks JNI/ObjC incluses dans l’analyse.
- Frame time P50/P95/P99 et jank à 60/120 Hz pour l’UI ; FPS média, dropped frames et sync A/V séparés.
- Temps prepare → Ready et prepare → première image effectivement affichée, local/froid/chaud/réseau séparés.
- Latence du changement de clip, coût de seek, buffering, CPU et effet thermique sur test long.
- Un feed 30 FPS n’est pas un feed vidéo 120 FPS parce que son écran fonctionne à 120 Hz.
- Aucun objectif chiffré de latence avant baseline physique ; fixer les seuils de régression après mesure.

## 10. Dette explicitement hors V1

DRM/Widevine/FairPlay et sorties sécurisées avancées ; PiP ; Cast/AirPlay UI dédiée ; téléchargements offline protégés ; HDR avancé ; gapless/crossfade garanti entre sources ; égaliseur/mixeur/DSP et effets sonores basse latence ; enregistrement microphone/caméra ; montage/transcodage ; waveforms temps réel ; synchronisation multi-lecteurs.

L’animation d’éléments, le stagger et les chorégraphies restent dans la dette `Element Animation & Choreography Engine`. La lecture vidéo n’autorise pas l’exécution de code développeur dans le hot path et ne remplace pas ce moteur.

## Prochaine action après validation

Commencer par les contrats et le lecteur audio foreground, puis un prototype vidéo natif et GPU sur les hosts actuels. La page Shorts avec vrais médias est réalisée ensuite, pas comme substitut aux gates du lecteur.
