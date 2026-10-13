# SampleApp — appels API portables

[Documentation](index.md) · [Guide développeur](developer-guide.md) · [Xeno.SDK](xeno-sdk.md)

Sélectionner **Live API** dans la barre d’onglets, le rail ou le drawer (menu hamburger), puis toucher un article pour ouvrir son détail. Le raccourci **Live API · load cloud articles** de Home sélectionne la même destination sans empiler une nouvelle page. Les données viennent de [JSONPlaceholder](https://jsonplaceholder.typicode.com/), une API de démonstration gratuite, sans clé. Les données sont fictives ; les écritures côté serveur y sont simulées. Cette démonstration utilise `GET /posts` et `GET /posts/{id}/comments` en HTTPS.

## Frontières

- `Services/ArticleService.cs` : contrat injectable, `HttpClient` réutilisé, vérification HTTP, JSON source-generated pour éviter la réflexion à l'exécution. Aucun type XenoUI, Android ou iOS.
- `Services/ArticleFeed.cs` : `RemoteFeed<T>` partagé par articles/commentaires, états idle/loading/ready/error, rétention en mémoire, rafraîchissement, annulation et numéro de requête rejetant les résultats périmés. Aucun type plateforme ou ECS.
- `Pages/ApiPage.cs` : présentation XenoUI, liste virtualisée à clés stables, Refresh/Retry et lifecycle.
- `ArticleDetailsModel` : sélection appartenant à l'application, sans singleton ; changement d'article invalidant les anciens commentaires.
- `ArticleDetailsArgs` : payload typé transmis par `Navigate<ArticleDetailsPage, ArticleDetailsArgs>()`, conservé par l'entrée de navigation. Le modèle reçoit la sélection dans `OnNavigatedTo` ; voir [texte contraint et navigation typée](text-layout-navigation.md).
- `ArticleDetailsPage` : titre et corps complets, identifiants article/auteur, commentaires (nom, email, texte), Refresh/Retry et Back. Le texte de l'article vient de la liste déjà chargée ; seuls ses commentaires déclenchent une requête supplémentaire.
- `IUiDispatcherCapability` : le NativeTunnel poste seulement la publication d'état et la reconstruction sur le thread UI. Android/iOS fournissent l'adaptateur ; ils ne portent pas le client HTTP ni les modèles métier.

Le réseau et la désérialisation ne s'exécutent ni dans `Body()` ni dans les frames. Les tâches, delegates, chaînes et DTO alloués pour une requête appartiennent au cold path. L'exigence de zéro allocation par frame stabilisée ne signifie pas zéro allocation lors d'un téléchargement ou d'une composition.

## Comportement

- Chargement au premier affichage ; un retour avec un résultat déjà chargé réutilise la mémoire.
- Refresh annule la requête précédente ; les anciens résultats ne remplacent jamais le plus récent.
- Les données précédentes restent affichées pendant un rafraîchissement ou après une erreur.
- Réponse vide, timeout, erreur HTTP et JSON invalide sont distingués dans l'UI.
- Sortie de page, passage en arrière-plan et destruction annulent les opérations ; les callbacks déjà en file sont invalidés.
- Le délai de 20 secondes couvre également la lecture du corps de la réponse.
- Android nécessite `INTERNET`, déjà déclaré dans le manifest du SampleApp. Aucun secret/API key n'est embarqué.
- Liste → détail utilise le `Navigate<T>()` existant et un slide horizontal ; Retour revient à la liste sans perdre ses données ni son scroll.
- Le détail conserve son scroll pour le même article et le remet en haut pour une autre sélection. Le cache de commentaires ne concerne que la dernière sélection ; aucun ancien commentaire n'est affiché pour un nouvel article.

Pas de SQLite, de cache disque, de favoris persistants, de synchronisation ou de garantie offline dans cette étape. Une relance du processus perd les données.

## Tests

Les tests portables compilent les services sous `net10.0`, sans projet plateforme. Ils couvrent HTTP, JSON, annulation, publication différée, résultats hors ordre, erreur/retry, réponse vide, sélection et disposal. Les commentaires d'un autre article et les IDs dupliqués sont rejetés. Le test live lit les articles puis les commentaires du premier article. Il ne prouve pas l'exécution du SampleApp sur téléphone : vérifier séparément chargement, mode avion, Retry, Refresh rapide, liste → détail → Retour, changement d'article pendant le chargement et background/foreground sur appareil. La validation iOS complète requiert macOS/Xcode.
