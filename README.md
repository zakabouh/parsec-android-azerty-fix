# Parsec Android AZERTY Fix

Correcteur Windows leger pour les problemes de clavier rencontres avec le client **Parsec sur Android** et un PC hote configure en **francais AZERTY**.

Il corrige notamment :

- les lettres `A/Q`, `Z/W` et `M` recues comme si le clavier Android etait QWERTY ;
- les chiffres et la ponctuation de la disposition US envoyee par Parsec ;
- plusieurs caracteres Unicode que le clavier Android n'envoie pas directement ;
- Retour arriere et Supprimer au moyen de raccourcis de composition facultatifs.

Le correcteur ne modifie pas la disposition Windows. Il reste en attente dans la zone de notification et ne traite que les evenements injectes pendant une session Parsec detectee. Le clavier physique et la souris locale restent donc inchanges.

## Installation

1. Telechargez `ParsecAzertyFix-Setup.exe` depuis la [derniere release](https://github.com/zakabouh/parsec-android-azerty-fix/releases/latest).
2. Lancez l'installateur.
3. Reconnectez-vous au PC avec Parsec Android.

L'installation est effectuee uniquement pour l'utilisateur courant, sans droits administrateur :

```text
%LOCALAPPDATA%\ParsecAzertyFix\ParsecAzertyFix.exe
```

Le programme est ajoute a `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` afin de demarrer automatiquement a chaque ouverture de session Windows.

> L'executable n'est pas signe numeriquement. Windows SmartScreen peut donc afficher un avertissement d'editeur inconnu. Le code source et le script de construction sont fournis dans ce depot.

## Utilisation

Une icone apparait dans la zone de notification Windows. Son menu permet de desactiver temporairement la correction ou de quitter le programme.

### Caracteres de composition

Certaines touches Unicode du clavier Android ne produisent aucun evenement exploitable dans Parsec. Le correcteur fournit donc une touche de composition : tapez l'accent grave `` ` ``, puis la touche indiquee.

| Sequence | Resultat | Sequence | Resultat |
|---|---:|---|---:|
| `` `e `` | `€` | `` `l `` | `£` |
| `` `y `` | `¥` | `` `c `` | `¢` |
| `` `o `` | `©` | `` `r `` | `®` |
| `` `t `` | `™` | `` `? `` | `¿` |
| `` `! `` | `¡` | `` `/ `` | `÷` |
| `` `\| `` | `¦` | `` `- `` | `¬` |
| `` `x `` | `×` | `` `s `` | `§` |
| `` `p `` | `¶` | `` `d `` | `°` |
| `` `b `` | Retour arriere | `` `u `` | Supprimer |
| `` `` `` | accent grave litteral | | |

Une composition incomplete est annulee apres cinq secondes et produit un accent grave normal.

## Desinstallation

Ouvrez **Parametres > Applications > Applications installees**, recherchez **Parsec Android AZERTY Fix**, puis choisissez **Desinstaller**.

## Construction depuis les sources

Sur Windows 10 ou Windows 11 avec .NET Framework 4.x :

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

Les fichiers sont generes dans `artifacts\` :

- `ParsecAzertyFix.exe` : correcteur portable ;
- `ParsecAzertyFix-Setup.exe` : installateur autonome contenant le correcteur ;
- `SHA256SUMS.txt` : empreintes SHA-256.

## Fonctionnement et confidentialite

- hook clavier Windows bas niveau (`WH_KEYBOARD_LL`) ;
- reinjection locale avec `SendInput` ;
- detection d'une session active dans `%APPDATA%\Parsec\log.txt` ;
- aucune connexion reseau, telemetrie ou collecte de donnees ;
- aucun droit administrateur requis.

Le support clavier de Parsec Android est experimental et peut varier selon le clavier logiciel, le constructeur du telephone et la version de Parsec. Ce projet est un contournement communautaire non officiel et n'est pas affilie a Parsec.

## English summary

This is an unofficial, per-user Windows workaround for Parsec Android keyboard events received on a French AZERTY host. Download the release installer, run it, and the helper will start automatically at Windows logon. It does not use the network or require administrator rights.

## Licence

[MIT](LICENSE)
