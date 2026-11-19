# Glossaire

[Documentation](index.md) · [Tutoriel](tutorial-first-app.md) · [Penser en XenoUI](thinking-in-xenoui.md) · [FAQ](faq.md)

Termes employés dans la documentation, par ordre alphabétique.

| Terme | Signification |
|---|---|
| **Action** | Un délégué C# `Action` passé à un composant pour signaler un événement (un toucher, un choix). Les événements **remontent** en actions ; les données **descendent** en valeurs et états. |
| **Ancre / overlay ancré** | Un popup placé près d’un élément (souvent le dernier touché), qui le suit quand il bouge. |
| **App (`XenoApp`)** | La classe d’application : enregistre les pages dans `OnStart`, possède les services partagés, reçoit les rappels de cycle de vie. Voir [Cycle de vie](lifecycle.md). |
| **Arrange (placement)** | Seconde passe de mise en page : le parent donne à chaque enfant sa position et sa taille finales. Voir [Mises en page](layouts.md). |
| **Axe principal** | L’axe selon lequel une `Row`/`Column` dispose ses enfants. |
| **Axe transversal** | L’axe perpendiculaire (vertical pour une `Row`, horizontal pour une `Column`). |
| **Body** | La méthode d’une page qui retourne son interface. Elle doit être pure : aucun effet de bord, aucun objet persistant créé dedans. |
| **Capacité (*capability*)** | Un service d’OS typé exposé par `NativeTunnel` (presse-papiers, saisie, dispatcher UI, média…). Voir [Capacités natives](native-capabilities.md). |
| **Chrome** | Les surfaces de navigation persistantes autour des pages : barre du bas, rail, tiroir, barre du haut. |
| **Clé (*key*)** | Identité stable d’un élément de liste (`key: item => item.Id`) pour suivre l’élément quand la fenêtre se déplace. |
| **Composant** | Une fonction qui retourne un `XenoComponent`. Réutilisable, paramétrée, sans cycle de vie propre. Voir [Créer un composant](components.md). |
| **Composition** | L’exécution de `Body()` et des fonctions de composants pour construire l’arbre d’interface. |
| **Destination** | Une entrée de premier niveau du scaffold (un onglet). Elle garde sa propre pile et sa scène. |
| **dp** | Pixel indépendant de la densité, unité logique de toutes les tailles (points sur iOS). |
| **ECS** | Le moteur orienté données sous XenoUI : chaque élément est une *entité* avec des *composants* (style, visuel, texte…). On n’y touche jamais directement. |
| **Élévation** | Ombre de type Material calculée depuis une valeur en dp : `.Elevation(4)`. |
| **Entité** | L’enregistrement du moteur pour un élément. Un `XenoComponent` en est le handle de 8 octets. |
| **Flex / `FlexGrow`** | Répartition de l’espace restant entre enfants sur l’axe principal. |
| **Hook (`Use…`)** | Une fabrique comme `UseState` ou `UseScroll` qui crée un objet persistant. À appeler dans un **initialiseur de champ**, jamais dans `Body()`. |
| **IME** | *Input Method Editor* : le clavier natif et sa composition de texte. XenoUI l’utilise pour ses champs dessinés. |
| **Liste paresseuse (*lazy*)** | Liste virtualisée (`LazyColumn`, `LazyRow`) qui ne compose que la fenêtre visible. |
| **Mesure (*measure*)** | Première passe de mise en page : chaque élément calcule la taille voulue. |
| **Modal** | Overlay qui bloque la page derrière jusqu’à sa fermeture (alerte, dialogue, feuille). |
| **Modificateur** | Appel chaîné qui modifie un élément (`.Padding(8)`, `.Background(…)`). L’ordre compte rarement. |
| **`NativeTunnel`** | Le pont typé entre XenoUI et les services de la plateforme. Voir [Capacités natives](native-capabilities.md). |
| **`None` (`XenoComponent.None`)** | « Rien » : les modificateurs dessus sont sans effet et les conteneurs l’ignorent. Sert au rendu conditionnel. |
| **Objet persistant** | État qui doit survivre aux reconstructions (`State`, `ScrollState`, `LazyListState`, `TextEditController`). Toujours un champ. |
| **Overlay** | Interface affichée au-dessus de la page et du chrome : snackbar, alerte, dialogue, popup, feuille du bas. |
| **Page (`XenoPage`)** | Un écran navigable. Instanciée **une seule fois** : ses champs persistent. |
| **Preview (`[Preview]`)** | Attribut qui permet à l’IDE ou à un test de rendre un composant ou une page sans appareil. Voir [Tester](testing.md). |
| **Rebuild (`Rebuild()`)** | Rappelle le `Body()` de la page active et remplace sa scène. À utiliser quand la *structure* change ; pour une valeur, un `State`. |
| **Remontée d’état (*hoisting*)** | Sortir l’état d’un composant pour le confier à son appelant. Voir [État](state.md). |
| **RTL** | Mise en page droite à gauche (arabe, hébreu). Le sous-arbre se met en miroir ; préférez `Start`/`End` à gauche/droite. |
| **Scaffold (`NavigationScaffoldState`)** | Le shell adaptatif qui héberge les destinations et choisit barre du bas, rail ou tiroir. |
| **Scène** | L’arbre rendu d’une page, d’un overlay ou du chrome. Une animation de scène fond, déplace ou rogne une scène entière. |
| **Scène conservée** | Arbre de page maintenu monté (par exemple un onglet quitté) pour un retour instantané avec état préservé. |
| **Skia** | La bibliothèque graphique 2D avec laquelle XenoUI dessine (via SkiaSharp). |
| **Slot** | Paramètre qui porte du contenu (`XenoComponent` ou `Func<…, XenoComponent>`) pour qu’un composant héberge l’interface de l’appelant. |
| **Snapshot** | Valeur immuable publiée par un modèle (par exemple `RemoteSnapshot<T>`) et lue par une page. |
| **État (`State<T>`)** | Valeur observable. Affecter une valeur différente notifie les éléments liés. |
| **Thread UI** | Le thread sur lequel on affecte les `State` et où s’exécutent les rappels. Ramenez les résultats de fond avec `IUiDispatcherCapability.Post`. |
| **Track (piste)** | Une propriété animée dans une transition : départ, arrivée, fenêtre de temps et easing. |
| **Valeur liée** | Valeur attachée à un `State<T>` (par exemple `Text(state)`) : elle se met à jour quand l’état change, sans reconstruction. |
| **Virtualisation** | Ne construire que ce qui est visible. |
| **`XenoComponent`** | Le handle de 8 octets retourné par chaque fabrique et modificateur. Valable seulement dans la composition qui l’a créé. |
| **ZStack** | Conteneur qui superpose ses enfants ; le dernier est au-dessus. |
