# Pinboard

Widget per il desktop di Windows, con lo stile dei widget di macOS. Non stanno nel pannello widget
di Windows: stanno sul desktop e restano visibili anche con "Mostra desktop" (Win+D o pulsante
in fondo alla barra delle applicazioni).

## Widget

| Widget | Stato |
| --- | --- |
| Clock (ora e data) | primo prototipo, da provare |

## Come resta sul desktop

Il widget e' una finestra di primo livello tenuta subito sopra il desktop e sotto tutte le altre
finestre. Quando "Mostra desktop" (Win+D o pulsante in fondo alla barra) porta il desktop in
primo piano, il widget diventa temporaneamente "sempre in primo piano" e resta visibile; quando
torna attiva un'altra finestra, ridiventa una finestra normale. Il codice e' in
`src/Pinboard.Clock/DesktopHost.cs`.

L'aggancio come figlio del desktop (`SetParent` su `Progman`/`WorkerW`, la tecnica di Lively)
e' ancora presente con l'opzione `--child`, ma su Windows 11 24H2 il contenuto non viene
disegnato, quindi non e' usato.

Limiti noti:
- il widget sta sopra le icone del desktop e le copre se si sovrappone;
- lo sfondo e' semitrasparente, senza sfocatura (Mica/Acrylic non funzionano su finestre
  trasparenti di WPF).

Opzioni a riga di comando (per diagnosi): `--floating` (finestra normale in primo piano),
`--child`, `--opaque`, `--soft`. Il log e' in `%APPDATA%\Pinboard\clock.log`.

## Compilare e provare

Servono Windows 10/11 e .NET 8 SDK.

```powershell
dotnet run --project src/Pinboard.Clock
```

Icona nell'area di notifica (accanto all'orologio di Windows):
- **Sposta widget**: stacca il widget dal desktop, lo trascini col mouse.
- **Blocca posizione**: salva la posizione in `%APPDATA%\Pinboard\clock.json` e lo riaggancia.
- **Esci**.

## Test da fare

1. Il widget compare in alto a destra.
2. Win+D e pulsante "Mostra desktop": il widget deve restare visibile.
3. Sposta widget, trascina, blocca: dopo il riavvio deve tornare dove l'hai lasciato.
4. Riavvia Explorer da Gestione attivita': il widget deve riagganciarsi.
5. Prova con piu' monitor e con scala del display diversa da 100%.
