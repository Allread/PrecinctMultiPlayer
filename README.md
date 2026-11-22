# PrecinctMP — mod multijoueur expérimental pour The Precinct

> Projet non officiel, au tout début. Rien n'a encore été testé en jeu.

## Ce que fait la v0.1

- Héberger / rejoindre une partie en UDP (jusqu'à 4 joueurs, port 7777 par défaut).
- Chaque joueur voit les autres se déplacer, à pied ou en voiture, avec leur pseudo et leur distance.
- Les autres joueurs sont représentés par des formes simples : capsule bleue pour le policier, boîte blanche pour la voiture. Elles n'ont pas de collision.
- Chaque joueur garde **sa propre partie solo** : PNJ, crimes, trafic et météo ne sont pas synchronisés.

## Touches

| Touche | Action |
|---|---|
| F8 | Héberger une partie |
| F9 | Rejoindre (IP définie dans la config) |
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

## Configuration

Après le premier lancement, édite `UserData\MelonPreferences.cfg`, section `[PrecinctMP]` :

```
PlayerName = "Anthony"
HostIp = "82.x.x.x"      # IP publique de l'hôte (ou IP Radmin/ZeroTier/Tailscale)
Port = 7777
PlayerObjectName = ""    # à remplir si "joueur NON détecté"
MinVehicleMass = 500.0
```

L'hôte doit ouvrir le **port UDP 7777** sur sa box, ou alors vous passez tous par un VPN de jeu (Radmin VPN, ZeroTier, Tailscale). Le VPN est le plus simple pour tester.

## Premier test (à faire en solo d'abord)

1. Lance le jeu, charge une partie : le HUD doit afficher `Local : à pied` ou `en voiture`.
2. Si le HUD affiche `joueur NON détecté`, appuie sur **F7** et envoie-moi `UserData\PrecinctMP_dump.txt`.
3. Même si la détection marche, fais F7 une fois à pied et une fois au volant, puis envoie-moi les deux dumps. Avec les vraies classes du jeu, je pourrai passer à l'étape suivante.
4. Test réseau sur un seul PC : lance le jeu, appuie sur F8, puis lance une 2e instance si Steam le permet et appuie sur F9 avec `HostIp = 127.0.0.1`. Sinon, teste directement avec un ami.

En cas de plantage, envoie `MelonLoader\Latest.log`.

## Feuille de route

| Étape | Contenu | État |
|---|---|---|
| 0.1 | Réseau + positions joueurs/voitures (formes simples) | ✅ écrit, à tester |
| 0.2 | Classes réelles du jeu (via dumps F7), vrais modèles de policier et de voiture | ⏳ |
| 0.3 | Gyrophares/sirène, livrée de la voiture distante, collisions voiture ↔ voiture | ⏳ |
| 0.4 | Partage des appels radio : l'hôte crée l'incident, les autres le voient | ⏳ |
| 0.5 | Synchro des suspects poursuivis et des arrestations (le plus dur) | ⏳ |
| plus tard | Trafic, PNJ et heure/météo synchronisés | ? |

## Structure

```
PrecinctMP.csproj
src/Mod.cs           Point d'entrée MelonLoader, touches, HUD
src/NetSession.cs    Hôte/client LiteNetLib, relais, ids des joueurs
src/Protocol.cs      Format des paquets
src/LocalPlayer.cs   Détection du joueur/voiture local
src/RemotePlayers.cs Affichage et interpolation des autres joueurs
src/Diagnostics.cs   Dump F7 pour découvrir les classes du jeu
```

## Limites connues

- Pas de chiffrement, et un client peut envoyer de fausses positions. À n'utiliser qu'entre amis.
- Une mise à jour du jeu peut casser le mod.
- Projet communautaire non affilié à Fallen Tree Games ni à Kwalee.
