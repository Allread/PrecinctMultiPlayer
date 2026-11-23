# PrecinctMP

Mod multijoueur expérimental et non officiel pour *The Precinct*.

> Le projet est en développement. Les DLL de la v0.4.1 sont disponibles dans le dossier `mod_dll`, mais cette version reste expérimentale.

## Présentation

PrecinctMP ajoute une session multijoueur légère au jeu : jusqu'à quatre joueurs peuvent partager leurs positions, leur état à pied ou en véhicule, leur pseudo et leur distance.

Le mod ne synchronise pas une partie complète. Chaque joueur conserve sa propre partie solo ; les PNJ, le trafic et les autres éléments de gameplay restent locaux. Les incidents, les suspects, l'heure et la météo de l'hôte constituent les exceptions et sont transmis aux clients pendant la session.

## État actuel

Version disponible : **v0.4.1**

- Communication réseau UDP avec hôte et clients.
- Modèles réels du jeu pour les véhicules distants, avec leur type et leur livrée.
- Modèle réel et animé du policier à pied.
- Synchronisation des principaux états d'animation : marche, course, accroupissement et arme.
- Affichage du pseudo, de la distance et de l'état du gyrophare (`[GYRO]`).
- Fenêtres F8 et F9 pour héberger ou rejoindre une session.
- Détection des adresses réseau locales et VPN.
- Recherche facultative de l'adresse IP publique de l'hôte.
- Blocage des commandes du jeu pendant la saisie dans les fenêtres modales.
- Partage par l'hôte des incidents en cours et de leur état.
- Affichage côté client des appels de l'hôte, triés par distance, avec marqueurs sur la carte ou dans le monde.
- Partage des suspects de l'hôte avec leur position, leur apparence, leur véhicule, leur statut et leurs animations.
- Synchronisation de l'heure, de la météo et de la vitesse d'écoulement du temps de l'hôte.
- Transition progressive de la météo côté client, en utilisant les changements normaux du jeu.
- Affichage de l'heure et de la météo de l'hôte dans le HUD.
- Envoi de demandes d'action du client vers l'hôte pour les suspects partagés.
- Vérification des actions par l'hôte avant leur exécution sur les vrais personnages du jeu.
- Fenêtre F6 pour personnaliser les touches des actions sur les suspects.

Les suspects affichés côté client sont des copies visuelles sans collision. Les actions sont toutefois exécutées par l'hôte sur ses vrais personnages, puis le résultat est renvoyé au client.

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

La v0.4.1 modifie le format réseau et ajoute une nouvelle référence dans `PrecinctMP.csproj`. L'hôte et tous les clients doivent utiliser exactement la même version.

1. Remplace l'ensemble du projet par les fichiers de la v0.4.1, y compris `PrecinctMP.csproj`.
2. Supprime les dossiers `bin` et `obj` afin d'éviter de conserver une ancienne configuration ou des anciennes références.
3. Lance le jeu une fois avec MelonLoader.
4. Vérifie que `MelonLoader\Il2CppAssemblies\` contient des DLL.
5. Configure `GameDir`.
6. Compile en `Release` :

   ```powershell
   dotnet build -c Release -p:GameDir="C:\Chemin\vers\The Precinct"
   ```

Le projet copie automatiquement :

- `PrecinctMP.dll` dans `<Dossier du jeu>\Mods\` ;
- `LiteNetLib.dll` dans `<Dossier du jeu>\UserLibs\`.

Si la copie automatique n'est pas active, installe manuellement les DLL en suivant [le guide d'installation](mod_dll/README.md).

## Utiliser le mod

Lance le jeu et charge une partie. Les raccourcis sont les suivants :

| Touche | Action |
|---|---|
| F8 | Ouvre « DÉBUTER UNE SESSION MULTIJOUEUR ». Permet de choisir le port et de démarrer ou arrêter la session. |
| F9 | Ouvre « REJOINDRE UNE SESSION ». Permet de saisir l'adresse IP et le port. |
| F6 | Ouvre « TOUCHES DU MOD » pour personnaliser les touches des actions sur les suspects. |
| Entrée | Valide la fenêtre active. |
| Échap | Ferme la fenêtre active. |
| F10 | Quitte la session multijoueur. |
| F7 | Génère le dump de diagnostic. |
| F11 | Affiche ou masque le HUD. |

Les valeurs saisies dans F9 et les touches personnalisées dans F6 sont mémorisées automatiquement dans `UserData\MelonPreferences.cfg`. Les commandes du jeu sont bloquées tant qu'une fenêtre F6, F8 ou F9 est ouverte.

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

## Incidents partagés

### Côté hôte

L'hôte lit les crimes en cours et envoie environ une fois par seconde :

- le type de crime ;
- la position de l'incident ;
- le nombre de suspects ;
- le nombre de suspects résolus.

### Côté client

À la connexion, les crimes locaux déjà en cours sont effacés et la génération locale des crimes et des appels radio est désactivée. Le client reçoit alors les incidents de l'hôte dans un panneau **APPELS EN COURS (hôte)**, en haut à droite, trié par distance.

Exemple :

```text
Vehicle Theft — 320 m — 0/2 suspects [HÔTE]
```

Un marqueur `▼` est affiché au-dessus de chaque lieu d'incident. La mention `[HÔTE]` indique l'appel accepté par l'hôte.

À la déconnexion, la génération locale des crimes et des appels radio est réactivée.

Les noms des crimes correspondent actuellement aux noms internes du jeu et apparaissent donc en anglais, par exemple `Vehicle Theft`.

## Synchronisation du monde — v0.4.1

L'hôte partage désormais son heure, sa météo et sa vitesse d'écoulement du temps avec les clients. Cette étape utilise les fonctions exposées par `TimeOfDayManager` : `GetTimeInHours`, `SetTimeInHours`, `GetPerceivedWeatherType`, `StartWeatherChange`, `SetStaticWeather` et la vitesse du temps.

### Côté hôte

L'hôte lit et envoie toutes les deux secondes :

- l'heure du jeu ;
- la météo actuelle ;
- la vitesse d'écoulement du temps.

Un état complet est également envoyé immédiatement lorsqu'un joueur rejoint la session.

### Côté client

- Les changements de météo aléatoires locaux sont arrêtés pendant la session.
- La météo suit celle de l'hôte avec une transition progressive, comme dans le jeu.
- L'heure avance normalement chez chaque joueur.
- L'heure locale n'est recalée que si elle s'écarte de plus de 10 minutes de jeu de celle de l'hôte, afin d'éviter les changements permanents de luminosité.
- Lors de la connexion, l'heure est immédiatement réglée sur celle de l'hôte.
- À la déconnexion, la météo aléatoire et la vitesse du temps d'origine du client sont restaurées.

Le HUD affiche une ligne `Monde :`, par exemple :

```text
Monde : 22h15, forte pluie
```

Le paramètre utilisé pour le changement de météo doit encore être confirmé en jeu. Il est actuellement interprété comme indiquant un changement instantané et désactivé afin de conserver une transition progressive. Si la météo ne change pas ou bascule immédiatement, le comportement devra être inversé.

## Suspects partagés

L'hôte envoie environ 10 fois par seconde les informations de chaque suspect :

- position et orientation ;
- statut ;
- apparence ;
- animation ;
- véhicule si le suspect est au volant.

Le client tente de représenter le suspect avec la même apparence lorsqu'un passant identique existe localement. Sinon, il utilise un passant quelconque, puis une capsule rouge si aucun modèle ne peut être créé.

Les animations principales sont reproduites côté client, notamment la course, la reddition et la chute. Lorsqu'un suspect est en voiture, le client utilise le véritable modèle du véhicule.

Le statut affiché au-dessus du suspect peut être :

- `se rend` ;
- `menotté` ;
- `arrêté` ;
- `neutralisé`.

À cette étape, les suspects sont des copies visuelles sans collision. Le client ne peut pas encore les menotter, les arrêter ou leur appliquer une action de gameplay. La prochaine étape consistera à envoyer les demandes d'action au serveur hôte, puis à piloter les vrais personnages.

Pour éviter de perdre une mission locale en cours, il est recommandé que le client se connecte depuis une patrouille libre.

## Actions sur les suspects

Le client peut demander une action lorsqu'il est à proximité d'un suspect partagé. Une invite indique les touches disponibles :

| Touche par défaut | Action |
|---|---|
| Y | Donner l'ordre de se rendre |
| U | Plaquer |
| I | Menotter |
| O | Fouiller |
| Tir | Envoyer une demande de tir si la trajectoire touche un suspect |

Les touches Y, U, I et O peuvent être personnalisées dans `MelonPreferences.cfg`. Les actions de proximité sont disponibles à pied ; les distances et les états du suspect sont contrôlés par l'hôte. L'hôte renvoie au client un résultat accepté ou refusé avec un message explicatif.

Les actions actuellement prises en charge sont : ordre de se rendre, plaquage, menottage, fouille et tir. Le client ne pilote pas directement les PNJ de l'hôte : il envoie une demande réseau et l'hôte reste l'autorité sur l'action et ses conséquences.

## Personnaliser les touches

Appuie sur **F6** pour ouvrir « TOUCHES DU MOD ». La fenêtre permet de modifier les quatre touches des actions de proximité :

- ordre de se rendre ;
- plaquer ;
- menotter ;
- fouiller.

Seules les lettres et les chiffres sont acceptés. Une touche ne peut pas être attribuée à deux actions. **Par défaut** restaure les touches `Y`, `U`, `I` et `O`, puis **Enregistrer** mémorise le choix dans `MelonPreferences.cfg`.

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
  ActionProtocol.cs Format des demandes et résultats d'actions
  ClientActions.cs Actions demandées par le client et invite à l'écran
  HostActions.cs   Validation et exécution des actions chez l'hôte
	KeysDialog.cs    Fenêtre F6 de personnalisation des touches
  HostCrimes.cs    Lecture et envoi des incidents de l'hôte
  CrimeSuppression.cs Désactivation des crimes et appels locaux du client
  SharedWorldView.cs Affichage des incidents et suspects partagés
  SharedWorldProtocol.cs Format des données du monde partagé
  WorldSync.cs     Synchronisation de l'heure et de la météo de l'hôte
  Hotkeys.cs       Gestion des raccourcis clavier
mod_dll/
  PrecinctMP.dll   DLL compilée du mod
  LiteNetLib.dll   Dépendance réseau du mod
	README.md        Guide d'installation de MelonLoader et des DLL
```

## Feuille de route

| Version | Objectif | État |
|---|---|---|
| 0.1 | Réseau et positions avec formes simples | ✅ Écrit |
| 0.2 | Classes réelles, modèles des véhicules et états du gyrophare | ✅ Écrit, à tester |
| 0.2.0 | Modèle et animations du policier à pied | ✅ Écrit, à tester |
| 0.2.2 | Synchronisation de l'heure, de la météo et de la vitesse du temps | ✅ Écrit, à compiler et tester |
| 0.3.0 | Incidents et suspects de l'hôte visibles par le client | ✅ Écrit, à compiler et tester |
| 0.4.0 | Actions sur les suspects : se rendre, plaquer, menotter, fouiller et tirer | ✅ Écrit |
| 0.4.1 | Personnalisation des touches des actions avec F6 | ✅ Écrit, à compiler et tester |
| 3.1 | Synchronisation avancée des gyrophares, sirènes et collisions | ⏳ Prévu |
| 4.0 | Progression complète des incidents et du monde partagé | ⏳ Prévu |
| Plus tard | Trafic, PNJ, heure et météo synchronisés | ⏳ Prévu |

## Limites et avertissements

- Le protocole n'est pas chiffré.
- Un client peut envoyer de fausses positions.
- Une mise à jour du jeu ou de MelonLoader peut casser le mod.
- Une connexion directe par Internet nécessite une redirection UDP et une configuration correcte du pare-feu.
- La récupération de l'IP publique dépend d'un service web externe.
- Les crimes locaux du client sont effacés à la connexion et leur génération est suspendue pendant la session ; une mission en cours peut donc être annulée.
- Les incidents, les suspects, les actions et le monde partagé utilisent le format réseau de la version installée : l'hôte et les clients doivent utiliser exactement la même version.
- La synchronisation de l'heure et de la météo utilise le format réseau de la version installée : l'hôte et les clients doivent utiliser la même version.
- Le sens du paramètre de transition de `StartWeatherChange` reste à confirmer en test réel.
- Les suspects partagés sont visuels côté client et ne possèdent pas de collision locale ; les actions sont exécutées à distance par l'hôte.
- Les noms des crimes sont affichés en anglais jusqu'à l'ajout d'une table de traduction basée sur les noms réellement utilisés par le jeu.
- Au lancement, le log doit indiquer `Blocage des crimes client : 7/7 patchs posés`. Un nombre inférieur à 7 indique qu'il faut fournir `MelonLoader\Latest.log`.
- Les raccourcis sont lus via l'API Windows et fonctionnent lorsque le jeu est au premier plan.
- Le mod est expérimental et n'est pas affilié à Fallen Tree Games ni à Kwalee.
