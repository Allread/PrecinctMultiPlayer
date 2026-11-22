# PrecinctMP

Mod multijoueur expérimental et non officiel pour *The Precinct*.

> Le projet est en développement. La version actuelle est écrite, mais n'est pas encore validée en jeu.

## Présentation

PrecinctMP ajoute une session multijoueur légère au jeu : jusqu'à quatre joueurs peuvent partager leurs positions, leur état à pied ou en véhicule, leur pseudo et leur distance.

Le mod ne synchronise pas une partie complète. Chaque joueur conserve sa propre partie solo ; les PNJ, crimes, suspects, incidents, trafic, météo et heure restent locaux.

## État actuel

Version expérimentale : **v0.2.0**

- Communication réseau UDP avec hôte et clients.
- Modèles réels du jeu pour les véhicules distants, avec leur type et leur livrée.
- Modèle réel et animé du policier à pied.
- Synchronisation des principaux états d'animation : marche, course, accroupissement et arme.
- Affichage du pseudo, de la distance et de l'état du gyrophare (`[GYRO]`).
- Fenêtres F8 et F9 pour héberger ou rejoindre une session.
- Détection des adresses réseau locales et VPN.
- Recherche facultative de l'adresse IP publique de l'hôte.
- Blocage des commandes du jeu pendant la saisie dans les fenêtres modales.

Le personnage distant utilise actuellement l'apparence du joueur local. La synchronisation des tenues, des menottes, du tir et des autres actions est prévue ultérieurement.

## Prérequis

### Pour compiler

- Windows ;
- Visual Studio 2026 ou le [.NET SDK 8](https://dotnet.microsoft.com/download) ;
- le projet cible `.NET 6` ;
- Git ;
- MelonLoader installé dans *The Precinct* ;
- le jeu lancé au moins une fois avec MelonLoader.

Le jeu utilise IL2CPP. Le premier lancement avec MelonLoader doit avoir généré :

```text
<Dossier du jeu>\MelonLoader\Il2CppAssemblies\
```

Ces assemblages sont nécessaires à la compilation.

### Pour jouer

Chaque joueur doit disposer de :

- sa propre installation du jeu ;
- MelonLoader ;
- `PrecinctMP.dll` dans `Mods` ;
- `LiteNetLib.dll` dans `UserLibs`.

## Récupérer le projet

```powershell
git clone https://github.com/Allread/PrecinctMultiPlayer.git
cd PrecinctMultiPlayer
```

Ouvre ensuite `PrecinctMP.slnx` ou `PrecinctMP.csproj` dans Visual Studio.

## Configuration du chemin du jeu

Le chemin du jeu est défini par la propriété `GameDir`. Il doit pointer vers le dossier qui contient `ThePrecinct.exe`, `MelonLoader` et les fichiers du jeu.

Tu peux le fournir lors de la compilation :

```powershell
dotnet build -c Release -p:GameDir="G:\SteamLibrary\steamapps\common\The Precinct"
```

Ou modifier la valeur correspondante dans `PrecinctMP.csproj`.

Évite de committer un chemin local personnel dans le dépôt. Utilise de préférence l'option `-p:GameDir=...` ou un fichier de configuration local non suivi par Git.

## Compiler

1. Lance le jeu une fois avec MelonLoader.
2. Vérifie que `MelonLoader\Il2CppAssemblies\` contient des DLL.
3. Configure `GameDir`.
4. Compile en `Release` :

   ```powershell
   dotnet build -c Release -p:GameDir="C:\Chemin\vers\The Precinct"
   ```

Le projet copie automatiquement :

- `PrecinctMP.dll` dans `<Dossier du jeu>\Mods\` ;
- `LiteNetLib.dll` dans `<Dossier du jeu>\UserLibs\`.

## Utiliser le mod

Lance le jeu et charge une partie. Les raccourcis sont les suivants :

| Touche | Action |
|---|---|
| F8 | Ouvre « DÉBUTER UNE SESSION MULTIJOUEUR ». Permet de choisir le port et de démarrer ou arrêter la session. |
| F9 | Ouvre « REJOINDRE UNE SESSION ». Permet de saisir l'adresse IP et le port. |
| Entrée | Valide la fenêtre active. |
| Échap | Ferme la fenêtre active. |
| F10 | Quitte la session multijoueur. |
| F7 | Génère le dump de diagnostic. |
| F11 | Affiche ou masque le HUD. |

Les valeurs saisies dans F9 sont mémorisées automatiquement dans `UserData\MelonPreferences.cfg`. Les commandes du jeu sont bloquées tant qu'une fenêtre F8 ou F9 est ouverte.

## Connexion réseau

### Via VPN — recommandé pour les premiers tests

L'hôte ouvre F8 et communique à ses amis une adresse marquée **Radmin VPN**, **ZeroTier**, **Tailscale** ou **Hamachi**. Les clients saisissent cette adresse et le port dans F9.

### Via Internet

Pour une connexion directe, l'hôte doit rediriger le port UDP choisi — `7777` par défaut — vers l'adresse locale de son PC :

1. Dans F8, repère l'adresse **Réseau local**.
2. Ajoute une règle NAT/redirection UDP sur la box.
3. Autorise le jeu dans le pare-feu Windows.
4. Utilise l'adresse **Internet (box)** affichée dans F8.

L'adresse publique est récupérée en arrière-plan via `api.ipify.org` et des services de secours. Elle peut rester indisponible si le service web ou le réseau ne répond pas.

## Premier test

Commence par tester la détection locale :

1. Lance une partie solo.
2. Vérifie que le HUD indique `Local : à pied` ou `Local : en voiture`.
3. Appuie sur F7 à pied puis au volant.
4. Si le joueur n'est pas détecté, conserve `UserData\PrecinctMP_dump.txt`.

Pour un test réseau sur la même machine, démarre l'hôte avec F8 et rejoins avec F9 en utilisant `127.0.0.1`. Pour un test avec plusieurs ordinateurs, utilise une adresse VPN avant d'essayer la connexion Internet directe.

## Diagnostic et contribution

En cas de problème, joindre :

- `MelonLoader\Latest.log` ;
- `UserData\PrecinctMP_dump.txt` ;
- la version du jeu et de MelonLoader ;
- le type de connexion utilisé : locale, VPN ou Internet ;
- les étapes précises pour reproduire le problème.

Pour comprendre les classes IL2CPP du jeu, le fichier suivant peut être utile :

```text
MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll
```

Il contient les types et méthodes générés, mais pas le code source du jeu.

## Structure du dépôt

```text
PrecinctMP.slnx
PrecinctMP.csproj
src/
  Mod.cs           Point d'entrée MelonLoader, touches et HUD
  NetSession.cs    Hôte/client LiteNetLib et synchronisation réseau
  Protocol.cs      Format des paquets
  LocalPlayer.cs   Détection du joueur et du véhicule local
  RemotePlayers.cs Affichage et interpolation des joueurs distants
  GameApi.cs       Accès aux classes du jeu
  VisualClone.cs   Copie visuelle minimale d'un objet du jeu
  VehicleVisual.cs Véhicule distant et livrée
  CharacterAnim.cs Synchronisation des animations
  Diagnostics.cs   Diagnostic F7
  ModalDialog.cs   Base commune des fenêtres modales
  JoinDialog.cs    Fenêtre de connexion
  HostDialog.cs    Fenêtre d'hébergement
  Hotkeys.cs       Gestion des raccourcis clavier
```

## Feuille de route

| Version | Objectif | État |
|---|---|---|
| 0.1 | Réseau et positions avec formes simples | ✅ Écrit |
| 0.2 | Classes réelles, modèles des véhicules et états du gyrophare | ✅ Écrit, à tester |
| 0.2.0 | Modèle et animations du policier à pied | ✅ Écrit, à tester |
| 0.2.x | Tenues et actions ponctuelles des joueurs | ⏳ Prévu |
| 0.3 | Synchronisation avancée des gyrophares, sirènes et collisions | ⏳ Prévu |
| 0.4 | Appels radio et incidents partagés | ⏳ Prévu |
| 0.5 | Suspects et arrestations synchronisés | ⏳ Prévu |
| Plus tard | Trafic, PNJ, heure et météo synchronisés | ⏳ Prévu |

## Limites et avertissements

- Le protocole n'est pas chiffré.
- Un client peut envoyer de fausses positions.
- Une mise à jour du jeu ou de MelonLoader peut casser le mod.
- Une connexion directe par Internet nécessite une redirection UDP et une configuration correcte du pare-feu.
- La récupération de l'IP publique dépend d'un service web externe.
- Les raccourcis sont lus via l'API Windows et fonctionnent lorsque le jeu est au premier plan.
- Le mod est expérimental et n'est pas affilié à Fallen Tree Games ni à Kwalee.
