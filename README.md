# Pinboard

Widget per il desktop di Windows, nello stile dei widget di macOS. Non stanno nel pannello widget
di Windows: stanno sul desktop e restano visibili anche con "Mostra desktop" (Win+D o pulsante
in fondo alla barra delle applicazioni).

## Widget

| Widget | Descrizione |
| --- | --- |
| Clock | orologio analogico, lancetta dei secondi arancione |
| Calendar | giorno della settimana in rosso, numero grande, mese e anno |
| Exams | widget medio: prossimo esame in programma con i giorni che mancano, e i task di oggi di qualsiasi esame |

Lo stile segue i widget piccoli di macOS: scheda quadrata con angoli molto arrotondati (170 x 170),
sfondo semitrasparente, tipografia sottile e tema chiaro o scuro in base al tema delle app di
Windows. Lo sfondo non e' sfocato (Mica/Acrylic non funzionano su finestre trasparenti di WPF).

## Widget Exams

Nello stile di ExamsTrack ("inchiostro e evidenziatore"): sfondo blu-inchiostro, testo color carta,
nome dell'esame in serif (Sitka), countdown in Bahnschrift condensato. Colori e palette degli esami
sono quelli dell'app. Resta scuro anche con il tema chiaro di Windows.

Legge in sola lettura `%APPDATA%\ExamsTrack\data\exams.json` (e `deletions.json` per escludere gli
esami eliminati) e si aggiorna da solo quando ExamsTrack salva.
- **Prossimo esame**: il primo esame in programma del corso di laurea `triennale`, non ancora superato
  e con data da oggi in poi. Mostra nome, countdown (colore dell'esame, ambra a 7 giorni, corallo a 2),
  data, ora e task completati.
- **Oggi**: fino a 3 task con scadenza oggi, di qualsiasi esame: cerchio nel colore dell'esame,
  titolo e nome dell'esame. Quelli da fare vengono prima, quelli fatti sono barrati.
- Il corso di laurea considerato e' la costante `CurrentDegree` in `ExamsWidget.cs`.

## Eseguibile

Serve il .NET 8 SDK solo per compilare. Da PowerShell, nella cartella della repo:

```powershell
.\publish.ps1
```

Crea `dist\Pinboard.exe`, un unico file che funziona anche senza .NET installato.

Per provare senza creare l'eseguibile: `dotnet run --project src/Pinboard`.

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
