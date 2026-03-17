# Window Resize for Windows — Benutzerhandbuch

## Inhaltsverzeichnis

1. [Erste Schritte](#erste-schritte)
2. [Fenstergröße ändern](#fenstergröße-ändern)
3. [Einstellungen](#einstellungen)
4. [Fehlerbehebung](#fehlerbehebung)

---

## Erste Schritte

1. Starten Sie **WindowsResizeCapture.exe**. Ein Startbildschirm wird kurz angezeigt.
2. Das App-Symbol erscheint im **Infobereich** (Benachrichtigungsbereich unten rechts in der Taskleiste).
3. Klicken Sie auf das Infobereich-Symbol, um das Menü zu öffnen.

> **Hinweis:** Es sind keine speziellen Berechtigungen erforderlich. Die App funktioniert sofort nach dem Start.

---

## Fenstergröße ändern

### Schritt für Schritt

1. Klicken Sie auf das **Window Resize-Symbol** im Infobereich.
2. Bewegen Sie den Mauszeiger über **„Größe ändern"**, um die Fensterliste zu öffnen.
3. Alle derzeit geöffneten Fenster werden mit ihrem **App-Symbol** und Namen im Format **[App-Name] Fenstertitel** angezeigt. Lange Titel werden automatisch gekürzt.
4. Bewegen Sie den Mauszeiger über ein Fenster, um die verfügbaren Voreinstellungen zu sehen.
5. Klicken Sie auf eine Größe, um das Fenster sofort anzupassen.

### Größenanzeige

Jeder Größeneintrag im Menü zeigt:

```
1920 x 1080          Full HD
```

- **Links:** Breite x Höhe (in Pixeln)
- **Rechts:** Bezeichnung (Standardname), grau angezeigt

### Voreinstellungen, die den Bildschirm überschreiten

Wenn eine Voreinstellung größer als die Auflösung des Bildschirms ist, auf dem sich das Fenster befindet, wird diese Größe **ausgegraut und nicht auswählbar**.

---

## Einstellungen

Klicken Sie auf das Window Resize-Infobereich-Symbol und wählen Sie **„Einstellungen..."**, um das Einstellungsfenster zu öffnen.

### Integrierte Größen

Die App enthält 12 integrierte Voreinstellungen:

| Größe | Bezeichnung |
|-------|-------------|
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

Integrierte Größen können nicht gelöscht oder bearbeitet werden.

### Benutzerdefinierte Größen

Sie können eigene Größen hinzufügen:

1. Geben Sie **Breite** und **Höhe** in Pixeln ein.
2. Klicken Sie auf **„Hinzufügen"**.
3. Die neue Größe wird zur Liste hinzugefügt und ist sofort im Größenänderungsmenü verfügbar.

Um eine benutzerdefinierte Größe zu entfernen, klicken Sie auf die Schaltfläche **„Entfernen"** daneben.

### Bei Anmeldung starten

Aktivieren Sie **„Bei Anmeldung starten"**, damit Window Resize automatisch startet, wenn Sie sich bei Windows anmelden.

### Screenshot

Aktivieren Sie **„Screenshot nach Größenänderung aufnehmen"**, um das Fenster nach der Größenänderung automatisch aufzunehmen.

Bei Aktivierung stehen folgende Optionen zur Verfügung:

- **In Datei speichern** — Screenshot als PNG-Datei im gewählten Ordner speichern.
  > **Dateinamenformat:** `MMddHHmmss_AppName_Fenstertitel.png` (z.B. `0227193012_chrome_Google.png`). Sonderzeichen werden entfernt.
- **In Zwischenablage kopieren** — Screenshot in die Zwischenablage kopieren, um ihn in andere Apps einzufügen.

Beide Optionen können unabhängig aktiviert werden.

---

## Fehlerbehebung

### Größenänderung fehlgeschlagen

Wenn die Meldung „Größenänderung fehlgeschlagen" erscheint:

- Das Zielfenster unterstützt keine externe Größenänderung.
- Das Fenster befindet sich im **Vollbildmodus** (drücken Sie **F11** oder **Esc** zum Beenden).

### Fenster erscheint nicht in der Liste

Das Größenänderungsmenü zeigt nur Fenster, die:

- Derzeit auf dem Bildschirm sichtbar sind
- Eine Titelleiste haben
- Nicht die eigenen Fenster der Window Resize-App sind

Minimierte Fenster werden nicht in der Liste angezeigt.

### Screenshot funktioniert nicht

Wenn keine Screenshots aufgenommen werden:

- Stellen Sie sicher, dass mindestens **„In Datei speichern"** oder **„In Zwischenablage kopieren"** in den Einstellungen aktiviert ist.
- Überprüfen Sie bei Dateispeicherung, ob der Speicherordner existiert und beschreibbar ist.
