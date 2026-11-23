# Installer MelonLoader et PrecinctMP

Ce guide explique comment installer **MelonLoader** dans *The Precinct*, puis copier la DLL de PrecinctMP dans le dossier `Mods` du jeu.

## 1. Prérequis

- Une installation légitime de *The Precinct* ;
- Windows ;
- le fichier `PrecinctMP.dll` compilé ;
- `LiteNetLib.dll`, fourni avec le mod si cette dépendance n'est pas déjà présente ;
- les droits d'écriture dans le dossier du jeu.

Le chemin utilisé dans les exemples est :

```text
G:\SteamLibrary\steamapps\common\The Precinct
```

Remplace-le par le chemin réel de ton installation.

## 2. Installer MelonLoader

1. Télécharge MelonLoader depuis sa page officielle :
   [github.com/LavaGang/MelonLoader](https://github.com/LavaGang/MelonLoader)
2. Lance `MelonLoader.Installer.exe` en tant qu'administrateur si nécessaire.
3. Dans l'installateur, sélectionne l'exécutable de *The Precinct*.

   Exemple :

   ```text
   G:\SteamLibrary\steamapps\common\The Precinct\ThePrecinct.exe
   ```

4. Vérifie que le type de jeu sélectionné correspond à la version installée. Pour ce projet, le jeu utilise **IL2CPP**.
5. Clique sur **Install**.
6. Lance le jeu une première fois avec MelonLoader.
7. Attends d'arriver au menu principal, puis ferme le jeu.

Cette première exécution permet à MelonLoader de créer ses dossiers et de générer les assemblages IL2CPP nécessaires au mod.

Après cette étape, le dossier suivant doit exister :

```text
G:\SteamLibrary\steamapps\common\The Precinct\MelonLoader\Il2CppAssemblies
```

## 3. Installer PrecinctMP.dll

### Installation manuelle

1. Ouvre le dossier de sortie du projet Visual Studio, généralement :

   ```text
   PrecinctMP\bin\Release\net6.0\
   ```

2. Repère le fichier :

   ```text
   PrecinctMP.dll
   ```

3. Copie-le dans le dossier `Mods` du jeu :

   ```text
   G:\SteamLibrary\steamapps\common\The Precinct\Mods\
   ```

4. Si le dossier `Mods` n'existe pas, crée-le exactement avec ce nom.

Si le projet a été compilé avec son script de copie automatique, `PrecinctMP.dll` se trouve déjà dans `Mods`. Dans ce cas, aucune copie manuelle n'est nécessaire.

## 4. Installer LiteNetLib.dll

PrecinctMP utilise également `LiteNetLib.dll`. Si elle n'est pas déjà installée, copie-la dans :

```text
G:\SteamLibrary\steamapps\common\The Precinct\UserLibs\
```

Crée le dossier `UserLibs` s'il n'existe pas.

L'installation finale doit ressembler à ceci :

```text
The Precinct\
├── ThePrecinct.exe
├── Mods\
│   └── PrecinctMP.dll
├── UserLibs\
│   └── LiteNetLib.dll
└── MelonLoader\
	└── Il2CppAssemblies\
```

## 5. Vérifier l'installation

1. Lance *The Precinct*.
2. Attends le menu principal ou charge une partie.
3. Appuie sur **F11** pour afficher ou masquer le HUD.
4. Appuie sur **F8** pour ouvrir la fenêtre d'hébergement.
5. Appuie sur **F9** pour ouvrir la fenêtre de connexion.

Si F8 ou F9 fonctionne, MelonLoader a chargé le mod.

Le fichier de log principal se trouve ici :

```text
The Precinct\MelonLoader\Latest.log
```

## 6. En cas de problème

### Les fenêtres F8 et F9 n'apparaissent pas

Vérifie que :

- `PrecinctMP.dll` est bien dans `The Precinct\Mods\` ;
- le jeu est lancé avec MelonLoader ;
- la DLL correspond à la version actuelle du projet ;
- `LiteNetLib.dll` est bien dans `UserLibs` ;
- `MelonLoader\Latest.log` ne contient pas d'erreur de chargement.

### Le dossier `Il2CppAssemblies` est absent

Lance le jeu une fois avec MelonLoader et attends le menu principal. Si le dossier n'est toujours pas créé, vérifie l'installation de MelonLoader et le type IL2CPP sélectionné dans l'installateur.

### Le jeu plante au démarrage

Retire temporairement `PrecinctMP.dll` du dossier `Mods`, relance le jeu et consulte :

```text
The Precinct\MelonLoader\Latest.log
```

Ne copie pas le dossier `src` dans le dossier du jeu : `src` contient le code source du projet. Seules les DLL compilées doivent être installées dans `Mods` et `UserLibs`.
