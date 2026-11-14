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

## Download

Dalle [release](https://github.com/tommipisca/Pinboard/releases/latest):
- `Pinboard-Setup-<versione>.exe`: installer. Installa Pinboard per l'utente corrente (senza permessi
  di amministratore) in `%LOCALAPPDATA%\Pinboard`, con collegamento sul desktop e nel menu Start.
  Si disinstalla da Impostazioni di Windows → App.
- `Pinboard-portable-<versione>.zip`: versione portable. Si estrae dove si vuole e si avvia
  `Pinboard.exe`.

Non serve installare .NET. L'installer non e' firmato: se SmartScreen avvisa al primo avvio, fai
clic su **Ulteriori informazioni → Esegui comunque**.

## Aggiornamenti

Pinboard controlla le release di GitHub 15 secondi dopo l'avvio e poi ogni 6 ore. Se trova una
versione nuova la scarica in background (di solito solo le differenze) e la installa quando esci da
Pinboard, oppure subito con **Impostazioni → Aggiornamenti → Riavvia e aggiorna** o dal menu
nell'area di notifica. Vale sia per la versione installata sia per quella portable. Il controllo
automatico si puo' disattivare nella stessa sezione. I dati in `%APPDATA%\Pinboard` non vengono toccati.

Gli aggiornamenti usano [Velopack](https://velopack.io).

## Sviluppo

Serve il .NET 8 SDK. Per provare: `dotnet run --project src/Pinboard` (cosi' gli aggiornamenti
sono disattivati).

### Pubblicare una release

Serve `vpk`, nella stessa versione del pacchetto Velopack in `Pinboard.csproj`:

```powershell
dotnet tool install -g vpk --version 1.2.161
```

1. Aggiorna `<Version>` in `src/Pinboard/Pinboard.csproj`, fai commit e push.
2. Esegui `.\release.ps1` (facoltativo: `-Notes note.md` con le novita' in Markdown).
   Scarica l'ultima release da GitHub per creare il pacchetto delta, poi mette tutti i file in
   `build\release\<versione>`.
3. Crea su GitHub la release con tag `v<versione>` e allega **tutti** i file di quella cartella:
   installer, portable, i `.nupkg`, `releases.win.json` e `RELEASES`.

L'app installata legge l'elenco delle versioni dalle release di GitHub (`releases.win.json`), quindi
ogni release deve includerlo e non deve essere segnata come pre-release.

Per provare un aggiornamento senza pubblicare: crea due versioni con
`.\release.ps1 -Feed <cartella>` (la seconda con `<Version>` piu' alta), installa la prima e
avviala con la variabile d'ambiente `PINBOARD_UPDATE_URL=<cartella>`.

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
