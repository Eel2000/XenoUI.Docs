# Texte contraint, navigation typée et contenu du Scaffold

[Documentation](index.md) · [Sample API](sample-api.md) · [Navigation](navigation-scaffold.md)

## Texte

`Text` revient automatiquement à la ligne lorsqu'une largeur finie ne suffit pas. Les retours à la ligne explicites sont conservés. Le layout mesure le paragraphe à la largeur disponible, puis le renderer peint son enregistrement ; aucun shaping n'est exécuté sur les frames stabilisées. Les textes ASCII courts sans contrainte particulière conservent le chemin monoligne optimisé ; les textes Unicode utilisant la police par défaut passent par le paragraphe pour bénéficier du fallback de glyphes. Les ressources de paragraphe appartiennent à l'entité et sont libérées avec elle, y compris lors du recyclage d'items lazy.

```csharp
Text(article.Title, 18).MaxLines(2, ellipsis: true);
Text(article.Body, 16); // Toutes les lignes, sans réduction de police.
Text("Un libellé long", 18).TextWrap(false); // Une ligne, clipping.
Text("Un libellé long", 18).MaxLines(1, ellipsis: true).AutoSize(12);
```

`MaxLines(0)` supprime la limite. `AutoSize` est optionnel et ne descend jamais sous la taille minimale. Lorsque la limite minimale ne suffit pas, clipping ou ellipsis s'applique ; le framework ne réduit pas le texte indéfiniment. La police reste à sa taille demandée par défaut. Le clipping protège la boîte du texte, il ne remplace pas la mesure de sa hauteur.

Dans une `Row`, la largeur des autres enfants, les marges et le gap sont pris en compte avant de remesurer le texte. Les enfants à largeur automatique peuvent rétrécir ; une largeur explicite reste fixe par défaut. `FlexShrink(0)` désactive ce rétrécissement, un facteur positif le permet. `FlexGrow(1)` reste le moyen de réserver au texte l'espace restant. Les listes horizontales scrollables ne sont pas rétrécies à leur viewport.

La taille, le texte, la largeur et les options invalident le layout sur le cold path. Les frames suivantes lisent les ressources mises en cache. Les allocations de shaping/composition sont autorisées ; l'exigence zéro allocation s'applique aux frames stabilisées, pas au changement du texte ou de viewport.

## Paramètres typés de navigation

```csharp
public sealed record ArticleDetailsArgs(Article Article);

public sealed class ArticleDetailsPage : XenoPage<ArticleDetailsArgs>
{
    public override void OnNavigatedTo(ArticleDetailsArgs arguments)
    {
        // Préparer le modèle sur le cold path, avant Body().
    }

    public override XenoComponent Body() => Text(Arguments.Article.Title);
}

// Page<T>() reste inchangé et crée une instance persistante.
Navigate<ArticleDetailsPage, ArticleDetailsArgs>(
    new(article), NavTransitions.SlideHorizontal(280));
```

Le type de page et celui des paramètres sont contrôlés à la compilation. Chaque entrée de pile conserve son payload ; Retour et restauration d'une destination appliquent les paramètres de l'entrée retrouvée. `OnNavigatedTo` est un callback de préparation, pas un callback exécuté par frame ni une promesse que la transition sera commitée. Une page persistante peut recevoir plusieurs payloads au cours de sa vie. Deux entrées de la même page restent deux entrées distinctes, avec activation/désactivation au commit.

Utiliser de préférence des records immuables : le framework conserve le payload, il n'effectue pas de copie profonde d'un objet mutable. Les contrôleurs/modèles persistants restent sous la responsabilité de la page ou de l'application ; cette API ne clone pas l'intégralité de leur état pour chaque entrée. Les APIs `Navigate<T>()`, `GoBack()` et les transitions existantes restent disponibles.

Le SampleApp utilise ce contrat pour transmettre l'article au détail ; les commentaires sont toujours chargés par le service .NET portable, pas par le router.

## Espace réservé à la BottomBar

Le Scaffold mesure la barre standard ou personnalisée, marges comprises, puis réduit la hauteur disponible aux pages. Les conteneurs scrollables et lazy utilisent cette hauteur, permettant d'atteindre le dernier élément sans qu'il soit masqué par la barre. L'ouverture du drawer conserve cet espace ; le drawer reste un overlay. Le masquage de la barre par l'IME retire sa réservation et n'ajoute pas deux fois la hauteur du clavier.

Pour un contenu volontairement derrière la barre :

```csharp
new NavigationScaffoldState(/* destinations */)
{
    ContentOverlaysNavigationBar = true
};
```

Dans ce mode, le développeur assume le padding et l'accessibilité du contenu sous la barre. Le FAB reste un overlay indépendant.

## Validation automatisée

Les régressions ciblées couvrent wrap, newline, largeur réelle des Rows, limites de lignes, taille minimale, reflow après resize, zéro allocation sur frames multiligne stabilisées, hauteur d'une barre personnalisée avec marges, ouverture du drawer et restauration de payloads sur une page persistante.
