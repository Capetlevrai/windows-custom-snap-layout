# CustomFancyZone

Quand la souris reste sur le bouton **Agrandir** (□) d'une fenêtre, un menu de dispositions s'ouvre, comme
celui de Windows 11, mais avec **tes propres dispositions**. Un clic sur une zone y place la fenêtre, puis,
comme dans Windows, les autres fenêtres ouvertes sont proposées pour remplir les zones restées vides.

![Le menu de CustomFancyZone](docs/menu.png)

- 8 dispositions par défaut : **50/50, 55/45, 60/40**, puis **2/3 | 1/3, 68/32, 74/26**, puis **80/20** et
  **quatre quarts**.
- **Assistance d'ancrage** comme Windows : miniatures en direct des autres fenêtres dans la zone vide, un clic
  pour la remplir. Marche avec n'importe quelles applications (Chrome, Bloc-notes, Explorateur…).
- **Zoom** des applications depuis le menu (case − · ↺ · +), ou automatique selon la zone.
- Tes dispositions se décrivent en quelques lettres dans `layouts.json`, relu automatiquement à chaque enregistrement.
- Aperçu bleu sur l'écran de la zone survolée, avant de cliquer.
- Suit le thème clair/sombre et la couleur d'accentuation de Windows.
- Multi-écrans et mises à l'échelle différentes (testé : écran 4K à 200 % + écran 1080p à 100 %).
- Aucun pilote, aucun service, aucun droit administrateur, 0 % de CPU au repos.

---

## Installation

Windows 10 (1809+) ou Windows 11, 64 bits. Rien d'autre à installer : .NET est inclus.

👉 **[Télécharger la dernière version](https://github.com/Capetlevrai/windows-custom-snap-layout/releases/latest)**

- **`CustomFancyZone-Setup-x.y.z.exe`** (recommandé) : installe l'outil pour ton compte, sans droits
  administrateur, dans `%LOCALAPPDATA%\Programs\CustomFancyZone`, avec un raccourci dans le menu Démarrer et
  l'option « Lancer au démarrage de Windows ». Désinstallable depuis *Paramètres > Applications*.
- **`CustomFancyZone.exe`** (portable) : à poser dans le dossier de ton choix et à lancer ; `layouts.json` est
  créé à côté.

Windows SmartScreen peut afficher « Windows a protégé votre ordinateur » parce que l'exe n'est pas signé :
*Informations complémentaires > Exécuter quand même*.

## Démarrage rapide

1. **Lancer** CustomFancyZone (menu Démarrer, ou l'exe portable). Une icône apparaît dans la zone de
   notification (à côté de l'horloge, parfois cachée derrière la flèche ^).
2. **Couper le menu natif de Windows**, sinon les deux menus se superposent :
   *Paramètres > Système > Multitâche > Ancrer les fenêtres* (déplier), puis décocher
   « Afficher les dispositions d'ancrage lorsque je survole le bouton permettant de développer une fenêtre ».
   Au démarrage, l'outil affiche une notification si l'option est encore cochée ; un clic dessus ouvre la bonne page.
3. **Survoler** le bouton Agrandir d'une fenêtre pendant 0,4 s : le menu s'ouvre. **Cliquer** une zone.
4. **Choisir** dans la zone vide la fenêtre qui va à côté (ou Échap pour s'arrêter là).
5. **Lancement automatique** : clic droit sur l'icône > « Lancer avec Windows ».

## Utilisation

| Geste | Effet |
|---|---|
| Souris immobile 0,4 s sur □ | Ouvre le menu sous le bouton (au-dessus s'il n'y a pas la place). |
| Survol d'une zone | La zone passe en couleur d'accent ; un rectangle translucide montre où la fenêtre va aller ; le nom de la disposition s'affiche en bas. |
| Clic sur une zone | La fenêtre est placée exactement dans la zone. Une fenêtre agrandie est d'abord restaurée. |
| Assistance d'ancrage | Juste après, chaque zone restée vide affiche les miniatures des autres fenêtres ouvertes (les plus récentes d'abord, 12 au maximum). Un clic place la fenêtre choisie, puis on passe à la zone vide suivante. Échap ou un clic ailleurs arrête. |
| Case « Zoom » (− · ↺ · +), à droite des quatre quarts | Zoome la fenêtre survolée, comme Ctrl − / Ctrl 0 / Ctrl +. Le menu reste ouvert pour enchaîner les crans. |
| Clic sur □ lui-même | Agrandit normalement la fenêtre et ferme le menu. |
| Souris hors du menu (0,35 s) ou clic ailleurs | Ferme le menu. |

Exemple : survoler □ de Chrome, cliquer la zone de gauche de « 55 / 45 », puis cliquer la miniature de Claude
(ou du Bloc-notes, ou de n'importe quelle autre fenêtre) dans la partie droite.

Les zones sont calculées sur **l'écran où se trouve le bouton**, sans la barre des tâches. Le menu ne prend
jamais le focus : la fenêtre à placer reste active.

## Menu de l'icône (clic droit)

| Entrée | Rôle |
|---|---|
| Modifier les dispositions… | Ouvre `layouts.json` dans l'éditeur par défaut (double-clic sur l'icône : pareil). |
| Ouvrir le dossier | Ouvre le dossier de l'outil (celui de l'exe). |
| Recharger | Relit `layouts.json` (normalement inutile : c'est automatique). |
| Menu natif de Windows : actif/désactivé | Indique l'état de l'option Windows et ouvre la page de Paramètres. |
| Lancer avec Windows | Ajoute/retire l'outil du démarrage (clé `HKCU\…\Run`). |
| Pause | Suspend l'outil sans le quitter. |
| Quitter | Ferme l'outil. |

---

## Personnaliser les dispositions

Le fichier est `layouts.json`, à côté de l'exe (`%LOCALAPPDATA%\Programs\CustomFancyZone\` avec l'installeur ;
clic droit sur l'icône > « Modifier les dispositions… » l'ouvre directement). Il accepte les commentaires `//`. Chaque enregistrement est pris en compte
tout de suite ; en cas d'erreur, une notification l'explique et la configuration précédente reste active.
Pour revenir aux réglages d'origine, supprimer le fichier : il est recréé au lancement suivant.

### Options

| Option | Défaut | Rôle |
|---|---|---|
| `hoverDelayMs` | `400` | Temps de survol du bouton avant l'ouverture du menu (ms). |
| `hideDelayMs` | `350` | Temps avant fermeture quand la souris quitte le menu (ms). |
| `columns` | `3` | Vignettes par ligne (3, comme Windows). |
| `tileWidth` | `96` | Largeur d'une vignette en pixels à 100 % (multipliée par la mise à l'échelle de l'écran). |
| `zoneGap` | `0` | Espace entre les fenêtres placées et autour d'elles (pixels à 100 %). `8` donne un rendu « carrelage » aéré. |
| `showPreview` | `true` | Rectangle translucide sur l'écran pendant le survol d'une zone. |
| `showNames` | `true` | Nom de la disposition en bas du menu. |
| `snapAssist` | `true` | Assistance d'ancrage après le placement (`false` : on place seulement la fenêtre survolée). |
| `showZoomBar` | `true` | Case « Zoom » dans la première case libre après les dispositions. |
| `customButtons` | Unreal | Applications qui dessinent leur bouton Agrandir sans le déclarer à Windows : classe de fenêtre (`windowClass`) et position du bouton en pixels à 100 % depuis le coin haut-droit (`right`, `top`, `width`, `height`). Unreal Engine (`UnrealWindow`, 2ᵉ bouton de 42 px en partant de la droite) est inclus. |
| `debugLog` | `false` | Journal détaillé dans `CustomFancyZone.log` (à côté de l'exe). |
| `layouts` | 8 dispositions | La liste affichée dans le menu, dans cet ordre. |

### Les dispositions d'origine

| Ligne | Dispositions |
|---|---|
| 1 | 50 / 50 · 55 / 45 · 60 / 40 |
| 2 | 2/3 \| 1/3 · 68 / 32 · 74 / 26 |
| 3 | 80 / 20 · quatre quarts (2 × 2) |

### Décrire une disposition avec `grid` (le plus simple)

On dessine l'écran avec des lettres : **une lettre = une zone**, toutes les colonnes ont la même largeur
et toutes les lignes la même hauteur. Une zone doit former un rectangle. `.` laisse une case vide.

```jsonc
{ "name": "50 / 50",               "grid": ["AB"] }              // A | B
{ "name": "Deux tiers | un tiers", "grid": ["AAB"] }             // A prend 2 colonnes sur 3
{ "name": "75 / 25",               "grid": ["AAAB"] }
{ "name": "Quatre quarts",         "grid": ["AB",
                                            "CD"] }
{ "name": "Moitié | deux empilées", "grid": ["AB",
                                             "AC"] }             // A sur toute la hauteur
```

Plus il y a de caractères par ligne, plus le découpage est fin : `"AAAAAAABBB"` fait du 70/30.

### Décrire une disposition avec `zones` (au pourcentage près)

Chaque zone est `[x, y, largeur, hauteur]` en **% de l'écran** (hors barre des tâches). Les zones peuvent
laisser des vides (la vignette montre alors le fond de l'écran en gris clair).

```jsonc
{ "name": "55 / 45",        "zones": [[0, 0, 55, 100], [55, 0, 45, 100]] }
{ "name": "Centrée (60 %)", "zones": [[20, 5, 60, 90]] }
{ "name": "Barre latérale", "zones": [[0, 0, 20, 100], [20, 0, 80, 100]] }
```

L'ordre des zones (« zone 2/4 » en bas du menu, et ordre de remplissage par l'assistance) suit l'ordre
d'écriture : ordre de lecture des lettres pour `grid`, ordre de la liste pour `zones`.

### Zoom automatique par zone avec `zoom`

`zoom` donne, dans l'ordre des zones, le zoom en % de la fenêtre qu'on y place (par le menu ou par
l'assistance), ou `null` pour ne pas y toucher. Pratique pour qu'une appli mise dans une zone étroite passe
d'elle-même en plus petit :

```jsonc
{ "name": "74 / 26", "zones": [[0, 0, 74, 100], [74, 0, 26, 100]], "zoom": [null, 80] }
```

## Fenêtres qui refusent de rétrécir

Certaines applications imposent une largeur minimale. Exemple : **l'appli Claude ne descend pas sous 1202 px
sur un écran 4K à 200 %** (environ 600 px à 100 %), soit 31,3 % de la largeur. Dans une zone plus étroite (26 %
du 74/26, 20 % du 80/20), elle déborderait de l'écran et ses boutons Réduire / Agrandir / Fermer disparaîtraient.
Le zoom de l'application n'y change rien : cette largeur minimale ne dépend pas du zoom.

L'outil corrige ça tout seul : si une fenêtre placée est plus large (ou plus haute) que sa zone, elle est
recalée contre le bord de l'écran, et les fenêtres et zones voisines de la même disposition sont réduites
d'autant. Rien n'est coupé, et la voisine garde **tout l'espace restant**. Avec Chrome + Claude, le 74/26
comme le 80/20 donnent donc Chrome sur 2638 px (68,7 %) et Claude à sa largeur minimale. Le journal note
chaque ajustement (« Taille minimale : … »).

Testé avec une fenêtre bloquée à 636 px de large, placée dans le 26 % d'un écran 1080p (499 px) : elle s'est
calée contre le bord droit (636 px) et la fenêtre voisine a été réduite jusqu'à elle.

## Le zoom des applications

Le zoom propre à une application (pas la mise à l'échelle de Windows) se règle dans l'application :
**Ctrl −** / **Ctrl +** / **Ctrl 0**, ou **Ctrl + molette**. Dans Claude, Chrome, Edge, VS Code et les autres
applications Chromium/Electron, les paliers sont 25, 33, 50, 67, 75, 80, 90, **100**, 110, 125, 150… % ;
on peut donc descendre sous 100 %.

CustomFancyZone envoie exactement ces raccourcis à la fenêtre (pavé numérique pour − et +, indépendant de la
disposition du clavier) :

- la case **Zoom** du menu envoie un cran à chaque clic ;
- l'option **`zoom`** fait Ctrl 0 (retour à 100 %) puis autant de crans qu'il faut pour atteindre le palier
  Chrome le plus proche de la valeur demandée.

À savoir :
- l'outil ne peut pas lire le zoom actuel d'une application : il envoie seulement les raccourcis ;
- il donne d'abord le focus à la fenêtre et **n'envoie rien** s'il n'y parvient pas, pour ne jamais zoomer une
  autre application par erreur ;
- dans Chrome, le zoom est retenu **par site** (zoomer x.com ne change pas les autres sites) ;
- les paliers varient selon l'application : le Bloc-notes avance de 10 % en 10 % (testé : deux crans → 80 %,
  Ctrl 0 → 100 %) ; une application sans zoom clavier ignore simplement les raccourcis.

---

## Compiler et mettre à jour

Prérequis : **.NET SDK 10** ; **Inno Setup 6** pour l'installeur (`winget install JRSoftware.InnoSetup`).

```powershell
.\build.ps1        # version de développement dans app\ (utilise le runtime .NET installé)
.\build.ps1 -Run   # idem, puis lance
.\release.ps1      # fichiers à distribuer dans dist\ : exe autonome + installeur
```

`build.ps1` ferme proprement l'instance en cours avant de remplacer l'exe et ne touche jamais à
`app\layouts.json`. `release.ps1` produit `dist\portable\CustomFancyZone.exe` (.NET inclus, un seul fichier)
et `dist\CustomFancyZone-Setup-<version>.exe` ; la version vient de `src\CustomFancyZone.csproj`.

## Ligne de commande (diagnostic)

Depuis PowerShell, ajouter `| Out-String` pour voir la sortie (c'est une application fenêtrée).

| Commande | Rôle |
|---|---|
| `CustomFancyZone.exe --check` | Valide `layouts.json` et liste les dispositions avec leurs zones en %. |
| `CustomFancyZone.exe --hittest X Y` | Ce que Windows trouve sous un point de l'écran (pixels physiques). `hit 9` = bouton Agrandir. |
| `CustomFancyZone.exe --place HWND DISPO ZONE` | Place une fenêtre sans passer par le menu (index à partir de 0). |
| `CustomFancyZone.exe --assist HWND DISPO ZONE [HWND…]` | Place une fenêtre puis lance l'assistance ; les HWND suivants limitent les fenêtres proposées. |
| `CustomFancyZone.exe --zoom HWND +\|-\|0\|POURCENT` | Zoome une fenêtre (elle doit pouvoir prendre le focus). |
| `CustomFancyZone.exe --demo [SECONDES] [DISPO ZONE]` | Affiche le menu au centre de l'écran (pour une capture). |
| `CustomFancyZone.exe --quit` | Ferme l'instance en cours. |

---

## Comment ça marche

1. **Détection du bouton.** Toutes les 40 ms, l'outil lit la position de la souris. Si elle est dans le haut
   d'une fenêtre, il pose à cette fenêtre la question que Windows lui pose lui-même pour décider d'afficher
   son menu : le message `WM_NCHITTEST` (« qu'y a-t-il sous ce point ? »). La réponse `HTMAXBUTTON` (9)
   signifie « bouton Agrandir ». Cela marche donc aussi avec les applications qui dessinent leur propre barre
   de titre (Chrome, Electron, le Bloc-notes de Windows 11…), dès lors qu'elles prennent en charge le menu
   natif. Le point est converti dans le système de coordonnées de la fenêtre pour les vieilles applications
   non adaptées aux écrans haute densité.
2. **Le menu** est une fenêtre « sans activation » (`WS_EX_NOACTIVATE`) : cliquer dessus ne retire pas le
   focus à la fenêtre visée. Ses coins arrondis sont ceux de Windows 11 (DWM).
3. **Le placement** compense les bordures invisibles (7 px à 100 %) que Windows 11 ajoute autour des fenêtres
   pour les redimensionner (`DWMWA_EXTENDED_FRAME_BOUNDS`), en deux passes au cas où la fenêtre change d'écran
   et donc de mise à l'échelle.
4. **L'assistance d'ancrage** liste les fenêtres comme Alt+Tab (visibles, redimensionnables, sans propriétaire,
   pas sur un autre bureau virtuel), de la plus récente à la plus ancienne, et affiche leurs miniatures en
   direct avec l'API de miniatures de DWM (celle des aperçus de la barre des tâches).

**Pourquoi l'outil ne coupe pas lui-même le menu natif ?** L'option est stockée dans
`HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\EnableSnapAssistFlyout`, mais l'Explorateur
ne la relit pas quand on écrit dans le registre. Testé sans succès : `WM_SETTINGCHANGE` (avec `TraySettings`,
`ShellState`, `Policy`, le chemin de la clé, `SPI_SETWINARRANGING`) et `SystemParametersInfo`. Seule l'appli
Paramètres l'applique sans redémarrer l'Explorateur, par un canal interne. D'où la case à décocher une fois.

## Limites connues

- **Fenêtres lancées en administrateur** : Windows interdit à un programme normal de les interroger et de les
  déplacer, donc pas de menu sur elles. Pour les gérer, lancer `CustomFancyZone.exe` en administrateur.
- Pas de groupes d'ancrage (retrouver un ensemble de fenêtres ancrées depuis la barre des tâches).
- L'assistance se pilote à la souris (Échap pour quitter) ; pas de navigation aux flèches.
- Une application dont le bouton Agrandir ne répond pas `HTMAXBUTTON` n'a pas de menu, comme avec le menu natif,
  sauf si elle est décrite dans `customButtons` (c'est le cas d'Unreal Engine, dont le bouton est dessiné par Slate).
  Les infobulles qui recouvrent le bouton (le « Maximize » d'Unreal) sont ignorées.
- Une application sans aucun bouton de fenêtre (Equibop avec la barre de titre façon Discord) n'offre rien à survoler.
- Une fenêtre à taille minimale (voir « Fenêtres qui refusent de rétrécir ») prend un peu plus que sa zone.
- Si l'outil est quitté alors que le menu natif est décoché, il n'y a plus de menu du tout au survol :
  recocher l'option dans Paramètres pour retrouver celui de Windows.

## Dépannage

| Problème | Piste |
|---|---|
| Rien ne s'ouvre | L'icône est-elle là, l'outil en pause ? Tester `--hittest X Y` sur le bouton : il doit répondre `hit 9`. |
| Deux menus superposés | Le menu natif est encore actif : voir l'étape 2 du démarrage rapide. |
| Une modification de `layouts.json` n'a pas d'effet | Une notification a dû signaler l'erreur ; sinon lancer `--check`. |
| Comportement étrange | Mettre `"debugLog": true` et lire `CustomFancyZone.log` à côté de l'exe (les choix de l'assistance y sont toujours notés). |

## Désinstaller

- **Installé avec le setup** : *Paramètres > Applications > Applications installées > CustomFancyZone >
  Désinstaller*. L'outil est fermé, le lancement automatique retiré et le dossier supprimé.
- **Version portable** : clic droit sur l'icône > décocher « Lancer avec Windows », « Quitter », puis supprimer
  l'exe et `layouts.json`.

Dans les deux cas, recocher ensuite l'option native dans *Paramètres > Système > Multitâche > Ancrer les
fenêtres* pour retrouver le menu de Windows. L'outil n'écrit rien d'autre sur le PC.

## Contenu du dépôt

```
├── README.md                 cette documentation
├── build.ps1                 compilation de développement vers app\
├── release.ps1               exe autonome + installeur vers dist\
├── installer\                script Inno Setup de l'installeur
├── tools\make-icon.ps1       génère src\app.ico
├── docs\menu.png             capture du menu
└── src\                      code source C# (.NET 10, WinForms)
    ├── Program.cs            point d'entrée, instance unique
    ├── TrayApp.cs            icône, détection du survol, ouverture du menu, placement
    ├── FlyoutForm.cs         le menu (dessin, survol, clic, case Zoom)
    ├── SnapAssistForm.cs     l'assistance d'ancrage (miniatures DWM des autres fenêtres)
    ├── PreviewForm.cs        l'aperçu translucide de la zone
    ├── WindowPlacer.cs       calcul des zones et déplacement des fenêtres
    ├── PlacementSession.cs   ajustement quand une fenêtre refuse de rétrécir
    ├── AppWindows.cs         liste des fenêtres proposables et de leurs icônes
    ├── Zoom.cs               zoom des applications par raccourcis clavier
    ├── Config.cs             lecture de layouts.json (grid / zones / zoom / customButtons)
    ├── Theme.cs              couleurs clair/sombre et accentuation
    ├── NativeSnap.cs         état du menu natif de Windows
    ├── Native.cs             appels Win32
    ├── Cli.cs                commandes de diagnostic
    ├── Log.cs, AppIcon.cs, app.ico
    └── layouts.default.json  dispositions d'origine (intégrées à l'exe)
```
