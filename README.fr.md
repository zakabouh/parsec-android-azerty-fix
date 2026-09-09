# Corriger le clavier AZERTY de Parsec Android sur Windows

[Read in English](README.md)

Correcteur Windows open source pour le problème de **clavier Parsec Android reconnu en QWERTY alors qu'il est en AZERTY** sur le PC distant.

Il corrige les touches `A/Q`, `Z/W` et `M` inversées, les chiffres qui produisent des symboles, la ponctuation et plusieurs caractères spéciaux. Un interrupteur sécurisé par session empêche désormais ce remappage d'affecter les connexions Parsec normales provenant d'un autre ordinateur.

## Est-ce le même problème que le vôtre ?

Ce projet vise les symptômes souvent décrits sur les forums avec les formulations suivantes :

- « Parsec Android utilise un clavier QWERTY au lieu d'AZERTY » ;
- « mon clavier AZERTY est reconnu en QWERTY sur le PC distant » ;
- « les touches A et Q, Z et W sont inversées » ;
- « mauvaise disposition du clavier dans Parsec » ;
- « les chiffres ne fonctionnent pas » ou « les chiffres écrivent des caractères spéciaux » ;
- « impossible de taper le code PIN Windows depuis Parsec Android » ;
- « les caractères spéciaux ne fonctionnent pas » ;
- « la touche Retour arrière fonctionne seulement quand je reste appuyé » ;
- « Retour arrière, Supprimer ou les touches spéciales ne sont pas envoyés » ;
- « clavier Samsung DeX ou Galaxy Tab mal configuré dans Parsec » ;
- « clavier physique, Bluetooth ou USB mélangé dans Parsec Android ».

Le programme fonctionne sur le **PC Windows hôte après l'établissement de la connexion Parsec**. Il ne modifie pas l'application Android elle-même.

## Questions souvent recherchées

### Pourquoi Parsec Android écrit-il en QWERTY sur mon PC AZERTY ?

Parsec peut transmettre les positions de touches Android/US alors que le PC Windows les interprète avec sa disposition française. Le clavier AZERTY se comporte alors comme un QWERTY, notamment avec `A/Q` et `Z/W` inversés. Le correcteur traduit ces événements distants avant leur arrivée dans les applications Windows.

### Pourquoi les chiffres écrivent-ils des symboles dans Parsec ?

Les rangées de chiffres américaine et française n'utilisent pas Maj de la même manière. Lorsque Windows interprète des codes US comme de l'AZERTY français, `1`, `2`, `3` et les autres chiffres peuvent devenir `&`, `é`, `"` ou d'autres caractères. Le correcteur envoie directement le chiffre ou le signe attendu.

### Pourquoi Retour arrière fonctionne-t-il seulement en restant appuyé ?

Certains claviers Android transmettent Retour arrière de façon irrégulière dans Parsec. Le programme conserve les événements normaux et propose également `` `b `` comme solution de secours. Il ne peut pas récupérer un appui si le client Android n'envoie absolument aucun événement.

## Limites

- Le programme ne corrige pas la saisie dans l'écran de connexion de l'application Parsec Android.
- Il ne fonctionne pas sur l'écran sécurisé de connexion ou de code PIN Windows avant l'ouverture de la session utilisateur.
- Il ne peut pas deviner une touche si Android et Parsec ne transmettent aucun événement ; les raccourcis de composition servent de solution de secours.
- Les problèmes de touche Entrée, souris/clic droit et manette ne font pas partie de ce correcteur de disposition clavier.
- Le doublement des lettres causé par certaines configurations SwiftKey est un problème Android distinct.
- Si Android et un autre ordinateur contrôlent simultanément l'hôte, Parsec n'expose pas assez de métadonnées pour appliquer une disposition différente à chaque événement clavier.

## Corrections apportées

- Conversion des positions QWERTY Android/US vers l'AZERTY français pour `A/Q`, `Z/W` et `M`.
- Conversion de la rangée des chiffres et de la ponctuation US avant leur mauvaise interprétation par la disposition française de Windows.
- Touche de composition pour les symboles Unicode que Parsec Android ne transmet pas comme événements Windows exploitables.
- Raccourcis de secours facultatifs pour Retour arrière et Supprimer.
- Remappage limité aux événements injectés après l'activation du mode Android pour la session Parsec courante.
- Aucun changement du vrai clavier Windows ni de la souris locale.

Parsec présente officiellement son application Android comme expérimentale et indique que les entrées clavier et souris peuvent parfois mal fonctionner. Voir [Installer l'application Parsec sur Android](https://support.parsec.app/hc/en-us/articles/32381582866452-Install-Parsec-App-on-Android).

## Installation

1. Téléchargez [`ParsecAzertyFix-Setup.exe`](https://github.com/zakabouh/parsec-android-azerty-fix/releases/latest/download/ParsecAzertyFix-Setup.exe).
2. Lancez l'installateur.
3. Connectez-vous au PC avec Parsec Android.
4. Activez **Mode Android pour cette session** depuis l'icône de notification, ou appuyez sur `Ctrl+Alt+A` depuis Android.

L'installation ne demande aucun droit administrateur. Elle est effectuée pour l'utilisateur Windows courant dans :

```text
%LOCALAPPDATA%\ParsecAzertyFix\ParsecAzertyFix.exe
```

Le programme est ajouté à `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` pour démarrer automatiquement à chaque ouverture de session Windows.

> L'exécutable n'est pas signé numériquement. Windows SmartScreen peut donc afficher un avertissement « Éditeur inconnu ». Le code source complet et le script de construction reproductible sont disponibles dans ce dépôt.

## Utilisation

Chaque nouvelle connexion Parsec commence en **Mode standard — aucune correction**. Le clavier reste donc inchangé lorsque le même PC hôte est contrôlé depuis un autre ordinateur Windows, macOS ou Linux.

Depuis Android, activez **Mode Android pour cette session** dans l'icône de notification ou appuyez sur `Ctrl+Alt+A`. Le programme revient automatiquement au mode standard quand la session Parsec se déconnecte. `Ctrl+Alt+F12` constitue un autre raccourci pour les clients disposant de touches de fonction.

L'icône indique le mode courant. Son menu permet également de désactiver complètement le correcteur ou de quitter le programme.

### Raccourcis de composition pour les caractères absents

Certains symboles du clavier Android n'arrivent jamais sur l'hôte Windows sous forme d'événements exploitables. Tapez un accent grave `` ` ``, puis la touche indiquée :

| Séquence | Résultat | Séquence | Résultat |
|---|---:|---|---:|
| `` `e `` | `€` | `` `l `` | `£` |
| `` `y `` | `¥` | `` `c `` | `¢` |
| `` `o `` | `©` | `` `r `` | `®` |
| `` `t `` | `™` | `` `? `` | `¿` |
| `` `! `` | `¡` | `` `/ `` | `÷` |
| `` `\| `` | `¦` | `` `- `` | `¬` |
| `` `x `` | `×` | `` `s `` | `§` |
| `` `p `` | `¶` | `` `d `` | `°` |
| `` `b `` | Retour arrière | `` `u `` | Supprimer |
| <kbd>`</kbd> <kbd>`</kbd> | Accent grave normal | | |

Une composition incomplète expire après cinq secondes et produit un accent grave normal.

## Désinstallation

Ouvrez **Paramètres Windows > Applications > Applications installées**, recherchez **Parsec Android AZERTY Fix**, puis choisissez **Désinstaller**.

## Construction depuis les sources

Sous Windows 10 ou Windows 11 avec .NET Framework 4.x :

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

Le dossier `artifacts\` contiendra :

- `ParsecAzertyFix.exe` — correcteur portable ;
- `ParsecAzertyFix-Setup.exe` — installateur autonome par utilisateur ;
- `SHA256SUMS.txt` — empreintes SHA-256.

## Confidentialité et sécurité

- Utilise le hook clavier bas niveau de Windows (`WH_KEYBOARD_LL`) et des appels locaux à `SendInput`.
- Lit `%APPDATA%\Parsec\log.txt` uniquement pour déterminer si une session Parsec est connectée.
- N'effectue aucune connexion réseau et ne contient ni télémétrie ni collecte de données.
- Ne demande aucun droit administrateur.

Ce projet est un contournement communautaire non officiel, sans affiliation avec Parsec.

## Licence

[MIT](LICENSE)
