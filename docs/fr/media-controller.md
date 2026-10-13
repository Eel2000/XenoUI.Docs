# Contrôleur média — socle portable

[Documentation](index.md) · [Plan et gates](media-playback-implementation.md)

Checkpoint du 6 octobre 2026 : socle portable et adaptateur **audio/vidéo foreground Android Media3 1.11.1**. Le parcours de base de la sortie vidéo native rectangulaire est validé sur A15 ; ce n'est pas la sortie GPU Skia du futur feed Shorts. AVPlayer iOS et services background ne sont pas implémentés. Créer un contrôleur sans `IMediaPlaybackCapability` échoue explicitement avec `NotSupportedException` ; ouvrir une session vidéo sans host vidéo échoue également.

## Module optionnel

Dans une application Xeno.SDK, `XenoEnableMedia=true` ajoute le module portable et, sur Android uniquement, `XenoUI.Media.Android`. L’Activity générée enregistre la capability avant `OnStart`. Une Activity personnalisée doit fournir cette registration via l’overload `XenoView(context, configureNative)` et relayer `HandleForeground`/`HandleBackground`, puis appeler `Shutdown()` à la destruction. Cette méthode arrête le thread GL et libère le runtime une seule fois, sans disposer prématurément le peer Java encore détenu par la fenêtre ; ne pas appeler `view.Dispose()` depuis `Activity.OnDestroy`. Aucun service Android ni autorisation background n’est ajouté. iOS reste sans capability native à ce checkpoint.

Les deux modules, [`XenoUI.Media`](https://www.nuget.org/packages/XenoUI.Media) et [`XenoUI.Media.Android`](https://www.nuget.org/packages/XenoUI.Media.Android), sont publiés sur nuget.org avec la même version que `Xeno.SDK` ; le SDK les référence automatiquement quand la propriété est activée.

```xml
<PropertyGroup>
  <XenoEnableMedia>true</XenoEnableMedia>
</PropertyGroup>
```

## Ownership et commandes

Créer un `MediaController` sur le thread de contrôle du host, lors d’un lifecycle de création, pas dans `Body()`. Le conserver avec la page ou l’application ; une recomposition ne doit pas réouvrir de session. Appeler `Dispose()` dans le lifecycle de destruction de son propriétaire. Le contrôleur n’est pas thread-safe : toutes ses lectures et commandes appartiennent au même thread.

Après enregistrement d’un backend conforme, la séquence sera :

```csharp
using XenoUI.Media;
using XenoUI.Core.NativeTunnel;
using MediaController = XenoUI.Media.MediaController; // évite Android.Widget.MediaController

var player = nativeTunnel.CreatePlayer(); // MediaKind.Audio par défaut
player.Configure(volume: 0.35f);
player.Prepare(MediaSource.Https("https://example.org/track.mp3"));
player.Play(); // intention de lecture, pas une garantie de son immédiat
player.Suspend(); // conserve l’intention pour un retour éventuel
player.Pause(); // annule l’intention même pendant une suspension
player.Resume(); // ne relance pas après cette Pause
player.Stop(); // nouvelle Prepare nécessaire avant Play
player.Dispose();
```

`Prepare` remplace la source et annule l’intention Play précédente. `Seek` utilise une plage native connue et refuse une source non seekable ; une durée `-1` signifie inconnue. Volume : 0..1 ; vitesse demandée : 0,25..4, sous réserve des capacités du futur adaptateur. Une commande non supportée doit échouer explicitement.

`MediaSource.Asset` accepte un chemin relatif sans traversal. Android résout la ressource `XenoAsset/<chemin>`, normalise les séparateurs Windows, puis l’extrait dans un cache privé à durée de vie host : au maximum huit assets, 16MiB par fichier et 32MiB au total. Cette extraction synchrone est un cold path audio ; elle ne constitue pas la stratégie de streaming/cache pour de grosses vidéos. `File` exige un chemin absolu, mais n’accorde aucun droit d’accès au fichier. `Https` refuse HTTP et les identifiants dans l’URL. Les options HTTP avancées et playlists ne sont pas encore implémentées.

Media3 gère seul le focus Android via `SetAudioAttributes(..., true)` et l’événement audio-becoming-noisy. Le host borne les sessions à deux et retire l’intention native de l’autre session lorsqu’une lecture audible est demandée. `RequiresUserPlay` annule l’intention du contrôleur après perte permanente de focus, retrait d’écouteurs ou remplacement par un autre lecteur. Les interruptions transitoires conservent l’intention. Ces routes physiques restent à valider sur appareils avec accord de l’utilisateur.

## Sortie vidéo native de référence

L'Activity SDK optionnelle utilise un `FrameLayout` contenant la vue Xeno et une `TextureView` non interactive. L'adaptateur est enregistré comme `IMediaPlaybackCapability` et `INativeVideoOutputCapability`. Une Activity personnalisée fournit ce host au troisième argument du constructeur Android et enregistre les deux contrats. Le budget est de six sessions média au total, dont au maximum quatre vidéo ; seule la session nommée par le placement vidéo courant est attachée à la `TextureView`. Chaque lecteur a un tampon borné (démarrage après 1 s, 15 s au plus) pour que plusieurs lecteurs préchargés restent sobres en mémoire et en réseau.

```csharp
// Activation/création de page, thread de contrôle :
var player = Native.CreatePlayer(MediaKind.Video);
player.Prepare(MediaSource.Https(videoUrl));
// Body(), sous un parent CrossAxis(Stretch), hors de tout scroll :
var viewport = MediaView.Video(player, height: 210);
// Play/Pause/Seek/Configure sont les mêmes commandes que pour l'audio.
```

Le handle générationnel seul entre dans le slab visuel. Le renderer publie un rectangle en pixels physiques ; un mailbox borné coalesce les valeurs avant leur application sur le thread principal Android. Aucun appel Media3, positionnement de View ni callback applicatif n'est exécuté dans le renderer. Aucun pixel vidéo n'est copié vers un bitmap ou un tableau C# : Android compose la surface native, **pas Skia**. Cela ne démontre pas un rendu zero-copy GPU.

Sur Android, l'hôte média utilise maintenant une **sous-couche native** : la surface Skia RGBA est au-dessus de la TextureView. Le renderer efface le rectangle vidéo accepté avec un paint `Clear` réutilisé, puis dessine normalement les enfants/siblings suivants. Les contrôles placés après `MediaView.Video()` dans un `ZStack` sont donc réellement superposés et restent hit-testés par XenoUI.

L'aspect-fit, la translation de scroll et le clipping rectangulaire du viewport sont supportés. Le placement transporte des valeurs numériques de rectangle et de clip au thread de contrôle ; Android utilise la translation de View et ses ClipBounds. Pas de crop/fill, clipping arrondi, alpha/rotation de vidéo, blur ou Surface DRM sécurisée. Les ancêtres arrondis, transparents et les matrices non translationnelles (dont le stretch de rebond) masquent la vidéo. Durant une transition ou un drawer ouvert, la surface reste masquée puis rattachée après stabilisation ; pas de snapshot animé de la vidéo. Un noir temporaire est attendu au réattachement : poster jusqu'à la première frame et leases versionnées restent des gates ouvertes. `Playing` n'est **pas une preuve de première image affichée**. La sous-couche native ne rend pas la vidéo composable comme une image GPU Skia.

La page Video du SampleApp conserve son viewport de diagnostic. La page Feed utilise la structure `Page → LazyColumn → ZStack(Video, Column(contrôles))` par item. Quatre [clips Samplelib](https://samplelib.com/sample-videos.html) de 5 à 15 secondes sont lus directement en HTTPS, dont un vertical ; leurs [conditions d'utilisation](https://samplelib.com/license.html) autorisent les assets de démonstration. Un asset distant déclaré par le backend supérieur à 60 secondes est mis en pause. Les liens peuvent changer ou devenir indisponibles ; Retry et les erreurs de connexion sont visibles.

Feed s'appuie sur `IMediaPreloadCapability` : à chaque changement de page il transmet sa liste ordonnée et l'index courant. Le backend Android (Media3 `DefaultPreloadManager`) précharge le clip suivant en mémoire (~2 s), les deux d'après dans un cache disque partagé de 256 Mo (~3 s), le précédent (~1 s) et rien d'autre ; le reclassement annule le travail des pages sautées par un swipe rapide. Une session qui prépare un clip préchargé démarre depuis ces données. Deux contrôleurs suffisent : la page courante joue, l'autre garde le clip précédent (retour arrière immédiat). Le pager publie la page choisie dès le relâchement du doigt ; la page reçoit son lecteur via `NativeVideo(State<MediaHandle>)`, sans reconstruire l'arbre. L'affiche reste peinte au-dessus de la surface jusqu'à ce que le nouveau clip joue (le module notifie dès que la taille vidéo est connue). Cette affiche est la première image du clip : `IMediaThumbnailCapability` l'extrait en arrière-plan (Android `MediaMetadataRetriever`, deux extractions à la fois), la met en cache disque (`cache/xeno-thumbs`) et `ImageFile(State<string?>)` l'affiche, cadrée comme la vidéo ; pendant un swipe la page suivante montre donc déjà son image, et la vidéo la remplace sans saut visible. Les clips doivent être encodés pour le short-video (~1–1,5 Mb/s, index MP4 en tête) : à 6–13 Mb/s, même préchargés, ils démarrent lentement sur un réseau mobile. Video Lab garde le contrôleur détenu/disposé par App. Likes/favoris sont des états locaux de page, les commentaires sont simulés.

Pour les validations physiques de cet incrément, reconstruire l'APK complet (`-t:Rebuild`) et l'installer sans fast deployment. Un paquet incrémental a échoué avant la création de l'Activity avec `UnsatisfiedLinkError` sur `MainActivity.n_onCreate` ; ce problème de cohérence JNI ne doit pas être confondu avec une erreur de lecture Media3.

## Snapshot, threads et hot path

`Synchronize()` est une opération du control path. Sur Android, une seule pompe host à 250ms lit les valeurs natives hors rendu et met en cache les snapshots. Les notifications `Changed` sont délivrées sur le thread principal lors d’un changement d’état ou de seconde affichée. Les handles et révisions obsolètes sont rejetés. `Snapshot` lit ensuite le cache du contrôleur sans rappeler l’adaptateur. Aucun timer par carte et aucune frame Skia pour entretenir le son.

Le contrôleur ne publie pas encore de slab ECS. Ne pas le lire directement depuis un thread GL concurrent : le futur transport control → ECS devra publier les structs de façon cohérente. Aucun timer, subscription globale, frame UI ou `Body()` n’est créé automatiquement par ce module.

Le contrat natif exige des snapshots cohérents, des commandes sur le thread approprié, des révisions capturées à l’origine de l’opération asynchrone et une destruction annulant les callbacks. L’adaptateur Android n’achemine aucun résultat asynchrone d’un ancien item : il arrête/retire la source précédente, puis lit l’état courant du player sur son thread propriétaire. `Stop` et `Dispose` sont directs et ne passent pas par une file susceptible de les perdre. Les leases et transports d’images vidéo restent à implémenter.

## Validation

Résultats du checkpoint : 23/23 média, 122/122 Core, huit évaluations SDK et build SampleApp Android sans warning ni erreur. Après warm-up, la boucle de 100 000 synchronisations/lectures de snapshots avec backend simulé alloue 0 octet managé sur son thread. Ce test ne démontre pas zéro allocation des lecteurs natifs, du pump, des labels réactifs ou de la publication native d’un commit de navigation. Les interruptions physiques, le background-service, iOS et le benchmark complet restent à réaliser.

Au relancement de la suite Core pendant cette validation, le test préexistant de cache a signalé une fois 12 336 octets sur sa boucle mesurée (121/122), puis la suite a repassé à 122/122 sans modification. Ce résultat intermittent reste à isoler dans une campagne dédiée ; le succès suivant ne le transforme pas en preuve de stabilité des allocations.
