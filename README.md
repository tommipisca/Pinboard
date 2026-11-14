# Pinboard

App per Windows che mette widget sul desktop, nello stile dei widget di macOS. Non stanno nel
pannello widget di Windows: stanno sul desktop e restano visibili anche con "Mostra desktop"
(Win+D o pulsante in fondo alla barra delle applicazioni).

## Dashboard

La finestra principale si apre avviando `Pinboard.exe` (o con un clic sull'icona accanto
all'orologio di Windows). Chiudendola, Pinboard resta attivo e i widget restano sul desktop.

- **Catalogo**: tutti i widget disponibili, con anteprima funzionante, filtri per categoria e per
  preferiti (la stella su ogni scheda) e ricerca. Al primo avvio si apre qui.
- **Dettaglio**: clic su un widget per vederlo in grande, nelle sue taglie, e aggiungerlo.
- **Personalizzazione**: prima di aggiungere un widget (e poi con "Personalizza") si scelgono
  taglia, tema (come Windows, chiaro, scuro), colore di sfondo, colore d'accento, trasparenza e
  le opzioni proprie del widget. L'anteprima si aggiorna mentre si cambia.
- **Sul desktop**: i widget aggiunti, da personalizzare, rimuovere o spostare. Lo stesso widget
  si puo' aggiungere piu' volte (per esempio due orologi con città diverse).
- **Impostazioni**: avvio con Windows, sposta widget, esci.

## Widget

| Widget | Taglie | Opzioni proprie |
| --- | --- | --- |
| Orologio | piccolo (analogico), medio (analogico + ora in cifre, città e data) | ora del computer o di un'altra città/fuso orario, lancetta dei secondi |
| Calendario | piccolo (giorno della settimana, numero, mese), medio (anche il mese intero) | — |

Lo stile segue i widget di macOS: scheda con angoli molto arrotondati, sfondo semitrasparente,
tipografia sottile. Lo sfondo non e' sfocato (Mica/Acrylic non funzionano su finestre trasparenti
di WPF).

### Aggiungere un widget

Ogni tipo di widget e' una classe che estende `WidgetDefinition` (in `src/Pinboard/Widgets/`):
nome, descrizione, categoria, taglie, colore d'accento predefinito, la vista (`WidgetView`) e,
se servono, i controlli per le opzioni proprie. Poi va aggiunto a `WidgetCatalog.All`.

## Eseguibile

Serve il .NET 8 SDK solo per compilare. Da PowerShell, nella cartella della repo:

```powershell
.\publish.ps1
```

Crea `dist\Pinboard.exe`, un unico file che funziona anche senza .NET installato.

Per provare senza creare l'eseguibile: `dotnet run --project src/Pinboard`.

## Dati

Widget, personalizzazioni, posizioni e preferiti stanno in `%APPDATA%\Pinboard\settings.json`.
Il vecchio `widgets.json` (orologio e calendario fissi) viene convertito da solo al primo avvio.

Opzioni da riga di comando:
- `--background`: parte senza aprire la dashboard (lo usa l'avvio con Windows).
- `--floating`: finestre normali in primo piano, per provare il disegno.
- `--debug`: log in `%APPDATA%\Pinboard\pinboard.log`.

La variabile d'ambiente `PINBOARD_HOME` sposta i dati in un'altra cartella, utile per provare
senza toccare i propri widget.

## Icona

`src/Pinboard/Assets/Pinboard.ico` e' l'icona dell'eseguibile e dell'area di notifica. Si rigenera
(insieme a `assets/pinboard-icon-1024.png`) con `python3 assets/make_icon.py`, che richiede Pillow.

## Come restano sul desktop

Ogni widget e' una finestra di primo livello tenuta subito sopra il desktop e sotto tutte le
altre finestre. Quando "Mostra desktop" porta il desktop in primo piano, i widget diventano
temporaneamente "sempre in primo piano" e restano visibili; quando torna attiva un'altra
finestra, ridiventano normali. Il codice e' in `src/Pinboard/Core/DesktopHost.cs`.

L'aggancio come figlio del desktop (`SetParent` su `Progman`/`WorkerW`, la tecnica di Lively) e'
stato provato ma su Windows 11 24H2 il contenuto non viene disegnato, quindi non e' usato.

Limite noto: i widget stanno sopra le icone del desktop e le coprono se si sovrappongono.
