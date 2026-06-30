# Journal des modifications

[Deutsch](CHANGELOG.md) · [English](CHANGELOG.en.md) · **Français**

Toutes les modifications notables de LookAway sont documentées ici.
Le format s'appuie sur [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/),
et la gestion des versions suit [Semantic Versioning](https://semver.org/lang/fr/).

## [Non publié]

### Ajouté

- Écran de pause sur **plusieurs moniteurs** : pendant la pause, chaque écran
  connecté peut être recouvert par sa propre superposition (option « Assombrir
  tous les écrans », activée par défaut). Fonctionne indépendamment du DDC/CI —
  donc aussi sur les ordinateurs portables.
- **Couleur de l'écran de pause librement configurable**, transparence comprise
  (curseur d'opacité/alpha), via un sélecteur de couleur dans les paramètres.

### Modifié

- Paramètres modernisés : la barre d'onglets en haut a été remplacée par un
  **menu latéral** rétractable (NavigationView avec bouton hamburger).
- Nouveau **thème clair menthe/sarcelle** (reposant pour les yeux) sur toute
  l'interface.

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

[Non publié]: https://github.com/ReneSchustek/LookAway/compare/v1.0.2...HEAD
[1.0.2]: https://github.com/ReneSchustek/LookAway/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/ReneSchustek/LookAway/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/ReneSchustek/LookAway/releases/tag/v1.0.0
