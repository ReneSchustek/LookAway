# Journal des modifications

[Deutsch](CHANGELOG.md) · [English](CHANGELOG.en.md) · **Français**

Toutes les modifications notables de LookAway sont documentées ici.
Le format s'appuie sur [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/),
et la gestion des versions suit [Semantic Versioning](https://semver.org/lang/fr/).

## [Non publié]

## [1.2.7] – 2026-07-11

### Corrigé

- **La mise à jour automatique n'installait pas le paquet.** La mise à jour était vérifiée et
  téléchargée, mais jamais appliquée — l'ancienne version restait installée. Le processus
  auxiliaire qui remplace les fichiers démarre depuis le dossier intermédiaire et en déduisait
  deux choses à tort :
  - **Son répertoire de données :** le paquet contenant un marqueur portable, il se croyait être
    une installation portable et cherchait ses paramètres à côté de lui. Il n'y trouvait pas
    l'empreinte de fichier enregistrée servant à vérifier le paquet — et refusait sa propre mise
    à jour. Il utilise désormais le répertoire de données de l'installation qu'il dessert. De
    plus, le marqueur portable n'est plus copié dans le dossier intermédiaire.
  - **Sa cible :** il utilisait son propre dossier de programme (le dossier intermédiaire) comme
    cible, se copiant ainsi sur lui-même. Il utilise désormais le dossier d'installation transmis
    et le vérifie au préalable (installation existante et accessible en écriture, hors du dossier
    intermédiaire).

## [1.2.6] – 2026-07-10

### Ajouté

- **Démarrage automatique de la pause :** La pause démarre désormais automatiquement après un
  délai configurable (15 secondes par défaut, par pas de 5 secondes jusqu'à 3 minutes) si le
  rappel n'est pas utilisé. Un compte à rebours dans la fenêtre de rappel affiche le temps
  restant. Activable ou désactivable dans les paramètres — désactivé, le rappel reste ouvert
  jusqu'à ce que vous choisissiez une action.

## [1.2.5] – 2026-07-02

### Modifié

- **Indication sur la pause multimédia :** les actions de pause incluent désormais une note
  expliquant quels lecteurs sont mis en pause automatiquement. Seules les applications intégrées
  aux commandes multimédias de Windows (SMTC) peuvent être contrôlées (par ex. Spotify,
  l'application Musique et la lecture dans Chrome, Edge et Firefox). **VLC ne le prend pas en
  charge** et ne sera pas mis en pause.

## [1.2.4] – 2026-07-01

### Modifié

- **Transparence de la superposition supprimée :** le curseur alpha/opacité du sélecteur
  de couleur disparaît, tout comme l'astuce trompeuse (« la transparence de l'écran »).
  La vraie transparence de fenêtre n'est pas réalisable de façon fiable dans WinUI 3 ; la
  superposition recouvre l'écran de manière opaque. Une couleur auparavant
  semi-transparente est automatiquement migrée vers son équivalent opaque visuellement
  identique — l'apparence ne change pas.

## [1.2.3] – 2026-07-01

### Corrigé

- **La superposition de pause s'affiche à nouveau :** avec « assombrir tous les
  écrans » activé (par défaut), la superposition ne s'affichait pas au début d'une
  pause. La cause était une `InvalidCastException` lors de l'énumération de la liste
  des moniteurs issue de `DisplayArea.FindAll()` (une projection WinRT dont la
  requête `IIterable` échoue dans CsWinRT) — sur les versions plus anciennes, cela
  faisait même planter l'application au clic sur « Démarrer la pause ». La liste des
  moniteurs est désormais copiée dans un tableau managé par index ; la superposition
  couvre à nouveau tous les moniteurs. (La protection ajoutée en 1.2.2 intercepte
  toujours une éventuelle erreur au lieu de bloquer l'application.)

### Modifié

- **Contenu de pause uniquement sur le moniteur principal :** avec « assombrir tous
  les écrans », le titre, l'astuce et le compte à rebours n'apparaissent plus que sur
  le moniteur principal ; les autres moniteurs sont simplement assombris
  (superposition vide). Le raccourci ÉCHAP met toujours fin à la pause depuis
  n'importe quel moniteur.
- **Meilleure couleur de texte automatique de la superposition :** la couleur de
  texte contrastée suit désormais la couleur réellement visible de la superposition
  (la couleur semi-transparente composée sur un fond clair, évaluée selon la
  luminosité perçue). Un noir semi-transparent — qui apparaît gris — obtient ainsi un
  texte foncé au lieu d'un texte clair peu lisible.

## [1.2.2] – 2026-07-01

### Corrigé

- **La mise à jour automatique ne reste plus bloquée :** un paquet de mise à jour
  déjà téléchargé et vérifié (signature et empreinte) **n'est plus retéléchargé ni
  réextrait à chaque démarrage**. Auparavant, chaque lancement réécrivait le
  `LookAway.exe` extrait ; un fichier fraîchement écrit et non signé est analysé par
  l'antivirus lors de sa première exécution, ce qui peut brièvement bloquer le
  lancement de l'assistant avec « Accès refusé ». Le paquet restait ainsi
  perpétuellement « froid » et la mise à jour n'était jamais appliquée. Le paquet
  préparé est désormais conservé et appliqué au prochain démarrage.
- **Démarrage de pause plus robuste :** si la construction de la superposition de
  pause ou de la fenêtre de rappel échoue, l'application reste utilisable : l'état
  est réinitialisé proprement, la luminosité et les médias sont restaurés et le
  minuteur continue de tourner — au lieu de rester bloqué dans un état « pause en
  cours ».

## [1.2.1] – 2026-06-30

### Corrigé

- **Le minuteur n'est plus réinitialisé inutilement :** enregistrer des réglages sans
  rapport (langue, son, couleur de l'overlay, fréquence des mises à jour …) ne
  redémarre plus le compte à rebours de travail en cours. Le compte à rebours survit
  également à un redémarrage **au sein de la même session Windows** (p. ex. une mise à
  jour) et reprend là où il s'était arrêté au lieu de repartir de zéro. Un redémarrage
  de Windows (nouvelle session) le réinitialise normalement ; la réinitialisation après
  veille/écran éteint reste inchangée.

### Ajouté

- **Installation en un clic :** lorsque « Vérifier les mises à jour » trouve un paquet,
  un bouton **« Installer maintenant »** est désormais proposé directement. La mise à
  jour est téléchargée, sa signature vérifiée et elle est appliquée au prochain
  démarrage — plus de détour par la page de version GitHub (qui reste visible comme
  solution manuelle).

### Modifié

- Finitions internes de qualité : audit complet des commentaires/principes sur toutes
  les couches, code mort supprimé, trémas corrects et cohérents jusque dans les
  commentaires des fichiers projet.

## [1.2.0] – 2026-06-30

### Ajouté

- **Authenticité des mises à jour (signature de release) :** les paquets sont
  vérifiés contre une signature détachée **ECDSA P-256 / SHA-256** avant toute
  extraction ou application (asset `*.sig` contre la clé publique embarquée,
  fail-closed). Un canal de publication compromis ne peut pas forger de signature
  valide sans la clé privée conservée hors ligne. Outils
  `tools/new-signing-key.ps1` et `tools/sign-release.ps1` ; la CI signe via le
  secret `LOOKAWAY_SIGNING_KEY`.

### Modifié

- **Pureté des couches :** le verrou d'instance unique passe derrière l'interface
  Core `ISingleInstanceLock` et rejoint, avec les adaptateurs Windows
  (`WindowsScreenDimmer`, `WindowsMediaController`), la couche Data ;
  `LookAway.Application` devient neutre vis-à-vis de la plateforme (`net10.0`).
- Les deux dépôts JSON partagent un `JsonFileStore` commun ; les fichiers
  `settings.json`/`history.json` corrompus sont sauvegardés en `*.corrupt` avant
  remplacement.
- Les libellés de raccourcis sont localisés via `ILocalizationService` (Strg/Ctrl,
  Umschalt/Shift/Maj) ; `SettingsViewModel` scindé par préoccupation (raccourcis/mises à jour).

### Corrigé

- Trémas corrects et cohérents (ä/ö/ü/ß) dans les commentaires, textes et noms de tests.

## [1.1.1] – 2026-06-30

### Corrigé

- L'affichage de la licence dans l'application (À propos) indique désormais
  correctement **MIT** au lieu de « Propriétaire ».
- **Sécurité :** une mise à jour en attente est vérifiée par sa version **et le
  SHA-256 de son exécutable** avant d'être appliquée — un dossier simplement déposé
  sous `%LOCALAPPDATA%\…\updates\` n'est plus exécuté. La protection contre les
  bombes zip limite désormais les octets **réellement écrits** au lieu de la taille
  déclarée dans l'archive.
- Plus de double rappel de pause lorsque le minuteur et une action utilisateur se
  déclenchent en même temps (l'affichage s'exécute de façon thread-safe sur le fil UI).
- Robustesse : le minuteur capture son jeton d'annulation localement, la visibilité
  de la superposition est `volatile`, les téléchargements partiels interrompus sont
  nettoyés et les ID d'événements de journal en collision ont été réattribués.

## [1.1.0] – 2026-06-30

### Ajouté

- Écran de pause sur **plusieurs moniteurs** : pendant la pause, chaque écran
  connecté peut être recouvert par sa propre superposition (option « Assombrir
  tous les écrans », activée par défaut). Fonctionne indépendamment du DDC/CI —
  donc aussi sur les ordinateurs portables.
- **Couleur de l'écran de pause librement configurable**, transparence comprise
  (curseur d'opacité/alpha), via un sélecteur de couleur dans les paramètres.
- **Mise à jour automatique** : lorsqu'une nouvelle version est disponible,
  LookAway peut l'installer lui-même — le nouveau paquet portable est téléchargé,
  les fichiers du programme sont remplacés après la fermeture et l'application
  redémarre. Nouveau réglage **« Mettre à jour automatiquement »** : télécharge la
  dernière version en arrière-plan et l'installe au prochain démarrage, sans
  intervention. Sinon, un clic sur l'entrée « Update » de la zone de notification suffit.

### Modifié

- Paramètres modernisés : la barre d'onglets en haut a été remplacée par un
  **menu latéral** rétractable (NavigationView avec bouton hamburger).
- Nouveau **thème clair menthe/sarcelle** (reposant pour les yeux) sur toute
  l'interface.
- Après une **mise en veille ou une inactivité** (p. ex. un appel téléphonique),
  le minuteur de travail redémarre à zéro si l'absence a duré au moins le temps
  d'une pause — les yeux se sont alors déjà reposés. Les courtes interruptions
  reprennent avec le temps restant comme avant ; une pause manuelle reste inchangée.
- L'entrée « Update » de la zone de notification n'ouvre plus seulement la page de
  publication : elle télécharge la nouvelle version et l'installe automatiquement.

## [1.0.2] – 2026-06-29

### Corrigé

- Plantage au démarrage en mode non empaqueté corrigé : l'icône de la zone de
  notification était transmise à H.NotifyIcon en PNG et levait une exception ; elle est
  désormais au format ICO DIB. De plus, la publication ne contenait pas l'index de
  ressources (PRI) ni les fichiers d'actifs en clair (XamlParseException /
  DirectoryNotFoundException) — l'application démarre maintenant de façon fiable. Le ZIP
  portable et le MSIX sont ainsi pleinement exécutables pour la première fois.

### Ajouté

- Installateur Setup.exe (Inno Setup) : emplacement d'installation librement choisi,
  installation pour l'utilisateur courant ou pour tous les utilisateurs, raccourci menu
  Démarrer / bureau (optionnel), démarrage automatique optionnel et désinstallateur.
  Autonome (self-contained) — aucune runtime .NET / Windows App SDK préinstallée requise.

### Modifié

- Les builds distribuables sont autonomes (Windows App SDK), le pipeline CI est durci
  (exécution verte, actions épinglées par SHA, node24) et l'historique Git a été nettoyé.

## [1.0.1] – 2026-06-29

### Ajouté

- Écran de pause plein écran assombri : masque l'écran pendant la pause, affiche le
  compte à rebours et l'objectif d'exercice et peut être terminé par avance avec **ÉCHAP**
- Icône d'application de l'EXE (Explorateur, barre des tâches, Alt+Tab) à partir du logo LookAway

### Modifié

- Logos de tuile et du Store (MSIX) régénérés à partir du logo LookAway

## [1.0.0] – 2026-06-28

Première version complète.

### Ajouté

- Application de zone de notification avec verrou d'instance unique et icône d'état avec info-bulle en direct
- Moteur de minuterie avec sept modèles de pause et état résilient à la mise en veille
- Rappel de pause sous forme de fenêtre de surimpression discrète (démarrer la pause / reporter / ignorer)
- Pause automatique en cas d'inactivité et mode Ne pas déranger pour les applications plein écran
- Fenêtre des paramètres (Général, Modèle de pause, Intervalles personnalisés, Son, Actions de pause,
  Raccourcis, Statistiques, Mise à jour, À propos)
- Assistant de premier démarrage pour la configuration initiale
- Trilingue (allemand, anglais, français) avec changement de langue à l'exécution
- Thème central (palette de couleurs, typographie, styles de boutons)
- Son de rappel optionnel avec choix, volume et pré-écoute
- Statistiques (aujourd'hui, semaine, année) avec export CSV
- Raccourcis globaux pour pause, report et Ne pas déranger
- Vérification de mise à jour via l'API des versions GitHub
- Actions de pause : assombrir l'écran et mettre en pause la lecture multimédia
- Démarrage automatique avec Windows via l'entrée Run propre à l'utilisateur
- Distribution sous forme de ZIP portable et de paquet MSIX

[Non publié]: https://github.com/ReneSchustek/LookAway/compare/v1.1.1...HEAD
[1.1.1]: https://github.com/ReneSchustek/LookAway/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/ReneSchustek/LookAway/compare/v1.0.2...v1.1.0
[1.0.2]: https://github.com/ReneSchustek/LookAway/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/ReneSchustek/LookAway/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/ReneSchustek/LookAway/releases/tag/v1.0.0
