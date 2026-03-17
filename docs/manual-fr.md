# Window Resize for Windows — Guide de l'utilisateur

## Table des matières

1. [Pour commencer](#pour-commencer)
2. [Redimensionner une fenêtre](#redimensionner-une-fenêtre)
3. [Paramètres](#paramètres)
4. [Dépannage](#dépannage)

---

## Pour commencer

1. Lancez **WindowsResizeCapture.exe**. Un écran de démarrage s'affiche brièvement.
2. L'icône de l'application apparaît dans la **zone de notification** (en bas à droite de la barre des tâches).
3. Cliquez sur l'icône pour ouvrir le menu.

> **Remarque :** Aucune autorisation spéciale n'est requise. L'application fonctionne immédiatement après le lancement.

---

## Redimensionner une fenêtre

### Étapes

1. Cliquez sur l'**icône Window Resize** dans la zone de notification.
2. Survolez **« Redimensionner »** pour ouvrir la liste des fenêtres.
3. Toutes les fenêtres ouvertes sont affichées avec leur **icône d'application** et leur nom au format **[Nom de l'app] Titre de la fenêtre**. Les titres longs sont automatiquement tronqués.
4. Survolez une fenêtre pour voir les tailles prédéfinies disponibles.
5. Cliquez sur une taille pour redimensionner la fenêtre immédiatement.

### Format d'affichage des tailles

Chaque entrée de taille dans le menu affiche :

```
1920 x 1080          Full HD
```

- **Gauche :** Largeur x Hauteur (en pixels)
- **Droite :** Étiquette (nom standard), affichée en gris

### Tailles dépassant l'écran

Si une taille prédéfinie est supérieure à la résolution de l'écran où se trouve la fenêtre, cette taille sera **grisée et non sélectionnable**.

---

## Paramètres

Cliquez sur l'icône Window Resize dans la zone de notification, puis sélectionnez **« Paramètres... »** pour ouvrir la fenêtre des paramètres.

### Tailles intégrées

L'application comprend 12 tailles prédéfinies intégrées :

| Taille | Étiquette |
|--------|-----------|
| 3840 x 2160 | 4K UHD |
| 2560 x 1440 | QHD |
| 1920 x 1200 | WUXGA |
| 1920 x 1080 | Full HD |
| 1680 x 1050 | WSXGA+ |
| 1600 x 900 | HD+ |
| 1440 x 900 | WXGA+ |
| 1366 x 768 | WXGA |
| 1280 x 1024 | SXGA |
| 1280 x 720 | HD |
| 1024 x 768 | XGA |
| 800 x 600 | SVGA |

Les tailles intégrées ne peuvent pas être supprimées ni modifiées.

### Tailles personnalisées

Vous pouvez ajouter vos propres tailles :

1. Saisissez la **Largeur** et la **Hauteur** en pixels.
2. Cliquez sur **« Ajouter »**.
3. La nouvelle taille est ajoutée à la liste et immédiatement disponible dans le menu de redimensionnement.

Pour supprimer une taille personnalisée, cliquez sur le bouton **« Supprimer »** à côté.

### Lancer à la connexion

Activez **« Lancer à la connexion »** pour que Window Resize démarre automatiquement lorsque vous vous connectez à Windows.

### Capture d'écran

Activez **« Capturer l'écran après le redimensionnement »** pour capturer automatiquement la fenêtre après le redimensionnement.

Lorsque cette option est activée, les options suivantes sont disponibles :

- **Enregistrer dans un fichier** — Enregistrer la capture au format PNG dans le dossier choisi.
  > **Format du nom :** `MMddHHmmss_NomApp_TitreFenêtre.png` (ex. : `0227193012_chrome_Google.png`). Les symboles sont supprimés.
- **Copier dans le presse-papiers** — Copier la capture dans le presse-papiers pour coller dans d'autres applications.

Les deux options peuvent être activées indépendamment.

---

## Dépannage

### Échec du redimensionnement

Si le message « Échec du redimensionnement » apparaît :

- La fenêtre cible ne prend pas en charge le redimensionnement externe.
- La fenêtre est en **mode plein écran** (appuyez sur **F11** ou **Échap** pour quitter d'abord).

### La fenêtre n'apparaît pas dans la liste

Le menu de redimensionnement n'affiche que les fenêtres qui :

- Sont actuellement visibles à l'écran
- Possèdent une barre de titre
- Ne sont pas les fenêtres de l'application Window Resize elle-même

Les fenêtres réduites n'apparaîtront pas dans la liste.

### La capture d'écran ne fonctionne pas

Si les captures d'écran ne sont pas réalisées :

- Vérifiez qu'au moins **« Enregistrer dans un fichier »** ou **« Copier dans le presse-papiers »** est activé dans les Paramètres.
- Si vous enregistrez dans un fichier, vérifiez que le dossier de destination existe et est accessible en écriture.
