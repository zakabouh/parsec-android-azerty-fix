# Corriger le clavier AZERTY de Parsec Android sur Windows

[Read in English](README.md)

Correcteur Windows open source pour le problème de **clavier Parsec Android reconnu en QWERTY alors qu'il est en AZERTY** sur le PC distant.

Il corrige les touches `A/Q`, `Z/W` et `M` inversées, les chiffres qui produisent des symboles, la ponctuation et plusieurs caractères spéciaux. Le programme distingue automatiquement les appuis quasi instantanés du clavier tactile Parsec Android des durées normales d'un clavier physique. Il n'y a ni apprentissage d'appareil ni raccourci clavier, et les connexions Parsec provenant d'un autre ordinateur restent inchangées.

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
- Si Android et un autre ordinateur contrôlent simultanément l'hôte, le programme reste en mode standard. Parsec n'indique pas quel client a produit chaque événement clavier.
- Une macro ou un autre clavier virtuel distant produisant des appuis de moins de 25 ms peut ressembler au clavier tactile Android. Le menu de notification permet de relancer la détection ou de forcer Android pour la session actuelle.
- Un clavier physique Bluetooth ou USB relié à Android produit des durées humaines. S'il subit le même problème de disposition, choisissez **Forcer Android pour cette session** dans le menu de notification.

## Corrections apportées

- Conversion des positions QWERTY Android/US vers l'AZERTY français pour `A/Q`, `Z/W` et `M`.
- Conversion de la rangée des chiffres et de la ponctuation US avant leur mauvaise interprétation par la disposition française de Windows.
- Touche de composition pour les symboles Unicode que Parsec Android ne transmet pas comme événements Windows exploitables.
- Raccourcis de secours facultatifs pour Retour arrière et Supprimer.
- Détection automatique du clavier tactile Android au début de chaque session Parsec ne comportant qu'un client.
- Deux pressions de durée physique, ou des touches qui se chevauchent, sont nécessaires pour classer un ordinateur standard ; un tap Android caractéristique ou sa séquence Maj synthétique suffit à activer la correction.
- Aucun changement du vrai clavier Windows ni de la souris locale.

Parsec présente officiellement son application Android comme expérimentale et indique que les entrées clavier et souris peuvent parfois mal fonctionner. Voir [Installer l'application Parsec sur Android](https://support.parsec.app/hc/en-us/articles/32381582866452-Install-Parsec-App-on-Android).

## Installation

1. Téléchargez **[`ParsecAzertyFix-INSTALLER.exe`](https://github.com/zakabouh/parsec-android-azerty-fix/releases/latest/download/ParsecAzertyFix-INSTALLER.exe)**. C'est le fichier recommandé pour presque tout le monde.
2. Lancez l'installateur.
3. Connectez-vous normalement depuis Android ou un autre ordinateur. La détection est automatique.

L'installation ne demande aucun droit administrateur. Elle est effectuée pour l'utilisateur Windows courant dans :

```text
%LOCALAPPDATA%\ParsecAzertyFix\ParsecAzertyFix.exe
```

Le programme est ajouté à `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` pour démarrer automatiquement à chaque ouverture de session Windows.

> L'exécutable n'est pas signé numériquement. Windows SmartScreen peut donc afficher un avertissement « Éditeur inconnu ». Le code source complet et le script de construction reproductible sont disponibles dans ce dépôt.

## Utilisation

Au début de chaque session ne comportant qu'un client :

- un tap de clavier tactile relâché en moins de 25 ms, ou la séquence Maj synthétique caractéristique d'Android, active la correction ;
- des pressions de durée physique ou des touches simultanées classent le client comme ordinateur standard et ne modifient aucune touche ;
- la déconnexion efface la décision afin d'analyser indépendamment la session suivante.

Pendant cette décision, le programme retarde d'au plus 35 ms les une ou deux premières touches imprimables. Les raccourcis avec Ctrl, Alt ou la touche Windows ne sont jamais retardés pour la détection. Aucun raccourci clavier ni combinaison dépendant de la disposition n'est nécessaire. Le menu permet de relancer la détection, de forcer Android pour la session actuelle, de désactiver complètement le correcteur ou de quitter le programme.

La classification repose sur le comportement des événements et non sur le port réseau. Le port client par défaut de Parsec est pseudo-aléatoire d'après sa documentation : un numéro de port ne peut donc pas identifier Android de façon fiable.

### Pourquoi la détection automatique fonctionne

La couche d'entrée Android de Parsec repose sur le backend Android open source de [libmatoya](https://github.com/snowcone-ltd/libmatoya/tree/master/src/unix/linux/android). Son code distingue le clavier virtuel avec `event.getDeviceId() <= 0`, précise explicitement que plusieurs traductions sont codées en dur pour un clavier EN-US et synthétise des événements Maj pour les symboles concernés. Ces événements arrivent sur l'hôte Windows sous forme de rafale très courte, alors qu'une touche physique reste enfoncée pendant une durée humaine.

Le programme observe uniquement le type et la durée des événements ; il n'enregistre ni les touches ni les caractères saisis. Une touche relâchée en moins de 25 ms, ou la rafale Maj synthétique, identifie le clavier tactile Android. Une touche encore enfoncée après 35 ms est immédiatement transmise sans modification ; deux pressions de ce type classent la session comme standard.

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

- `ParsecAzertyFix-INSTALLER.exe` — installateur recommandé pour l'utilisateur Windows courant ;
- `ParsecAzertyFix-PORTABLE.exe` — version portable pour utilisateurs avancés, sans installation ni démarrage automatique ;
- `SHA256-CHECKSUMS.txt` — empreintes SHA-256 facultatives pour vérifier les fichiers.

## Confidentialité et sécurité

- Utilise le hook clavier bas niveau de Windows (`WH_KEYBOARD_LL`) et des appels locaux à `SendInput`.
- Lit `%APPDATA%\Parsec\log.txt` uniquement pour détecter les connexions et déconnexions Parsec.
- N'enregistre aucune touche, aucun texte saisi, aucune adresse de client et aucun identifiant d'appareil.
- N'effectue aucune connexion réseau et ne contient ni télémétrie ni collecte de données.
- Ne demande aucun droit administrateur.

Ce projet est un contournement communautaire non officiel, sans affiliation avec Parsec.

## Licence

[MIT](LICENSE)
