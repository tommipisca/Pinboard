# Pinboard

Widget per il desktop di Windows, nello stile dei widget di macOS. Non stanno nel pannello widget
di Windows: stanno sul desktop e restano visibili anche con "Mostra desktop" (Win+D o pulsante
in fondo alla barra delle applicazioni).

## Widget

| Widget | Descrizione |
| --- | --- |
| Clock | orologio analogico, lancetta dei secondi arancione |
| Calendar | giorno della settimana in rosso, numero grande, mese e anno |
| **Exams** | **il widget di [ExamsTrack](#widget-exams)**: prossimo esame con countdown e task di oggi, nello stile dell'app |

Il widget **Exams** mostra i dati dell'app **ExamsTrack** (la versione per Windows): legge i suoi file in
sola lettura e usa il suo tema, quindi funziona solo se ExamsTrack e' installato. Clock e Calendar
sono indipendenti.

Lo stile segue i widget piccoli di macOS: scheda quadrata con angoli molto arrotondati (170 x 170),
sfondo semitrasparente, tipografia sottile e tema chiaro o scuro in base al tema delle app di
Windows. Lo sfondo non e' sfocato (Mica/Acrylic non funzionano su finestre trasparenti di WPF).

## Widget Exams

Nello stile di ExamsTrack ("inchiostro e evidenziatore"): sfondo blu-inchiostro, testo color carta,
nome dell'esame in serif (Sitka), countdown in Bahnschrift condensato. Colori e palette degli esami
sono quelli dell'app. Resta scuro anche con il tema chiaro di Windows.

Legge in sola lettura `%APPDATA%\ExamsTrack\data\exams.json` (e `deletions.json` per escludere gli
esami eliminati) e si aggiorna da solo quando ExamsTrack salva.
- **Prossimo esame**: stessa regola dell'app (`nextExam` in `logic.js`): il primo esame con una data,
  da oggi in poi, ordinato per data e poi per nome. Per impostazione predefinita considera entrambi i
  corsi, come ExamsTrack; con entrambi visibili indica se e' triennale o magistrale. Mostra nome, countdown (colore dell'esame, ambra a 7 giorni, corallo a 2),
  data, ora e task completati.
- **Oggi**: fino a 3 task con scadenza oggi, di qualsiasi esame: cerchio nel colore dell'esame,
  titolo e nome dell'esame. Quelli da fare vengono prima, quelli fatti sono barrati.
- Per limitarlo a un corso, aggiungi `"ExamsDegree": "triennale"` (o `"magistrale"`) in
  `%APPDATA%\Pinboard\widgets.json` e riavvia Pinboard. Non legge ancora l'impostazione
  "Triennale e magistrale" di ExamsTrack: e' salvata nel localStorage dell'app.

## Eseguibile

Serve il .NET 8 SDK solo per compilare. Da PowerShell, nella cartella della repo:

```powershell
.\publish.ps1
```

Crea `dist\Pinboard.exe`, un unico file che funziona anche senza .NET installato.

Per provare senza creare l'eseguibile: `dotnet run --project src/Pinboard`.

## Icona

`src/Pinboard/Assets/Pinboard.ico` e' l'icona dell'eseguibile e dell'area di notifica. Si rigenera
(insieme a `assets/pinboard-icon-1024.png`) con `python3 assets/make_icon.py`, che richiede Pillow.

## Uso

Icona nell'area di notifica (accanto all'orologio di Windows):
- **Sposta widget**: stacca i widget dal desktop, li trascini col mouse.
- **Blocca posizione**: salva le posizioni in `%APPDATA%\Pinboard\widgets.json` e li rimette sul desktop.
- **Avvia con Windows**: aggiunge o toglie l'avvio automatico.
- **Esci**.

Per diagnosi: `--floating` (finestre normali in primo piano) e `--debug` (log in
`%APPDATA%\Pinboard\pinboard.log`).

## Come restano sul desktop

Ogni widget e' una finestra di primo livello tenuta subito sopra il desktop e sotto tutte le
altre finestre. Quando "Mostra desktop" porta il desktop in primo piano, i widget diventano
temporaneamente "sempre in primo piano" e restano visibili; quando torna attiva un'altra
finestra, ridiventano normali. Il codice e' in `src/Pinboard/DesktopHost.cs`.

L'aggancio come figlio del desktop (`SetParent` su `Progman`/`WorkerW`, la tecnica di Lively) e'
stato provato ma su Windows 11 24H2 il contenuto non viene disegnato, quindi non e' usato.

Limite noto: i widget stanno sopra le icone del desktop e le coprono se si sovrappongono.
