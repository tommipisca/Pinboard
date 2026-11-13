# Pinboard

Widget per il desktop di Windows, nello stile dei widget di macOS. Non stanno nel pannello widget
di Windows: stanno sul desktop e restano visibili anche con "Mostra desktop" (Win+D o pulsante
in fondo alla barra delle applicazioni).

## Widget

| Widget | Descrizione |
| --- | --- |
| Clock | orologio analogico, lancetta dei secondi arancione |
| Calendar | giorno della settimana in rosso, numero grande, mese e anno |

Lo stile segue i widget piccoli di macOS: scheda quadrata con angoli molto arrotondati (170 x 170),
sfondo semitrasparente, tipografia sottile e tema chiaro o scuro in base al tema delle app di
Windows. Lo sfondo non e' sfocato (Mica/Acrylic non funzionano su finestre trasparenti di WPF).

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
