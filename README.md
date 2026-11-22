# PrecinctMP — mod multijoueur expérimental pour *The Precinct*

> Projet communautaire non officiel. La version actuelle est une version expérimentale, écrite mais encore à tester en jeu.

## Fonctionnalités actuelles — v0.2.0

- Héberger ou rejoindre une session UDP jusqu'à 4 joueurs.
- Port UDP `7777` par défaut, modifiable depuis la fenêtre d'hébergement.
- Afficher les autres joueurs avec leur pseudo et leur distance.
- Détecter le joueur et le véhicule local via les classes IL2CPP du jeu (`PlayerManager`, `VehicleController`).
- Utiliser le véritable modèle du jeu pour les véhicules distants, avec leur type et leur livrée (`Cruiser`, `SUV`, `Station Wagon`, etc.).
- Utiliser le véritable modèle animé du policier à pied.
- Synchroniser les principaux états d'animation : marche, course, accroupissement et arme.
- Afficher `[GYRO]` lorsque le gyrophare d'un autre joueur est actif.
- Afficher les adresses réseau de l'hôte, notamment Radmin VPN, ZeroTier, Tailscale, Hamachi et le réseau local.
- Afficher l'adresse IP publique de la box lorsqu'elle peut être récupérée par un service externe.
- Bloquer les commandes du joueur pendant la saisie dans les fenêtres F8 et F9.

Chaque joueur conserve sa propre partie solo. Les PNJ, crimes, trafics, suspects, incidents, météo et heure ne sont pas synchronisés.

Le personnage distant utilise actuellement l'apparence du joueur local. La tenue de l'autre joueur et les actions ponctuelles comme les menottes ou le tir sont prévues pour une version ultérieure.

## Commandes

| Touche | Action |
|---|---|
| F8 | Ouvre « DÉBUTER UNE SESSION MULTIJOUEUR ». Affiche les adresses réseau et le port, ou permet d'arrêter la session en cours. |
| F9 | Ouvre « REJOINDRE UNE SESSION ». Saisie de l'adresse IP et du port. |
| Entrée | Valide la fenêtre F8 ou F9. |
| Échap | Ferme la fenêtre F8 ou F9. |
| F10 | Quitte la session multijoueur. |
| F7 | Génère `UserData\PrecinctMP_dump.txt` pour le diagnostic. |
| F11 | Affiche ou masque le HUD. |

Les valeurs saisies dans la fenêtre F9 sont mémorisées automatiquement dans `UserData\MelonPreferences.cfg`. Une seule fenêtre peut être ouverte à la fois.

## Compilation

Prérequis :

- [.NET SDK 8](https://dotnet.microsoft.com/download) ;
- MelonLoader installé dans le dossier du jeu ;
- le jeu lancé au moins une fois avec MelonLoader ;
- le dossier `MelonLoader\Il2CppAssemblies\` généré par MelonLoader.

Le jeu utilise actuellement **IL2CPP**. Les DLL présentes dans `MelonLoader\Il2CppAssemblies\` sont nécessaires à la compilation.

Compile depuis le dossier du projet :

```powershell
dotnet build -c Release -p:GameDir="G:\SteamLibrary\steamapps\common\The Precinct"
```

Le projet copie automatiquement `PrecinctMP.dll` dans `Mods\` et `LiteNetLib.dll` dans `UserLibs\`.

Si le jeu est installé ailleurs, adapte `GameDir` à ton installation. Le chemin peut aussi être défini dans `PrecinctMP.csproj`.

## Configuration et réseau

La configuration se trouve dans `UserData\MelonPreferences.cfg`, section `[PrecinctMP]`. Les valeurs principales sont créées automatiquement au premier lancement :

```ini
PlayerName = "Anthony"
HostIp = ""
Port = 7777
PlayerObjectName = ""
MinVehicleMass = 500.0
```

L'adresse et le port utilisés pour rejoindre une session se saisissent normalement dans F9. Il n'est donc plus nécessaire de modifier `HostIp` et `Port` manuellement.

### Connexion par Internet

Dans la fenêtre F8, l'adresse **Internet (box)** est récupérée en arrière-plan via `api.ipify.org` et des services de secours. Cette adresse peut mettre quelques secondes à apparaître et dépend d'un service externe.

Pour une connexion directe par Internet :

1. Repère l'adresse **Réseau local** de l'ordinateur hôte dans F8.
2. Configure sur la box une redirection **UDP du port choisi**, par défaut `7777`, vers cette adresse locale.
3. Autorise le jeu dans le pare-feu Windows si nécessaire.
4. Donne aux autres joueurs l'adresse **Internet (box)** et le port affichés dans F8.

### Connexion par VPN

Radmin VPN, ZeroTier, Tailscale ou Hamachi évitent généralement la configuration de la box. Dans ce cas, les autres joueurs utilisent dans F9 l'adresse marquée avec le nom du VPN, et non l'adresse Internet de la box.

## Installation d'une mise à jour

Lorsque l'archive ne contient pas le fichier `.csproj` :

1. Ferme Visual Studio ou le projet.
2. Ouvre le dossier qui contient `PrecinctMP.csproj`.
3. Copie le contenu du dossier `src` de l'archive par-dessus le dossier `src` existant.
4. Remplace également `README.md`.
5. Ne remplace pas `PrecinctMP.csproj` : il contient notamment le chemin du jeu.
6. Rouvre le projet et compile en configuration `Release`.

Le dossier `src` contient le code source du projet. Il ne doit pas être copié directement dans le dossier du jeu ; seule la DLL compilée doit être installée dans `Mods`.

## Diagnostic et premier test

1. Lance le jeu et charge une partie solo.
2. Vérifie que le HUD affiche `Local : à pied` ou `Local : en voiture`.
3. Appuie sur F7 à pied puis au volant afin de générer les diagnostics.
4. Si le joueur n'est pas détecté, envoie `UserData\PrecinctMP_dump.txt`.
5. Pour tester le réseau, démarre une session avec F8, puis rejoins-la avec F9 depuis une autre instance ou un autre PC.

Pour un test local, utilise `127.0.0.1` dans F9. Pour un test entre amis, utilise une adresse VPN ou l'adresse Internet après avoir configuré la redirection UDP.

En cas de plantage, fournir `MelonLoader\Latest.log` ainsi que `UserData\PrecinctMP_dump.txt` si disponible.

Le fichier `MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll` est également utile pour identifier les classes et méthodes du jeu, mais il ne contient pas directement leur code source.

## Feuille de route

| Version | Contenu | État |
|---|---|---|
| 0.1 | Réseau et positions des joueurs et véhicules avec formes simples | ✅ Écrit |
| 0.2 | Classes réelles du jeu, modèles et livrées des véhicules, état du gyrophare | ✅ Écrit, à tester |
| 0.2.0 | Modèle réel et animations du policier à pied | ✅ Écrit, à tester |
| 0.2.x | Tenue de l'autre joueur, menottes, tir et autres actions | ⏳ Prévu |
| 0.3 | Synchronisation complète des gyrophares et sirènes, collisions entre véhicules | ⏳ Prévu |
| 0.4 | Partage des appels radio et des incidents | ⏳ Prévu |
| 0.5 | Synchronisation des suspects poursuivis et des arrestations | ⏳ Prévu |
| Plus tard | Synchronisation du trafic, des PNJ, de l'heure et de la météo | ⏳ Prévu |

## Structure du projet

```text
PrecinctMP.csproj
src/Mod.cs           Point d'entrée MelonLoader, touches et HUD
src/NetSession.cs    Hôte/client LiteNetLib, relais et identifiants
src/Protocol.cs      Format des paquets réseau
src/LocalPlayer.cs   Détection du joueur et du véhicule local
src/RemotePlayers.cs Affichage et interpolation des joueurs distants
src/Diagnostics.cs   Diagnostic F7 des classes du jeu
src/GameApi.cs       Accès aux classes du jeu
src/VisualClone.cs   Copie visuelle minimale d'un objet du jeu
src/VehicleVisual.cs Véhicule distant et livrée du jeu
src/CharacterAnim.cs Synchronisation des animations du policier
src/Polyfills.cs     Attributs nécessaires aux DLL IL2CPP
src/ModalDialog.cs   Base commune des fenêtres modales
src/JoinDialog.cs    Fenêtre « Rejoindre une session »
src/HostDialog.cs    Fenêtre « Débuter une session multijoueur »
src/Hotkeys.cs       Gestion des touches via l'API Windows
```

## Limites connues

- Le protocole n'est pas chiffré et un client peut envoyer de fausses positions. À utiliser uniquement entre amis.
- Une mise à jour du jeu ou de MelonLoader peut casser le mod.
- La récupération de l'IP publique dépend d'un service web externe.
- Une connexion directe par Internet nécessite une redirection du port UDP et une configuration correcte du pare-feu.
- Les touches F7 à F11 sont lues via l'API Windows : elles fonctionnent lorsque la fenêtre du jeu est au premier plan.
- Si l'interface HUD n'apparaît pas, le mod peut continuer à fonctionner ; consulter `MelonLoader\Latest.log`.
- Projet non affilié à Fallen Tree Games ni à Kwalee.
