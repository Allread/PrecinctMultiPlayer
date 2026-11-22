# PrecinctMP — mod multijoueur expérimental pour The Precinct

> Projet non officiel, au tout début. Rien n'a encore été testé en jeu.

## Ce que fait la v0.2.0

- Héberger / rejoindre une partie en UDP (jusqu'à 4 joueurs, port 7777 par défaut).
- Chaque joueur voit les autres se déplacer, à pied ou en voiture, avec leur pseudo et leur distance.
- Le mod lit le joueur et sa voiture directement via les classes du jeu (`PlayerManager`, `VehicleController`).
- La voiture des autres joueurs utilise **le vrai modèle du jeu, avec la même livrée** (Cruiser, SUV, Station Wagon...). Ni l'un ni l'autre n'a de collision.
- **Le policier à pied utilise le vrai modèle du jeu, animé** : le mod envoie les paramètres d'animation (marche, course, accroupi, arme…) et le modèle distant joue les mêmes animations. Limite : il a **ton** apparence (tenue), pas celle de l'autre joueur.
- `[GYRO]` s'affiche à côté du pseudo quand le gyrophare d'un autre joueur est allumé.
- Chaque joueur garde **sa propre partie solo** : PNJ, crimes, trafic et météo ne sont pas synchronisés.

## Touches

| Touche | Action |
|---|---|
| F8 | Ouvre « DÉBUTER UNE SESSION MULTIJOUEUR » : tes IP (Radmin, ZeroTier, réseau local…) avec un bouton Copier, et le port. Pendant une session, permet de l'arrêter. |
| F9 | Ouvre « REJOINDRE UNE SESSION » : IP + port (Entrée = rejoindre, Échap = annuler). Les valeurs sont mémorisées. |

Pendant qu'une fenêtre est ouverte, les commandes du jeu sont bloquées.
| F10 | Quitter la session |
| F7 | Diagnostic → `UserData/PrecinctMP_dump.txt` |
| F11 | Afficher / masquer le HUD |

## Compilation

Prérequis : [.NET SDK 8](https://dotnet.microsoft.com/download), MelonLoader installé sur le jeu et lancé une fois.

1. Lance le jeu une fois avec MelonLoader. Le jeu est en **IL2CPP** : au premier lancement, MelonLoader génère `MelonLoader\Il2CppAssemblies\`, et le projet a besoin de ce dossier pour compiler.
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

## Fichier à m'envoyer en priorité

`MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll` contient la liste de toutes les classes et méthodes du jeu, mais pas leur code. C'est grâce à lui que je peux brancher le mod sur les vraies classes (joueur, voiture, sirène, incidents).

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
| 0.2 | Classes réelles du jeu, vrais modèles de voiture + livrée, état sirène | ✅ écrit, à tester |
| 0.2.0 | Vrai modèle du policier à pied, animé | ✅ écrit, à tester |
| 0.2.x | Tenue réelle de l'autre joueur, actions ponctuelles (menottes, tir…) | ⏳ |
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
src/GameApi.cs       Accès aux classes du jeu (PlayerManager, VehicleController...)
src/VisualClone.cs   Copie « coquille vide » d'un objet du jeu (meshes ± Animator)
src/VehicleVisual.cs Voiture des joueurs distants (prefab du jeu + livrée)
src/CharacterAnim.cs Synchro des paramètres d'animation du policier
src/Polyfills.cs     Attributs compilateur manquants dans les DLL IL2CPP
src/ModalDialog.cs   Base des fenêtres (souris, blocage des commandes, Entrée/Échap)
src/JoinDialog.cs    Fenêtre « Rejoindre une session »
src/HostDialog.cs    Fenêtre « Débuter une session multijoueur »
src/Hotkeys.cs       Touches via l'API Windows (compatible IL2CPP / nouvel Input System)
```

## Limites connues

- Pas de chiffrement, et un client peut envoyer de fausses positions. À n'utiliser qu'entre amis.
- Une mise à jour du jeu peut casser le mod.
- Les touches F7 à F11 sont lues directement par Windows : elles marchent même si le jeu utilise le nouvel Input System d'Unity, mais uniquement quand la fenêtre du jeu est au premier plan.
- Si le HUD n'apparaît pas (IMGUI absent du build), le mod continue de fonctionner et écrit tout dans `MelonLoader\Latest.log`.
- Projet communautaire non affilié à Fallen Tree Games ni à Kwalee.
