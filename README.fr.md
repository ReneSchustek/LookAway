<div align="center">

<img src="src/LookAway.App/Assets/LookAwayLogo.png" alt="LookAway" width="120" />

# LookAway

**Des pauses d'écran, rappelées intelligemment.**

[![CI](https://github.com/ReneSchustek/LookAway/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/ReneSchustek/LookAway/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/ReneSchustek/LookAway?sort=semver)](https://github.com/ReneSchustek/LookAway/releases/latest)

[Deutsch](README.md) · [English](README.en.md) · **Français**

</div>

LookAway est une application Windows légère, logée dans la zone de notification, qui rappelle
discrètement de faire des pauses d'écran. Elle tourne sans déranger en arrière-plan, propose plusieurs
modèles de pause fondés scientifiquement et se configure entièrement par utilisateur Windows — pour
toutes celles et ceux qui travaillent beaucoup à l'écran et veulent préserver leurs yeux, leur posture
et leur concentration.

## Version actuelle

**[v1.0.2](https://github.com/ReneSchustek/LookAway/releases/latest)** est la version actuelle.
Elle apporte un **installateur Setup.exe** avec un emplacement d'installation librement choisi, un
démarrage fiable en mode portable comme en mode MSIX, l'**écran de pause assombri** (fermable avec
ÉCHAP) et la nouvelle icône d'application. Trois variantes d'installation sont disponibles — voir
[Installation](#installation).

## Fonctionnalités

- **7 modèles de pause** – Pomodoro, Pomodoro modifié, rythme ultradien, Physical Counter,
  pauses courtes, basé sur les tâches et la recommandation légale
- **Pause automatique et Ne pas déranger** – met en pause en cas d'inactivité et supprime les rappels
  pendant les applications plein écran (présentations, films, jeux)
- **Trilingue** – allemand, anglais, français, commutable à chaud
- **Statistiques et export CSV** – pauses par jour, semaine et année, exportables
- **Raccourcis globaux** – démarrer une pause, ignorer, basculer Ne pas déranger – depuis partout
- **Son optionnel** – un signal discret lors du rappel (au choix parmi trois sons)
- **Écran de pause assombri** – une surimpression plein écran apaisante masque l'écran pendant la
  pause, affiche le compte à rebours et l'objectif d'exercice, et peut être terminée à tout moment
  avec **ÉCHAP**
- **Actions de pause** – assombrir l'écran (DDC/CI) et mettre en pause la lecture multimédia pendant
  la pause
- **Démarrage automatique** – démarre au besoin automatiquement avec Windows

## Prérequis

- Windows 10 (version 1809) ou plus récent / Windows 11

## Installation

Tous les artefacts se trouvent sur la
[page des versions](https://github.com/ReneSchustek/LookAway/releases/latest) (actuellement **v1.0.2**).

### Variante A : Setup.exe (confortable)

1. Téléchargez et exécutez `LookAway-Setup-v1.0.2.exe`.
2. Dans l'assistant, **choisissez librement l'emplacement** et décidez entre « pour moi uniquement »
   ou « pour tous les utilisateurs ». LookAway est ajouté au menu Démarrer (raccourci bureau/démarrage
   automatique en option) et démarre dans la zone de notification.

> Remarque : la Setup.exe n'est pas signée avec un certificat d'autorité – Windows SmartScreen peut
> avertir (« Informations complémentaires » → « Exécuter quand même »).

### Variante B : portable (sans installation)

1. Téléchargez `LookAway-Portable-v1.0.2.zip` et décompressez-le dans un dossier quelconque.
2. Lancez `LookAway.exe`. En mode portable, toutes les données se trouvent à côté de l'EXE – idéal
   pour une clé USB.

### Variante C : MSIX

Le MSIX est signé avec un certificat **auto-signé**. Pour que Windows autorise l'installation, le
certificat fourni doit être approuvé une seule fois :

1. Téléchargez `LookAway-v1.0.2.cer` et `LookAway-v1.0.2-x64.msix`.
2. Dans une PowerShell **administrateur**, importez le certificat :
   ```powershell
   Import-Certificate -FilePath .\LookAway-v1.0.2.cer -CertStoreLocation Cert:\LocalMachine\Root
   ```
3. Double-cliquez sur le `.msix` et suivez l'invite d'installation. LookAway apparaît ensuite dans le
   menu Démarrer et démarre dans la zone de notification.

## Premiers pas

Au premier lancement, un court assistant en trois étapes guide la configuration : langue, modèle de
pause et démarrage automatique. Ensuite, LookAway vit dans la zone de notification – un clic sur
l'icône ouvre le menu, un double-clic ouvre les paramètres.

## Configuration

Toutes les options se trouvent dans la fenêtre des paramètres (menu de la zone de notification →
« Paramètres… ») : langue, démarrage automatique, modèle de pause, intervalles personnalisés, son,
actions de pause, raccourcis, statistiques et vérification des mises à jour.

## Modèles de pause

| Modèle | Travail | Pause | Recommandé pour |
|---|---|---|---|
| Pauses courtes | 60 min | 5 min | Longues phases de travail calmes |
| Pomodoro classique | 25 min | 5 min | Travail concentré par étapes |
| Pomodoro modifié | 50 min | 10 min | Blocs de concentration plus longs |
| Rythme ultradien | 90 min | 20 min | Travail profond selon le rythme naturel |
| Physical Counter | 40 min | 2 min | Posture et micro-pauses |
| Basé sur les tâches | jusqu'à 120 min | 10 min | Travailler jusqu'à un jalon |
| Recommandation légale | 120 min | 15 min | Travail sur écran selon la réglementation |

## Raccourcis (par défaut)

| Action | Combinaison de touches |
|---|---|
| Démarrer une pause | `Ctrl + Alt + P` |
| Ignorer / Reporter | `Ctrl + Alt + S` |
| Basculer Ne pas déranger | `Ctrl + Alt + D` |

Les raccourcis peuvent être activés dans les paramètres ou réinitialisés à leurs valeurs par défaut.

## Confidentialité

LookAway est sobre en données et fonctionne entièrement en local :

- **Ce qui est enregistré :** paramètres, historique des pauses et fichiers journaux – exclusivement
  sur votre propre appareil sous `%APPDATA%\LookAway` (ou à côté de l'EXE en mode portable).
- **Ce qui n'arrive pas :** aucune donnée d'utilisation, aucune télémétrie et aucune donnée
  personnelle ne sont envoyées à un serveur.
- **Seule connexion réseau :** la vérification de mise à jour optionnelle interroge l'API publique des
  versions GitHub pour savoir si une version plus récente existe. Elle peut être désactivée dans les
  paramètres.

## Captures d'écran

Les captures d'écran actuelles (icône de la zone de notification, rappel de pause, paramètres,
statistiques) se trouvent sous [`docs/screenshots/`](docs/screenshots/).

## Journal des modifications

L'historique des versions est dans [`CHANGELOG.md`](CHANGELOG.md).

## Pour les développeurs

L'architecture, la compilation, les tests et les détails internes sont décrits dans
[`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md).

## Licence

Propriétaire – tous droits réservés.
