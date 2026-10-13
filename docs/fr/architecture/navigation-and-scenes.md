# Navigation, scènes ECS et chemin de frame

[Architecture](index.md) · [Runtime](runtime.md) · [API NavigationScaffold](../navigation-scaffold.md) · [Décisions V1](decisions-v1.md)

## Clavier et chrome retained

En mode BottomBar, `HideBottomBarWhenKeyboardVisible` vaut `true` par défaut : la barre et son FAB sont masqués et exclus du hit-testing tant que l'IME est visible. Les scènes restent montées, sans rappel des slots ni de `Body()` par frame, puis redeviennent visibles à la fermeture. `false` conserve le chrome au-dessus du clavier. Rail et Drawer ne sont pas masqués par cette option.

Le host Android transmet séparément la visibilité IME et l'inset du viewport : un inset de barre système seul ne signifie pas que le clavier est ouvert. Les hosts peuvent utiliser `SetViewportBottomInset(inset, keyboardVisible)` ; l'ancienne surcharge à un argument reste disponible pour les hosts dont l'inset ne représente que le clavier (iOS).

Sur Android, la visibilité est lue depuis les insets de la fenêtre racine, y compris quand le parent consomme les insets avant leur arrivée sur la surface. Le host observe les changements de layout natifs et ne transmet que l'occlusion restante dans le cadre visible : avec `adjustResize`, cette valeur peut être zéro alors que l'IME est visible. La BottomBar reste alors masquée, sans soustraire deux fois la hauteur du clavier. Cette observation reste hors de la boucle de rendu ; elle est détachée avec la vue.

### Validation physique — Galaxy A15, 5 octobre 2026

SampleApp Release déployée sur Samsung SM-A155F (Android 16). Le parcours Gallery → TextInput a confirmé le masquage de la BottomBar pendant la saisie, la conservation de `XenoIMEtest`, puis sa restauration au premier Retour natif (fermeture du clavier). Le second Retour dépile TextInput et retrouve Gallery. Aucun message `AndroidRuntime:E` relevé pour le processus testé. Les 102 tests Core passaient avant l'ajout de la politique de racine ; cette validation physique ne couvre pas iOS.

### Validation physique — Galaxy Note20, 6 octobre 2026

SampleApp Release déployée sur Samsung SM-N981B (Android 13). Le parcours Home → TextInput confirme que la BottomBar flottante et le FAB sont masqués pendant la saisie, puis restaurés au Retour natif qui ferme le clavier. Le fond du champ reste blanc pendant l'appui, sans assombrissement de bouton. La sélection par appui long et son menu restent fonctionnels avec le clavier ouvert. Les 120 tests Core passent, incluant le cas IME visible avec occlusion résiduelle nulle après redimensionnement natif.

Le comportement initialement observé (sortie depuis Lab après plusieurs sélections) est remplacé par `RootBackBehavior.PlatformDefault` : Android restaure la destination initiale avant la sortie, iOS ne parcourt pas les onglets au Retour. Les sélections ne deviennent pas des entrées de détail. La politique et l'historique optionnel sont traités sur le cold path ; la présentation réutilise les scènes retained et les tracks compilés.

La validation A15 du paquet Release entièrement régénéré confirme Gallery → People → Lab → Retour → Home → Retour → launcher, puis Gallery → TextInput → ouverture IME → Retour (IME fermée, détail conservé) → Retour (Gallery) → Retour (Home). Le processus testé (PID 10308) n'a aucun message `AndroidRuntime:E`. Les 106 tests Core passent, dont la priorité drawer/détail/racine et les conventions Android/iOS ; le drawer n'a pas été testé physiquement sur ce téléphone en mode BottomBar, et iOS reste validé uniquement au niveau du contrat Core.

## Invariant central

Une transition ne recompose pas les pages à chaque frame. Pour la navigation principale, le cold path construit une destination uniquement lors de sa première ouverture, puis les sélections suivantes publient deux roots déjà montés. Le hot path ne fait qu’évaluer des valeurs et dessiner les deux scènes.

```text
ScaffoldSceneHost
├── ScaffoldChromeScene        identité stable pour les composants standard
└── DestinationSceneRecord[]
    ├── pile Home retained
    ├── pile Search retained
    └── pile Profile retained
```

Le chrome et les contenus ont des owners ECS distincts. Le renderer dessine le contenu actif puis le chrome. L’InputSystem donne la priorité au chrome et n’accepte que la destination entrante ; les coordonnées de hit-test sont inversées avec la même `SceneVisualState` que le renderer.

```text
NavTransitionBuilder → NavTransition immuable → AnimationTrack[]
                                              ↓ frame
                                 SceneVisualState (struct)
```

Les entités portent `SceneId` et `Generation`. Un `EntityHandle` associe l’index de slab et sa génération afin de rejeter un callback tardif après recyclage. Une table contiguë de `SceneRecord` décrit chaque root et un registre dense conserve les entités possédées par scène. La destruction d’une scène ne vide ni les autres scènes ni les caches de fonts/images de durée de vie engine.

## Transaction de navigation

1. Résoudre la pile et la transition sur le cold path.
2. Retrouver le root retained ou composer la scène une seule fois dans un scope `Compose` lié au thread.
3. Mesurer/arranger uniquement si ce root n’a jamais vu ce viewport.
4. Publier le root entrant et rendre le sortant non interactif.
5. Évaluer translation, scale, rotation, clip et opacité dans les slabs/structs.
6. Au commit, conserver la destination sortante inactive. Lors d’un pop, détruire uniquement l’entrée de détail retirée.

Une nouvelle sélection pendant une transition annule la présentation intermédiaire sans faux callback lifecycle. Les scènes de destination restent montées ; une entrée explicitement retirée de pile libère ses callbacks avec ses entités.

Sur les hosts Android/iOS, la fin d’une transition publiée depuis un thread de rendu est dispatchée vers le thread de contrôle du host avant les callbacks publics `OnActivated`/`OnDeactivated`. Le commit capture l’identité de l’entrée de pile sur le cold path : un Back, une nouvelle navigation ou un dispose rejette l’ancien commit même si deux entrées partagent la même instance de page. Le scheduler drawer calcule seulement l’échéance dans la frame ; fermeture structurelle et sélection différée sont dispatchées une seule fois vers le contrôle. Deux tests dédiés couvrent le rendu sur un thread distinct et les commits tardifs invalidés.

## Cold path et hot path

Cold path autorisé : `Body()`, slots Scaffold/FAB, validation, compilation des transitions, création de piles, entités, callbacks, paragraphes et chargement d’assets.

Hot path de transition : horloge monotone, progression, boucle linéaire sur les tracks, interpolation, mise à jour de `SceneVisualState`, rendu Skia, hit-test transformé et demande de frame suivante. Aucun `Body()`, LINQ, collection temporaire, delegate développeur, asset ou compilation de transition ne doit y entrer.

La virtualisation peut déclencher une mutation structurelle lorsque sa fenêtre change : elle est traitée comme un sous-passage cold path explicitement scopé, avec buffers réutilisés. Elle ne doit pas contaminer l’évaluation des transitions lorsque la fenêtre reste stable.

## Ownership et ressources

- entités, callbacks, chaînes et abonnements : propriété de la scène ;
- pages, états de scroll/lazy et contrôleurs texte : propriété de l’application/page ;
- images décodées et fonts : propriété de l’engine, partagées entre scènes ;
- session IME active : une seule session native ; le contrôleur et la sélection logique restent propriété de la page retained.

## Dette officielle : Element Animation & Choreography Engine

Ce moteur futur couvrira apparition/disparition, delay, stagger, ordre, rythme, changements de layout, ajout/suppression/réorganisation, items virtualisés, `AnimationKey` et reduced motion. Il réutilisera l’horloge, le scheduler, les easings et les tracks de navigation, mais restera un module séparé : une transition de scène ne doit jamais appeler arbitrairement le code des éléments.
