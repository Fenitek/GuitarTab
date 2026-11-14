# GuitarStudio — istruzioni per gli agenti

Leggi questo file **ogni volta** che apri il progetto, prima di toccare qualsiasi cosa.
Poi leggi `HANDOFF.md`: contiene obiettivo, architettura, trappole già scoperte e backlog.

## Regola n. 1: la versione sale a ogni modifica

**Dopo ogni cambio di qualsiasi cosa all'interno dell'applicazione** (codice, XAML, stili, asset,
dipendenze, testi dell'interfaccia) il contatore della versione **va aumentato**. Nessuna eccezione,
nemmeno per una modifica di una riga.

1. La versione vive in **un solo posto**: `<Version>` in `src/GuitarStudio/GuitarStudio.csproj`.
   L'interfaccia (barra laterale, finestra Aiuto) la legge dall'assembly tramite `AppInfo.Version`:
   non scriverla a mano da nessun'altra parte.
2. Schema `MAJOR.MINOR.PATCH`:
   - **PATCH** (0.2.0 → 0.2.1): correzioni, ritocchi grafici, rifiniture, refactoring.
   - **MINOR** (0.2.1 → 0.3.0): una funzione nuova visibile all'utente.
   - **MAJOR**: solo se lo chiede l'utente.
3. Aggiungi una voce in cima a `CHANGELOG.md` (versione, data, cosa è cambiato, in italiano e in
   parole comprensibili all'utente).
4. Un commit per ogni versione, con messaggio `vX.Y.Z: descrizione breve`. Versione, changelog e
   modifica stanno **nello stesso commit**.
5. Le modifiche che non toccano l'applicazione (solo documentazione o file di repository) non
   richiedono un aumento di versione.

## Flusso di lavoro

1. `git pull` (se c'è un remoto raggiungibile) e leggi `HANDOFF.md` + le ultime voci di `CHANGELOG.md`.
2. Fai la modifica.
3. `dotnet build` deve finire con **0 avvisi e 0 errori**.
4. Prova l'app: `dotnet run --project src/GuitarStudio`. Per le regressioni dell'editor usa
   l'autotest (`GS_SELFTEST=1`, vedi `HANDOFF.md` §3) e controlla `%TEMP%\gs_selftest.txt`.
5. Aumenta la versione, aggiorna `CHANGELOG.md`, e se hai cambiato architettura, stato delle sezioni
   o scoperto una trappola nuova aggiorna anche `HANDOFF.md`.
6. Commit. Il push su `origin` (https://github.com/Fenitek/GuitarTab) si fa quando l'utente lo chiede.

## Vincoli da non violare

- **NAudio resta 2.4.x**: alphaTab.Windows 1.9 va in crash con la 3.x.
- **Non nascondere l'attribuzione "alphaTab.net"** sulle partiture (lo richiede la licenza).
- **Anteprima audio**: mai `api.PlayNote` finché `_midiReady` è falso (crash del sintetizzatore
  su un thread interno, chiude il programma).
- Interfaccia e messaggi in **italiano**. Caratteri minimi 13 px, contrasto alto: l'utente usa uno
  schermo ad alta risoluzione ed è esigente sulla qualità dell'interfaccia.
- Tutti i controlli devono usare il tema scuro di `Themes/Dark.xaml`: niente controlli WPF con
  l'aspetto predefinito chiaro. Colori solo tramite le risorse del tema, non valori sparsi.
- L'impostazione riprende le convenzioni dei programmi di notazione; **non copiare grafica o marchio
  di Guitar Pro**.
- Non scaricare audio da YouTube; non inserire SoundFont senza una licenza che permetta di
  ridistribuirli.
- Non committare `bin/`, `obj/` né file personali dell'utente.
