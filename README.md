# PrecinctMP — mod multijoueur expérimental pour The Precinct

> Projet non officiel, expérimental. La v0.2.2 n'a pas encore été testée en jeu.

## Ce que fait la v0.2.2

- Héberger / rejoindre une partie en UDP (jusqu'à 4 joueurs, port 7777 par défaut).
- Chaque joueur voit les autres se déplacer, à pied ou en voiture, avec leur pseudo et leur distance.
- Les autres joueurs sont représentés par des formes simples : capsule bleue pour le policier, boîte blanche pour la voiture. Elles n'ont pas de collision.
- Chaque joueur garde **sa propre partie solo** : PNJ, crimes, trafic et météo ne sont pas synchronisés.
- F8 ouvre une fenêtre **DÉBUTER UNE SESSION MULTIJOUEUR**.
- F9 ouvre une fenêtre **REJOINDRE UNE SESSION** avec les champs Adresse IP et Port.
- Les fenêtres mémorisent automatiquement le dernier port et la dernière connexion dans `MelonPreferences.cfg`.
- La fenêtre d'hébergement affiche les adresses IP locales et VPN (Radmin VPN, ZeroTier, Tailscale, Hamachi et réseau local), avec un bouton Copier pour chaque adresse.
- L'adresse IP Internet publique est recherchée en arrière-plan et affichée comme **Internet (box)**, avec un bouton Copier.
- Les commandes du joueur sont bloquées pendant l'affichage d'une fenêtre F8 ou F9, puis restaurées à la fermeture.
- Une seule fenêtre peut être ouverte à la fois : F8 ferme celle de F9 et inversement.

## Touches

| Touche | Action |
|---|---|
| F8 | Ouvrir la fenêtre pour débuter ou arrêter une session |
| F9 | Ouvrir la fenêtre pour rejoindre une session |
| F10 | Quitter la session |
| F7 | Diagnostic → `UserData/PrecinctMP_dump.txt` |
| F11 | Afficher / masquer le HUD |

## Compilation

Prérequis : [.NET SDK 8](https://dotnet.microsoft.com/download), MelonLoader installé sur le jeu et lancé une fois.

1. Vérifie le type de build du jeu :
   - `Precinct_Data\Managed\Assembly-CSharp.dll` existe → **Mono** (par défaut).
   - `GameAssembly.dll` à la racine du jeu → **IL2CPP** : ajoute `-p:IsIl2Cpp=true`.
2. Compile :
   ```
   dotnet build -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\The Precinct"
   ```
3. `PrecinctMP.dll` est copié dans `Mods\` et `LiteNetLib.dll` dans `UserLibs\`.

## Configuration et réseau

L'adresse IP et le port de la dernière connexion sont désormais saisis directement dans la fenêtre F9 et mémorisés automatiquement dans `UserData\MelonPreferences.cfg`. Il n'est plus nécessaire de modifier ces valeurs manuellement.

Dans la fenêtre F8, les adresses sont classées pour faciliter le choix : les VPN sont affichés en priorité, puis le réseau local. L'adresse **Internet (box)** est fournie par un service web externe (`api.ipify.org`, avec des sites de secours) et peut prendre quelques secondes à apparaître. Elle n'est pas nécessaire si vous utilisez Radmin VPN, ZeroTier ou un autre VPN de jeu.

Pour permettre à des amis de rejoindre l'hôte avec son IP Internet :

1. Repère l'adresse **Réseau local** de ton PC dans la fenêtre F8.
2. Configure une redirection **UDP du port 7777** de ta box vers cette adresse locale.
3. Autorise le jeu dans le pare-feu Windows si cela est demandé.
4. Communique l'adresse affichée comme **Internet (box)** et le port choisi à tes amis.

L'utilisation d'un VPN de jeu est généralement plus simple : communique plutôt l'adresse marquée Radmin VPN, ZeroTier, Tailscale ou Hamachi.

## Premier test (à faire en solo d'abord)

1. Lance le jeu, charge une partie : le HUD doit afficher `Local : à pied` ou `en voiture`.
2. Si le HUD affiche `joueur NON détecté`, appuie sur **F7** et envoie-moi `UserData\PrecinctMP_dump.txt`.
3. Même si la détection marche, fais F7 une fois à pied et une fois au volant, puis envoie-moi les deux dumps. Avec les vraies classes du jeu, je pourrai passer à l'étape suivante.
4. Test réseau sur un seul PC : lance le jeu, appuie sur F8 et démarre la session, puis lance une 2e instance si Steam le permet et appuie sur F9. Saisis `127.0.0.1` et le même port dans la fenêtre.

En cas de plantage, envoie `MelonLoader\Latest.log`.

## Feuille de route

| Étape | Contenu | État |
|---|---|---|
| 0.1 | Réseau + positions joueurs/voitures (formes simples) | ✅ écrit |
| 0.2.1 | Fenêtre F9 pour rejoindre une session, mémorisation IP/port | ✅ écrit, à tester |
| 0.2.2 | Fenêtres F8/F9, blocage des commandes, sélection des interfaces réseau et IP publique | ✅ écrit, à tester |
| 0.2 | Classes réelles du jeu (via dumps F7), vrais modèles de policier et de voiture | ⏳ |
| 0.3 | Gyrophares/sirène, livrée de la voiture distante, collisions voiture ↔ voiture | ⏳ |
| 0.4 | Partage des appels radio : l'hôte crée l'incident, les autres le voient | ⏳ |
| 0.5 | Synchro des suspects poursuivis et des arrestations (le plus dur) | ⏳ |
| plus tard | Trafic, PNJ et heure/météo synchronisés | ? |

## Structure

```
PrecinctMP.csproj
src/Mod.cs           Point d'entrée MelonLoader, touches, HUD
src/ModalDialog.cs   Comportement commun aux fenêtres modales
src/JoinDialog.cs    Fenêtre pour rejoindre une session
src/HostDialog.cs    Fenêtre pour débuter une session et afficher les IP
src/GameApi.cs       Accès aux fonctions du jeu, dont le blocage des commandes
src/NetSession.cs    Hôte/client LiteNetLib, relais, ids des joueurs
src/Protocol.cs      Format des paquets
src/LocalPlayer.cs   Détection du joueur/voiture local
src/RemotePlayers.cs Affichage et interpolation des autres joueurs
src/Diagnostics.cs   Dump F7 pour découvrir les classes du jeu
```

## Installation d'une mise à jour

Le zip de la v0.2.2 ne contient pas le `.csproj`. Ferme Visual Studio, puis copie le contenu de `src` par-dessus le dossier `src` existant, ainsi que `README.md`. Le fichier `PrecinctMP.csproj` et le chemin du jeu ne sont pas modifiés.

Ne copie pas le dossier `src` dans le dossier du jeu : il s'agit du code source du projet Visual Studio. Seule la DLL compilée doit être installée dans `Mods`.

## Limites connues

- Pas de chiffrement, et un client peut envoyer de fausses positions. À n'utiliser qu'entre amis.
- Une mise à jour du jeu peut casser le mod.
- La récupération de l'IP publique dépend d'un service web externe et ne fournit pas l'IP si la requête échoue.
- La redirection du port UDP sur la box reste nécessaire pour une connexion directe par Internet.
- Projet communautaire non affilié à Fallen Tree Games ni à Kwalee.
