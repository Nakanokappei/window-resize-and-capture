# Window Resize for Windows — Manuale utente

## Indice

1. [Per iniziare](#per-iniziare)
2. [Ridimensionare una finestra](#ridimensionare-una-finestra)
3. [Impostazioni](#impostazioni)
4. [Risoluzione dei problemi](#risoluzione-dei-problemi)

---

## Per iniziare

1. Esegui **WindowsResizeCapture.exe**. Viene visualizzata brevemente una schermata di avvio.
2. L'icona dell'app appare nell'**area di notifica** (in basso a destra nella barra delle applicazioni).
3. Fai clic sull'icona per aprire il menu.

> **Nota:** Non sono necessarie autorizzazioni speciali. L'app funziona immediatamente dopo l'avvio.

---

## Ridimensionare una finestra

### Procedura

1. Fai clic sull'**icona Window Resize** nell'area di notifica.
2. Passa il mouse su **"Ridimensiona"** per aprire l'elenco delle finestre.
3. Tutte le finestre aperte vengono visualizzate con la loro **icona dell'app** e il nome nel formato **[Nome app] Titolo finestra**. I titoli lunghi vengono troncati automaticamente.
4. Passa il mouse su una finestra per vedere le dimensioni preimpostate disponibili.
5. Fai clic su una dimensione per ridimensionare immediatamente la finestra.

### Formato di visualizzazione delle dimensioni

Ogni voce nel menu mostra:

```
1920 x 1080          Full HD
```

- **Sinistra:** Larghezza x Altezza (in pixel)
- **Destra:** Etichetta (nome standard), visualizzata in grigio

### Dimensioni che superano lo schermo

Se una dimensione preimpostata è superiore alla risoluzione dello schermo in cui si trova la finestra, tale dimensione sarà **disattivata e non selezionabile**.

---

## Impostazioni

Fai clic sull'icona Window Resize nell'area di notifica, quindi seleziona **"Impostazioni..."** per aprire la finestra delle impostazioni.

### Dimensioni integrate

L'app include 12 dimensioni preimpostate integrate:

| Dimensione | Etichetta |
|------------|-----------|
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

Le dimensioni integrate non possono essere eliminate o modificate.

### Dimensioni personalizzate

Puoi aggiungere le tue dimensioni:

1. Inserisci **Larghezza** e **Altezza** in pixel.
2. Fai clic su **"Aggiungi"**.
3. La nuova dimensione viene aggiunta all'elenco ed è immediatamente disponibile nel menu di ridimensionamento.

Per rimuovere una dimensione personalizzata, fai clic sul pulsante **"Rimuovi"** accanto ad essa.

### Avvio all'accesso

Attiva **"Avvio all'accesso"** per far avviare automaticamente Window Resize quando accedi a Windows.

### Screenshot

Attiva **"Cattura screenshot dopo il ridimensionamento"** per catturare automaticamente la finestra dopo il ridimensionamento.

Quando attivato, sono disponibili le seguenti opzioni:

- **Salva su file** — Salva lo screenshot come file PNG nella cartella scelta.
  > **Formato nome file:** `MMddHHmmss_NomeApp_TitoloFinestra.png` (es.: `0227193012_chrome_Google.png`). I simboli vengono rimossi.
- **Copia negli appunti** — Copia lo screenshot negli appunti per incollarlo in altre app.

Entrambe le opzioni possono essere attivate in modo indipendente.

---

## Risoluzione dei problemi

### Ridimensionamento non riuscito

Se appare il messaggio "Ridimensionamento non riuscito":

- La finestra di destinazione non supporta il ridimensionamento esterno.
- La finestra è in **modalità a schermo intero** (premi **F11** o **Esc** per uscire prima).

### La finestra non appare nell'elenco

Il menu di ridimensionamento mostra solo le finestre che:

- Sono attualmente visibili sullo schermo
- Hanno una barra del titolo
- Non sono le finestre dell'app Window Resize stessa

Le finestre ridotte a icona non appariranno nell'elenco.

### Lo screenshot non funziona

Se gli screenshot non vengono catturati:

- Assicurati che almeno **"Salva su file"** o **"Copia negli appunti"** sia attivato nelle Impostazioni.
- Se salvi su file, verifica che la cartella di destinazione esista e sia scrivibile.
