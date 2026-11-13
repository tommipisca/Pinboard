# Pinboard

Widget per il desktop di Windows, con lo stile dei widget di macOS. Non stanno nel pannello widget
di Windows: sono agganciati al desktop e restano visibili anche con "Mostra desktop" (Win+D o
pulsante in fondo alla barra delle applicazioni).

## Widget

| Widget | Stato |
| --- | --- |
| Clock (ora e data) | primo prototipo, da provare |

## Come funziona l'aggancio

La finestra del widget diventa figlia del livello del desktop che contiene lo sfondo
(`Progman` / `WorkerW`) tramite `SetParent`. Non e' piu' una finestra di primo livello, quindi
"Mostra desktop" non la minimizza e non compare in barra delle applicazioni ne' in Alt+Tab.
Il codice e' in `src/Pinboard.Clock/DesktopHost.cs` e gestisce sia il layout classico sia quello
di Windows 11 24H2.

Limiti noti:
- i widget stanno sotto le icone del desktop;
- i click non arrivano al widget, perche' il livello delle icone sta sopra (per ora i widget
  sono solo informativi);
- lo sfondo e' semitrasparente, senza sfocatura (Mica/Acrylic non funzionano su finestre
  trasparenti di WPF).

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
