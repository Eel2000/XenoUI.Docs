# Benchmark sur Samsung Galaxy A15 — 5 octobre 2026

[Index](index.md) · [Runtime](architecture/runtime.md) · [Comparaison PC](runtime-benchmark.md)

Ces mesures ont été exécutées **sur le téléphone**, pas extrapolées depuis le PC. Elles constituent la référence mobile sur `codex/runtime-critical-stabilization` (correctifs et premières mesures commités dans `5f50e38`). Les sections 1 et 2 conservent la baseline initiale ; la section 6 donne la nouvelle série après correction des allocations.

## Conditions

- Samsung SM-A155F, Android 16/API 36, ARM64, 8 processeurs logiques visibles à .NET.
- .NET 10.0.12, APK de benchmark Release, sans AOT ni trimming, sans debugger attaché. Ce n’est pas une mesure d’un packaging de production AOT/trimmé.
- Écran physique 1080 × 2340, densité 2,8125 ; mode observé 90 Hz (modes disponibles : 60/90 Hz, pas 120 Hz).
- Téléphone connecté et en charge USB. État thermique Android 1 avant les mesures, 2 après ; température AP relevée après le run : 44,8 °C. Les résultats ne décrivent pas un téléphone refroidi en laboratoire.
- Aucun réglage thermique, CPU ou fréquence d’affichage n’a été forcé.

## 1. Scénarios CPU identiques au PC

Skia bitmap CPU 360 × 800, densité 1, exécuté sur un worker du téléphone. Trois séries de 500 opérations après 200 opérations de warm-up par série.

| Scénario | Moyenne ms/op | p50 | p95 | p99 | Maximum | Octets managés/op |
|---|---:|---:|---:|---:|---:|---:|
| Rendu stable, 100 textes | 0,400 | 0,388 | 0,462 | 0,507 | 1,008 | 0 |
| Navigation à mi-parcours, deux scènes de 100 textes | 2,775 | 2,549 | 3,418 | 3,936 | 6,170 | 0 |
| Lazy 10 000 items, fenêtre immobile | 0,114 | 0,105 | 0,135 | 0,384 | 0,688 | 0 |
| Scroll progressif, 16 dp/op | 0,203 | 0,149 | 0,319 | 0,886 | 2,109 | 146,67 |
| Grand saut vers une fenêtre disjointe | 0,738 | 0,674 | 0,924 | 1,041 | 12,392 | 11 365,94 |
| 1 000 évaluations de transition compilée | 0,283 | 0,282 | 0,292 | 0,299 | 0,330 | 0 |

Les frames stables n’ont construit aucun item/scène pendant les mesures. Le scroll progressif a construit 500 items sur 1 500 opérations, les sauts 76 466. Les sauts ont déclenché **3 collections Gen0**, aucune Gen1/Gen2 ; les autres scénarios n’ont enregistré aucune collection dans leur fenêtre de mesure. Le maximum de 12,392 ms n’est pas attribué à un GC spécifique sans trace corrélée.

La simulation numérique d’une seconde de scroll à pas 60/120 Hz reproduit un écart de 0,001953125 dp sur l’A15. Il s’agit d’une simulation : l’écran de cet appareil ne propose pas de mode 120 Hz.

## 2. Frames sur la surface OpenGL réelle

Rendu sur le thread GL du téléphone, surface 1080 × 2340 à densité 2,8125. Par scénario : 120 frames de warm-up puis 300 frames mesurées, une seule série. Les chiffres ci-dessous sont le temps CPU autour de `XenoEngine.Refresh`, pas un timer GPU.

| Scénario | Moyenne CPU ms | p50 | p95 | p99 | Maximum | Octets managés/Refresh |
|---|---:|---:|---:|---:|---:|---:|
| Rendu stable, 100 textes | 1,174 | 1,013 | 2,177 | 2,695 | 3,336 | 0 |
| Navigation à mi-parcours, deux scènes de 100 textes | 6,484 | 6,411 | 8,351 | 8,760 | 9,326 | 0 |
| Lazy 10 000 items, fenêtre immobile | 0,447 | 0,377 | 0,829 | 0,930 | 1,038 | 0 |
| Lazy progressif, 16 dp/frame | 0,636 | 0,460 | 1,369 | 1,622 | 2,417 | 146,67 |

Aucune construction dans les scénarios stables ; 100 items créés sur 300 frames de scroll progressif. Aucune collection GC dans ces quatre fenêtres courtes.

| Scénario | Intervalle moyen entre callbacks GL | p95 | Maximum |
|---|---:|---:|---:|
| Stable | 11,108 ms | 13,517 ms | 14,860 ms |
| Navigation | 11,108 ms | 13,597 ms | 15,228 ms |
| Lazy immobile | 11,097 ms | 13,573 ms | 15,021 ms |
| Lazy progressif | 11,092 ms | 13,690 ms | 15,356 ms |

Les intervalles sont cohérents avec une cadence de callbacks proche de 90 Hz, mais **ne sont pas des timestamps de présentation SurfaceFlinger**. Ils ne prouvent ni 90 FPS affichés sans jank ni un coût GPU inférieur au budget. À 90 Hz, le budget théorique est 11,11 ms ; une transition à 8,35 ms CPU au p95 laisse peu de marge pour les autres coûts.

## 3. Démarrage de la SampleApp

La série de mesures utilise `am start -W` : arrêt du processus avant chaque lancement froid, retour Home puis reprise pour le mode hot. `TotalTime` est la mesure de lancement Android ; `WaitTime` est conservé séparément dans les données brutes.

Cette mesure ne remplace pas `reportFullyDrawn`/TTFD et n’instrumente pas le premier buffer Skia présenté. [Définitions officielles Android](https://developer.android.com/topic/performance/vitals/launch-time).

SampleApp installée en Release, sans AOT ni trimming, sans debugger. Dix lancements, tous confirmés `COLD` par Android :

| Mesure | Moyenne | p50 | p95 | Minimum | Maximum |
|---|---:|---:|---:|---:|---:|
| Lancement froid `TotalTime`, 10 essais | 1 268,3 ms | 1 259 ms | 1 339 ms | 1 237 ms | 1 339 ms |

Échantillons dans l'ordre : 1339, 1260, 1241, 1254, 1276, 1259, 1274, 1237, 1258, 1285 ms. Les percentiles utilisent le rang supérieur (`ceil(n × p)`), sans interpolation ; avec seulement dix essais, le p95 est le maximum observé.

Les dix reprises depuis Home ont ramené la tâche au premier plan, avec `LaunchState: UNKNOWN (0)` et **sans champ `TotalTime`**. Leur temps de démarrage hot est donc **indisponible**, pas zéro. `WaitTime` ne sert pas de substitut. Le collecteur conserve ces valeurs manquantes à `null` et les exclut des statistiques.

Le banc précédent a été arrêté avant cette série. Le téléphone restait en charge et chaud : état thermique Android 2, AP 52,0 °C et peau 43,2 °C relevés pendant la série. Ces chiffres sont une référence dans ces conditions, pas un démarrage à appareil refroidi ni une validation d'une version AOT de production.

## 4. Mémoire après le benchmark

Instantané `dumpsys meminfo` du processus de benchmark après les scénarios :

- PSS total : 199 932 Ko Android (environ 195,2 Mio).
- RSS total : 281 563 Ko (environ 275,0 Mio).
- PSS Native Heap : 41 033 Ko ; taille du heap : 48 820 Ko, allocation : 46 590 Ko.
- Graphics : 57 131 Ko, dont EGL mtrack 28 775 Ko et GL mtrack 28 356 Ko.
- Swap PSS total : 451 Ko.

Cet instantané inclut runtime, JIT, surfaces, caches et ressources du banc. Ce n’est ni la mémoire de la SampleApp, ni le high-water mark, ni une preuve d’absence de fuite. Les octets/op des tableaux mesurent uniquement les allocations managées du thread exécutant `Refresh`.

## 5. Reproduction

Les bancs de mesure (`XenoUI.Android.Benchmarks` et le script de démarrage) font partie des sources du framework et ne sont pas distribués. Le banc écrit `runtime-benchmark.json` et `gl-benchmark.json` sur l’appareil, dans `/sdcard/Android/data/com.xenoui.benchmarks/files/` ; les démarrages sont mesurés avec `am start -W` comme décrit au § 3.

## 6. Correction des allocations — nouvelle série A15

Deux allocations récurrentes du moteur ont été supprimées : journal de rollback Lazy réutilisé avec sa capacité conservée, et police de mesure réutilisée par engine. La taille de police reste exacte ; le typeface est explicitement remis à jour pour ne pas contaminer le mesurage du texte suivant. Le rollback des builders défaillants reste testé.

Même appareil, packaging Release JIT, dimensions et protocoles de mesure. Téléphone toujours en charge : état thermique 2 avant, 3 après la série ; relevés finaux AP 47,3 °C et SKIN 43,7 °C. La variation thermique interdit d'attribuer toute différence de temps au correctif seul. Les compteurs d'allocation ne sont pas remplacés par une comparaison de FPS.

| Scénario | Avant B/op | Après B/op | p95 CPU après | GC Gen0 après |
|---|---:|---:|---:|---:|
| CPU, scroll progressif avec nouveaux textes | 146,67 | 80 | 0,230 ms | 0 |
| CPU, sauts aléatoires avec nouveaux textes | 11 365,94 | 4 880,30 | 0,578 ms | 0 |
| GL, scroll progressif avec nouveaux textes | 146,67 | 80 | 0,920 ms | 0 |
| CPU, scroll progressif avec libellé constant préparé | non mesuré | 0 | 0,238 ms | 0 |
| CPU, sauts aléatoires avec libellé constant préparé | non mesuré | 0,73 | 0,474 ms | 0 |
| GL, scroll progressif avec libellé constant préparé | non mesuré | 0 | 0,924 ms | 0 |
| GL, sauts alternés avec libellé constant préparé | non mesuré | 0 | 1,836 ms | 0 |

Le scénario initial reste inchangé : formatage et concaténation du texte dans le builder, puis premiers rendus de textes distincts avec cache Skia borné. Ses allocations baissent de 45,5 % en scroll progressif et de 57,1 % pour les sauts CPU, mais ne disparaissent pas. Les chaînes du builder et les créations de blobs sur cache miss restent des chemins allouants ; cette comparaison n'est pas un profiling attribuant chaque octet à une pile d'appel.

Le diagnostic préparé utilise un seul libellé constant, déjà rendu pendant le warm-up, sans précharger les 10 000 items ni agrandir le cache de 512 blobs. Il isole la matérialisation : 100 items construits sur 300 frames GL progressives, 15 900 sur 300 sauts alternés entre les indices 1000/5000, avec zéro octet managé mesuré. Le résidu de 0,73 B/op des sauts **aléatoires CPU** n'est pas expliqué par une trace d'allocation : il reste ouvert, et ne doit pas être arrondi à zéro.

Sur la nouvelle série GL : rendu stable p95 1,626 ms, navigation p95 8,602 ms, Lazy immobile p95 0,731 ms ; zéro octet managé dans ces trois scénarios. Aucune collection Gen0/1/2 n'a été observée dans les fenêtres mesurées de cette série ; cela ne prouve pas l'absence de GC lors d'une session soutenue.

La suite Core passe **100/100** tests Release, avec nouveaux tests de mesure de texte répétée et de matérialisation de fenêtres disjointes à libellé constant. Builds Android du benchmark et de SampleApp : zéro warning, zéro erreur. Un premier packaging incrémental du benchmark a échoué en JNI avant d'exécuter le moteur ; une reconstruction complète a permis le run retenu. Aucun chiffre de cette tentative échouée n'est retenu.

json`. La baseline précédente n'est pas écrasée.

SampleApp corrigée reconstruite intégralement et réinstallée sur l'A15 : Home → Runtime Lab, liste verticale passée aux batches 6–9 et liste horizontale aux chips 6–9, affichage vérifié par captures. Le PID testé est resté vivant sans erreur AndroidRuntime dans son log filtré. Son premier package incrémental avait aussi échoué en JNI avant `OnCreate` ; le run fonctionnel utilise le package reconstruit, pas ce package défaillant. Ce smoke test n'est pas une mesure d'allocation des items complexes de SampleApp.

Les prochains correctifs exigent un profiling attribué des misses Skia, du résidu des sauts aléatoires, des arbres d'items complexes (collections enfants, callbacks, abonnements) et une politique de recyclage bornée. Le contrat zéro-allocation ne peut pas inclure arbitrairement un builder développeur créant des chaînes ou closures à chaque entrée d'item. Les allocations natives restent à mesurer séparément.

## 7. Collections GC soutenues sur la surface GL A15

Le mode dédié `gc-stress` exécute directement le banc GL, sans passer par le benchmark CPU. Il ne contient aucun appel à `GC.Collect()` ni attente de finalizers. Le runtime Core/Android/iOS n'en contient pas non plus ; le benchmark CPU comparatif en utilise avant chaque fenêtre, hors compteurs publiés.

Par scénario : 120 frames de warm-up puis 2 700 frames mesurées, trois scénarios successifs dans le même processus. Les compteurs `GC.CollectionCount(0/1/2)` sont capturés immédiatement après la dernière frame, **avant** le tri, la création des statistiques et la sérialisation. La construction, le warm-up, la destruction et les statistiques sont hors de chaque fenêtre. Aucune collection forcée entre scénarios ; le remplissage du heap précédent peut donc influencer le suivant.

Les allocations `Refresh` sont comptées sur le thread GL. Le delta `GC.GetTotalAllocatedBytes(false)` concerne approximativement **tout le processus**, y compris callbacks natifs/SkiaSharp et autres threads ; il n'est pas une mesure de mémoire retenue. Les compteurs GC concernent aussi le processus, pas seulement `Refresh`. Les colonnes par génération ne doivent pas être additionnées comme un nombre de pauses indépendantes.

Ce test compte les collections, mais ne mesure pas leur durée de pause et ne distingue pas précisément leur origine. Le temps CPU d'une frame ou son maximum n'est pas une durée de GC. ART/Java, allocations natives et présentation SurfaceFlinger ne sont pas couverts par les compteurs .NET.

Résultats A15, Release JIT, surface 1080 × 2340, densité 2,8125 :

| Scénario | Fenêtre | Items construits | B/Refresh | Allocation Refresh totale | Gen0 | Gen1 |
|---|---:|---:|---:|---:|---:|---:|
| Scroll progressif, nouveaux textes | 29,963 s | 900 | 80,01 | 216 040 B | 0 | 0 |
| Sauts aléatoires, nouveaux textes | 29,970 s | 143 062 | 5 040,11 | 13 608 288 B | **3** | 0 |
| Sauts alternés, libellé constant préparé | 29,963 s | 143 100 | 0 | 0 B | 0 | 0 |

`GC.MaxGeneration = 1` sur ce runtime Mono : Gen1 est la génération maximale exposée. La valeur Gen2 conservée dans le JSON n'est pas une troisième génération supportée et n'est pas utilisée pour la conclusion. Les trois collections Gen0 des sauts correspondent à environ 6 collections/minute **sur cette fenêtre**, pas une prédiction de fréquence sur toute session.

| Scénario | CPU Refresh p95 | p99 | Maximum |
|---|---:|---:|---:|
| Scroll progressif | 1,173 ms | 1,573 ms | 3,903 ms |
| Sauts aléatoires, nouveaux textes | 1,942 ms | 2,489 ms | 26,749 ms |
| Sauts alternés, libellé préparé | 2,025 ms | 2,344 ms | 4,968 ms |

Le maximum de 26,749 ms et l'intervalle maximal entre callbacks de 31,448 ms dans le scénario allouant sont préoccupants, mais aucune trace ne permet de les attribuer à un GC particulier. Ce n'est pas une mesure de durée de pause GC ni de frame effectivement présentée.

Le compteur global approximatif renvoie 0 pour le scroll malgré 216 040 B comptés sur le thread, et 13 255 208 B pour les sauts. Il est donc insuffisamment précis sur ces fenêtres pour quantifier les allocations hors `Refresh` ; ses zéros ne signifient pas absence d'allocation du processus. Il reste un diagnostic brut, pas une métrique d'acceptation.

État thermique Android 3 avant/pendant, 2 après ; relevés finaux AP 45,5 °C et SKIN 41,9 °C, toujours en charge USB. Une seule série, appareil chaud. Le banc a été arrêté après récupération du rapport, puis SampleApp remise au premier plan. Aucune erreur AndroidRuntime du PID du banc dans le log consulté.

## Conclusion

Le zéro-allocation est confirmé sur les frames stabilisées **testées**, et après correction sur la matérialisation GL à libellé constant préparé. Le scénario avec nouveaux textes reste allouant ; les grands sauts avaient provoqué trois GC Gen0 dans la baseline, aucun dans la nouvelle fenêtre courte. Le résidu des sauts aléatoires, les traces de présentation/jank, TTFD, mémoire native soutenue et packaging de production restent ouverts.

Le test soutenu de la section 7 montre toutefois **trois GC Gen0 sur 2 700 grands sauts GL à nouveaux textes** : l'absence de GC dans une fenêtre courte ne valide pas l'objectif zéro pression GC pendant toute l'interaction.
