# GuitarStudio — documento di passaggio

Questo documento serve a un'altra AI (o a uno sviluppatore) per riprendere il progetto senza dover ricostruire niente. Contiene: obiettivo, stato attuale, architettura, trappole già scoperte, i problemi segnalati dall'utente con una prima diagnosi, e la lista del lavoro da fare.

---

## 1. Chi è l'utente e cosa vuole

- Chitarrista (elettrica, acustica, rock e blues). Impara guardando video di tablature e rifacendole, spesso più lentamente.
- Ha una **Blackstar ID:Core Stereo 10 V3** collegata al PC via USB (fa da scheda audio). Al momento della scrittura non era collegata: il nome esatto del dispositivo di ingresso non è noto.
- Vuole un **vero programma Windows** (non un'app web), con un editor di tablature **all'altezza di Guitar Pro** (non ce l'ha). Le altre funzioni vanno in sezioni separate dell'app.
- È esigente sulla **qualità dell'interfaccia**: ha bocciato una prima versione con pulsanti messi in fila senza criterio. Ha mandato come riferimento la schermata di Guitar Pro 8: barra superiore con trasporto e display, tavolozza verticale a sinistra, spartito al centro, pannello proprietà a destra (Brano/Traccia), elenco tracce con mixer e panoramica battute in basso. **Riprendere l'impostazione (convenzione comune ai programmi di notazione), non copiare la grafica o il marchio di Guitar Pro.**
- Lingua dell'interfaccia e dei messaggi: **italiano**.

## 2. Stato in breve

| Sezione (barra laterale) | Stato |
|---|---|
| **Tab & Pratica** | Funziona: apertura file Guitar Pro / MusicXML, riproduzione, editor di tablature, pratica (velocità, loop, speed trainer, metronomo, conteggio), manico. |
| **Allenatore** | Solo pagina segnaposto. |
| **Trascrivi MP3** | Solo pagina segnaposto. |
| **Libreria** | Solo pagina segnaposto. |

Il progetto è un repository git (remoto: https://github.com/Fenitek/GuitarTab). Regole di lavoro e di versione: `AGENTS.md`. Storico: `CHANGELOG.md`.

### Aggiornamento v0.4.3

- Colori letterali rimossi dai sorgenti C#/XAML fuori da Dark.xaml, inclusi Black/White/
  Transparent e i due COLORREF del titolo (WindowTheme ora converte BgPanel/Line in BGR).
  Risorse semantiche Fretboard*, Score*, Editor*, Track*, ToastBackground,
  ShadowColor e TransparentFill; riuso di Accent, PlaybackNote e BgDeep dove coincidevano.
  Refactoring confrontato separatamente dai ritocchi visivi: 42 foto su 69 identiche;
  le restanti differiscono solo nell'attribuzione renderizzata e nei dati/stati del recupero.
- Manico: font disegnati 10,5/12 → 13 px, fascia inferiore 16 → 20 px. ComboBoxItem
  selezionato: TextMain/SemiBold, perché Accent su BgPressed era 4,30:1. Toast: DockPanel
  invece della misura orizzontale illimitata, così il testo non viene tagliato al minimo.
  Guida: larghezza massima, invece che fissa, per le tre schede, titoli a capo con margine; resta da adattare il corpo a due colonne.
- Setup: dopo un recupero, se esiste App.StartupFile, il toast nomina il file non aperto.
  La copia resta non salvata e _currentPath resta null. Apertura normale da CLI verificata
  separatamente; il confronto dell'autotest normalizza entrambi i percorsi Windows.
- Diagnostics/VisualCapture.cs fotografa esclusivamente HWND dell'app con PrintWindow;
  nessun input nativo, screenshot del desktop o attivazione di finestre. Le scene compongono
  le foto degli HWND di popup/dialoghi sopra quella dell'ospite; i PNG *-popup-N conservano
  anche ogni finestra completa, utile se il popup si estende oltre l'ospite.
- Modalità foto: GS_SELFTEST=1 + GS_SELFTEST_UI=1 + GS_UI_CAPTURE_DIR, obbligatorio
  GS_SELFTEST_DATA_DIR con dati sintetici. PracticeView.VisualSelfTest fotografa recupero,
  modifica, tutti i combo visibili, ☰, guida, nuova traccia e avviso alle due dimensioni;
  seleziona Recupera soltanto nel presentatore di test. Verifica il toast e chiude da sola,
  anche in caso di errore. Senza GS_UI_CAPTURE_DIR resta l'autotest completo precedente.
- AppPaths isola settings, autosave e crash.log in GS_SELFTEST_DATA_DIR soltanto con
  GS_SELFTEST=1. La modalità normale mantiene LOCALAPPDATA/GuitarStudio. MainWindow non
  richiede attivazione durante l'autotest; le finestre delle foto usano ShowActivated=false.
- Foto fuori repository: `%USERPROFILE%/.codex/visualizations/2026/11/14/01a1dbd8-afd1-78d3-82a7-0e4fa2e0579a/GuitarStudio-v0.4.3/`.
  `prima-completa`, `solo-tema`, `dopo` (vedi controllo visivo),
  manifest DPI/dimensioni e confronti pixel. Foto a 1920×1080 fisici e minimo 1500×930
  (Windows 150%, minimo applicazione 1000×620 DIP). Guida al proprio minimo 640×420 DIP;
  dialoghi non ridimensionabili conservano la dimensione naturale in entrambi gli ospiti.
- `docs/CONTROLLO_VISIVO.md` e relativo CSV riportano ogni foto e i difetti; restano trasporto
  tagliato, guida al minimo, numeri dei tasti alti affollati e attribuzione alphaTab poco
  contrastata. Richiedono un intervento oltre i ritocchi eseguiti; nessun ridisegno applicato.
  `docs/PROVA_MANUALE.md`: 14 controlli entro cinque minuti, incluso recupero dopo arresto forzato.
- Verifiche: build 0 avvisi/0 errori, 59 test, autotest completo OK, recupero+CLI e CLI senza
  recupero controllati nell'autotest visivo. Nessuna modifica a UseWorkers=false o al MIDI.

### Aggiornamento v0.4.2

- `HistorySnapshot.BarSplice`: indice, conteggi prima/dopo, MasterBar eliminate serializzate
  e contesto `Bars` di tutte le tracce/staff coinvolti. Per inserire non salva il nuovo contenuto;
  l'inverso lo cattura prima di Undo. Il contesto ai margini conserva gli effetti che Finish
  può modificare fuori dalla battuta rimossa. Il serializzatore generico ModelData<T>
  riusa l'adattatore alphaTab 1.9 per MasterBar e Bar, senza passare da `.gp`.
- MoveRight oltre fine, InsertBarAfter, DeleteBar e Paste a battute intere usano BarSplice.
  Nell'incolla con prima battuta vuota lo splice sostituisce una battuta e ne inserisce N-1:
  ripristina anche le altre voci/staff della prima battuta. Al ripristino modifica prima
  tutte le liste MasterBars/Bars, poi Relink, Chain e Finish. Score resta lo stesso oggetto;
  `BarIndices=null` comunica alla UI un cambiamento strutturale.
- HistoryTests è diviso in due file partial. Confronta tutti i membri `.gp` dopo un identico
  roundtrip Full (ID/timestamp ZIP normalizzati), non solo i campi del confronto musicale.
  Copre inizio/centro/fine, più battute, sostituzione vuota, quattro tracce, secondo staff,
  due voci, armature, metadati, effetti ai margini e storie miste, con Undo/Redo ripetuti.
- PerformanceTests misura le quattro operazioni strutturali e i relativi Undo/Redo;
  `GS_PERF_STRUCTURAL_REPORT` sceglie il JSON. Dati in docs/benchmarks/v0.4.2.csv:
  modifica nel modello circa 600 → 3,5–9,7 ms; tutti i 120 tempi finali sotto 50 ms
  (massimo 26,75 ms). Metodo e tabella completi in docs/PERFORMANCE.md.
- **Anteprima confermata persa prima della correzione:** autotest 1 richiesta/0 PlayNote,
  con MIDI già pronto. Ora PreviewCurrentNote conserva l'ultima PendingPreview
  (Score, Note, revisione, timestamp). OnMidiLoaded corrente la riprova; anche SoundFontLoaded
  può sbloccarla. Prima di PlayNote: SoundFont e `_midiReady` veri, revisione corrente,
  nota ancora appartenente alla catena Score/Track/Staff/Bar/Voice/Beat. Mai PlayNote se falso.
- OnEditorChanged e CancelPendingMidiReload invalidano l'anteprima; il solo cursore non
  la invalida. Due cifre rapide suonano solo il tasto finale. Contatori richieste/suonate
  e latenza per autotest; misurato 1/1 in circa 310 ms. Il debounce resta 300 ms.
  Un motore separato già pronto è solo una proposta nel resoconto, non implementata.
- GS_SELFTEST include le verifiche di anteprima (eventi vecchi, nota staccata, Undo,
  cambio documento e nuovi canali); `GS_SELFTEST_PREVIEW_ONLY=1` esegue solo queste e chiude.
  Usare GS_SELFTEST_EXIT=1 per la chiusura del test completo.
- Verifica v0.4.2: 59 test verdi, build 0 avvisi/0 errori, autotest completo OK
  (anteprime, audio e autosave reale), nessuna nuova eccezione nel crash.log. L'attesa
  del controllo autosave confronta la revisione corrente e registra i numeri di revisione.

### Aggiornamento v0.4.1

- Misure/limiti in `docs/PERFORMANCE.md`, campioni in `docs/benchmarks/v0.4.1.csv`.
  AlphaTex generato: 200 battute, 4 tracce, 6.400 tempi e 8.000 note, Release.
  Modello SetFret 697,48 → 3,81 ms; Undo locale 978,69 → 3,67 ms.
  App cifra 849,27 → 8,28 ms (massimo 17,76); Undo locale 1.170,04 → 6,32 ms.
  Il budget è del gestore, non tasto → immagine: render differito ancora 86,24 ms.
  InsertBarAfter resta strutturale, circa 650 ms nell'app; il brano non è stato paginato.
- `Editing/HistorySnapshot.cs`: snapshot lossless delle battute locali (tutte le voci),
  più confinanti/estremi di collegamenti. Struttura, attivazione voci, proprietà traccia,
  brano e tempo in chiave conservano lo snapshot `.gp` completo. Undo/Redo catturano
  l'inverso dello stesso tipo; per gli edit locali l'istanza Score resta viva.
- Adattatore reflection isolato per BarSerializer e MapObjectReader/Writer **interni ad
  alphaTab 1.9**: conserva anche automazioni, testi e stili. Non fare Finish su una battuta
  isolata: perde legature esterne. Ripristino: Relink, lookup note per indirizzo, Chain,
  Finish dell'intero Score. `ChangedBars` espone le battute locali; null per cambiamenti globali.
- **Trappole**: gli importer azzerano i contatori statici degli ID; con snapshot locali
  possono creare collisioni, corrette prima di Changed. Finish non elimina sempre un vecchio
  SlideTarget: prima di Finish si puliscono i riferimenti a note staccate. Test dedicati.
- `PracticeView.Playback.cs`: debounce MIDI 300 ms. Ogni edit blocca `_midiReady`;
  caricamenti associati a Score/revisione, MidiLoaded vecchi non abilitano anteprime.
  Apri/Nuova/Demo e Closed annullano il timer. Un nuovo Score (Undo strutturale) genera
  ancora MIDI implicitamente in alphaTab.RenderScore; gli edit locali non cambiano Score.
- `PracticeView.Rendering.cs`: render dopo 16 ms a priorità Background, accorpato sul
  dispatcher. **UseWorkers resta false** in entrambi i punti di inizializzazione. WPF
  impone lazy loading disabilitato: non forzarlo per usare RenderHints incrementali.
  Render espliciti/cambio documento cancellano la richiesta pendente; il callback usa
  il modello corrente. Il ridisegno completo rimane costoso e può bloccare brevemente la UI.
- Pannello tracce: chiave su riferimenti/nome/suono tracce e numero battute. Edit locali
  aggiornano solo celle/tooltip coinvolti. Il cambio Score strutturale ricrea i controlli
  per ricollegare gli handler al modello nuovo, senza closure su vecchi Track.
- `GS_BENCHMARK=1` avvia la misura nell'app, `GS_PERF_APP_REPORT` sceglie il JSON;
  l'istanza si chiude da sola. `GS_PERF_MODEL_REPORT` registra PerformanceTests.
  Non usare GS_BENCHMARK con GS_SELFTEST. Stopwatch separa Render, LoadMidiForScore,
  pannello e lavoro differito. Una raffica di 12 edit verifica 1 render, 1 MIDI,
  0 ricostruzioni pannello e anteprime bloccate fino al MIDI pronto.
- Verifiche: 45 test verdi (37 precedenti più 8 nuovi), autotest OK anche per audio e
  autosave reale; benchmark nell'app concluso senza nuove eccezioni.

### Aggiornamento v0.4.0

- `Recovery/AutosaveStore.cs`: una copia `.gp` per sessione in
  `%LOCALAPPDATA%\GuitarStudio\autosave\`; lease `.lock` esclusivo, temporaneo `.tmp`,
  flush e sostituzione atomica sullo stesso volume. Le sessioni attive vengono escluse dal recupero.
  Dispose rilascia il lease ma conserva il `.gp`; Delete lo elimina esplicitamente.
- `PracticeView.Autosave.cs`: DispatcherTimer ogni 60 secondi, solo se IsDirty e la revisione
  è cambiata dall'ultima copia. Export e scrittura sul dispatcher per evitare corse col modello.
  Se fallisce, avviso e nuovo tentativo al giro seguente; non viene mai chiamato MarkSaved.
- Il recupero precede demo/file da riga di comando; dialogo canonico Ui/Dialogs.cs con
  Recupera / Elimina copia / Più tardi. Chiudere o premere Esc equivale a Più tardi.
  Più tardi e copie illeggibili restano sul disco; ogni copia orfana viene proposta dalla più recente.
  Una copia accettata mantiene il proprio lease e diventa documento senza percorso, IsDirty=true.
- Salvataggio riuscito, apertura/nuova/demo dopo conferma e Closed eliminano solo la copia
  della sessione; chiusura annullata e salvataggio fallito la conservano. Il timer si ferma in Closed.
- `TabEditor.Attach(..., isDirty: true)` serve al recupero; il comportamento predefinito resta pulito.
- Test xUnit per scrittura/rilettura, sessioni attive, lease orfani, recupero e guasti I/O.
  Parallelismo tra classi disattivato: alphaTab inizializza metadati SMuFL globali senza
  sincronizzazione e i primi Settings costruiti in parallelo possono dare Collection modified.
  L'autotest WPF aspetta un tick reale, verifica il salvataggio esplicito e prepara una copia
  da controllare dopo Closed. `GS_SELFTEST_EXIT=1` chiude pulitamente dopo il report;
  usarlo insieme a `GS_SELFTEST=1` per verifiche non interattive (durata circa un minuto).
- Verifica v0.4.0: `dotnet build` 0 avvisi/0 errori, `dotnet test` 37/37;
  autotest con tick reale e `GS_SELFTEST_EXIT=1` terminato con OK, copia e lease rimossi
  dopo la chiusura. Nessuna nuova eccezione nel crash.log. Audit statico UI senza rilievi.
  Il controllo manuale delle finestre è stato interrotto con Esc dall'utente; il nuovo
  dialogo di recupero non è stato provato manualmente. La logica di persistenza/recupero
  è coperta dai test senza UI.

### Aggiornamento v0.3.1

- `tests/GuitarStudio.Tests`: xUnit, `net10.0-windows`, incluso nella soluzione.
  Eseguire `dotnet test`: crea partiture alphaTex senza UI; controlla indici, proprietà
  dei genitori e catene PreviousBeat/NextBeat separate per voce dopo ogni modifica,
  annulla/ripeti e rilettura di ExportGp. Verifica anche il contenuto musicale.
- Guitar Pro normalizza una voce interamente vuota in un solo tempo vuoto: i confronti
  ignorano questi segnaposto senza ignorare pause visibili o note.
- Regressioni corrette: FixBar riempie di pause anche dopo aver tolto tempi in eccesso
  e riduce una singola nota troppo lunga; RestsFor completa il resto nei metri dispari.
- L'inserimento dei numeri a due cifre è coperto come SetFret(10/12/24) nel modello;
  la combinazione temporizzata dei tasti reali resta coperta dall'autotest WPF.
- **Rendering alphaTab 1.9**: `Core.UseWorkers = false`. Con il worker, una sequenza
  rapida di taglia/annulla/ripeti può deserializzare BoundsLookup di uno Score vecchio
  contro il modello attuale (`ArgumentOutOfRangeException` in BoundsLookup.FromJson).
  Il disegno sul dispatcher evita la corsa; su partiture grandi può rendere l'editing meno fluido.
  L'autotest ora fallisce anche per eccezioni WPF intercettate dall'App, prima ignorate nel report.
  Impostare `Tab.Settings.Core.UseWorkers` sull'oggetto già esistente nel costruttore, prima
  di OnApplyTemplate: cambiare da worker a sincrono dopo l'avvio provoca anche un'eccezione
  di cancellazione non gestita sul thread interno di alphaTab.

### Aggiornamento v0.3.0

- Due voci modificabili: `TabEditor.VoiceIndex`, `SelectVoice(0/1)`; `CurrentVoice`, selezione,
  appunti, durate e hit test seguono la voce attiva. La selezione si azzera cambiando voce.
- Attivare una voce inesistente crea pause vuote nelle altre battute e pause visibili in quella
  corrente. La prima attivazione di una battuta vuota è annullabile. I file importati con voci
  aggiuntive vengono preservati; il selettore espone le prime due.
- `Relink` mantiene una catena di beat distinta per ogni voce. Taglia/Canc su una selezione
  non rimuovono più battute globali: agiscono soltanto sui beat della voce selezionata.
  Eliminare una battuta in tutte le voci richiede il comando esplicito corrispondente.
- L'autotest verifica una minima sopra un arpeggio, taglia/annulla/ripeti/incolla senza perdita
  dell'altra voce, esportazione/rilettura .gp e durate MIDI sovrapposte.
- Contesto visivo e mappa dei componenti canonici in `DESIGN.md`; i token restano in Dark.xaml.
- **Avvio dal sandbox**: un'istanza lanciata con i permessi limitati degli strumenti può leggere
  uno spartito sul Desktop ma non salvarlo. Per uso interattivo avviare l'eseguibile con normali
  permessi utente fuori dal sandbox (non serve modificare ACL o avviare come amministratore).
  Non chiudere un'istanza con modifiche non salvate prima di averle recuperate.

### Aggiornamento v0.2.1

- **Audio dopo le modifiche**: `RenderScore` rigenera il MIDI solo se cambia l'istanza di `Score`.
  Le modifiche nello stesso oggetto (note, durate, BPM, suono, tracce) richiedono
  `Api.LoadMidiForScore()`: lo fa `OnEditorChanged`, fermando prima il lettore e invalidando
  `_midiReady`. Non aggiungere la rigenerazione al generico `Render`, usato anche per zoom e tracce.
- `_midiReady` segue `MidiLoad` / `MidiLoaded`. Non azzerarlo in `TracksChanged`, che arriva
  dopo `Changed` e potrebbe lasciare bloccate le anteprime quando il MIDI è già pronto.
- `Relink()` ricostruisce anche i gruppi dei ritornelli: una nuova `MasterBar` senza
  `RepeatGroup` causa `NullReferenceException` durante la generazione MIDI, anche senza ritornelli.
- L'autotest ora verifica il MIDI ricevuto dal lettore (note e suono), i BPM, campioni audio
  non silenziosi e Play dal pulsante. Facoltativamente `GS_SELFTEST_REPORT` imposta il percorso
  del resoconto (il log in corso usa lo stesso percorso con estensione `.live.txt`);
  senza questa variabile restano i percorsi `%TEMP%` descritti sotto.

### Aggiornamento v0.2.0 (leggere prima del resto)

Le sezioni sotto descrivono la v0.1.0; con la v0.2.0 è cambiato questo:

- **Risolti** i punti 6.2 (avvisi), 6.4, 6.5 e quasi tutto il 6.6; del 6.3 è fatta la scelta del SoundFont (manca un SoundFont migliore incluso). Restano da fare: verifica a orecchio (6.1.1) e prova con mouse/tastiera veri.
- **File nuovi**: `Themes/Dark.xaml` (tutti i colori e gli stili: non mettere stili o colori nelle viste), `Ui/WindowTheme.cs` (barra del titolo scura: chiamare `WindowTheme.Apply(this)` in ogni finestra), `Ui/Dialogs.cs` (usare questo, **mai `MessageBox`**), `AppSettings.cs` (preferenze in `%LOCALAPPDATA%\GuitarStudio\settings.json`), `AppInfo.cs` (versione), `Views/HelpWindow.cs` (guida F1: aggiornarla quando cambia una scorciatoia), `Views/PracticeView.Shell.cs` (avvisi `ShowToast`, file recenti, SoundFont, export MIDI, larghezza pannelli).
- **Trappola scoperta**: `Score.Finish` di alphaTab **cancella in silenzio** `IsHammerPullOrigin`, lo slide legato/shift e `IsTieDestination` se manca l'altra nota sulla stessa corda. Era la causa di "legato e slide non vanno". `TabEditor.LinkWarning` lo controlla *prima* di applicare l'effetto.
- **Apertura file**: `PracticeView.OpenPath` legge con `ScoreLoader` e poi chiama `Api.RenderScore` (non più `Api.Load`), così un file non valido non tocca la tablatura aperta.
- `Tab.Api` è annullabile: nelle viste usare la proprietà `Api`.
- **Autotest**: con `GS_SELFTEST_UI=1` (oltre a `GS_SELFTEST=1`) alla fine restano aperti guida, menu a tendina e avviso, per fotografarli. Se `bin/Debug` è bloccato da un'istanza aperta dell'app, compilare con `-c Release`.
- Non verificato a schermo: l'aspetto dei menu a tendina aperti e del menu ☰ (i modelli sono in `Themes/Dark.xaml`).

## 3. Tecnologia

- **C# / .NET 10 / WPF** (`net10.0-windows`). SDK installato: 10.0.204.
- **alphaTab 1.9.0** (`AlphaTab` + `AlphaTab.Windows`, NuGet): disegno di tab e spartito, import/export Guitar Pro, sintetizzatore MIDI con SoundFont. Licenza MPL-2.0.
- **NAudio 2.4.0**. ⚠️ Deve restare **2.4.x**: alphaTab.Windows 1.9 è compilato contro NAudio 2.4. Con NAudio 3.x l'app va in crash all'avvio della riproduzione (`TypeLoadException: Method 'Read' in type 'AlphaTab.NAudioSynthOutput'…`).
- **SoundFont**: `Assets/sonivox.sf2` (SONiVOX GM, licenza Apache 2.0, preso dal pacchetto npm `@coderline/alphatab@1.9.0`). È piccolo (1,3 MB) e la qualità dei suoni di chitarra è bassa (vedi §6).

### Compilare e avviare

```bash
dotnet build
dotnet run --project src/GuitarStudio
```

Eseguibile: `src/GuitarStudio/bin/Debug/net10.0-windows/GuitarStudio.exe`.
Registro errori: `%LOCALAPPDATA%\GuitarStudio\crash.log` (scritto da `App.xaml.cs`).

### Autotest

Con la variabile d'ambiente `GS_SELFTEST=1` l'app, dopo l'avvio, crea una tab nuova e simula la tastiera (cifre, frecce, effetti, selezione, copia/incolla, terzine, proprietà battuta, nuova traccia di basso, Drop D + capotasto, salvataggio `.gp` e rilettura, riproduzione). Il resoconto va in `%TEMP%\gs_selftest.txt` (e riga per riga in `%TEMP%\gs_selftest_live.txt`, utile se crasha). Codice: `Views/PracticeView.SelfTest.cs`.

Limite: i tasti sono iniettati con `RaiseEvent` sulla vista, **non** con input reale. Il comportamento con il focus dentro lo `ScrollViewer` di alphaTab e i clic reali non è stato provato a mano.

Per fotografare la finestra senza che altre finestre la coprano si è usato `PrintWindow` (user32) da PowerShell: `CopyFromScreen` cattura quello che c'è davanti.

## 4. Struttura dei file

```
GuitarStudio.slnx
src/GuitarStudio/
  GuitarStudio.csproj          riferimenti NuGet; Assets copiati in output
  App.xaml(.cs)                colori/stili globali (tema scuro), log degli errori non gestiti
  MainWindow.xaml(.cs)         barra laterale con le 4 sezioni; chiede di salvare alla chiusura
  Assets/sonivox.sf2           SoundFont (+ licenza)
  Editing/
    TabEditor.cs               TUTTA la logica di modifica sul modello alphaTab (nessuna UI)
    ModelCopy.cs               copia profonda di Beat/Note via reflection (alphaTab non ha Clone pubblico)
    InstrumentCatalog.cs       suoni MIDI chitarra/basso (programmi 24–36) e nomi delle accordature
  Views/
    PracticeView.xaml          layout della sezione Tab & Pratica (barra, tavolozza, spartito, pannelli)
    PracticeView.xaml.cs       setup alphaTab, file, rendering, cursore/overlay, tastiera, trasporto
    PracticeView.Layout.cs     tavolozza effetti, pannelli, display, ispettore Brano/Traccia/Battuta, pannello tracce
    PracticeView.SelfTest.cs   autotest (vedi sopra)
    FretboardView.cs           manico disegnato a mano (OnRender), cliccabile
    TrackPropertiesWindow.*    finestra "nuova traccia" (duplica in parte InstrumentCatalog: da unificare)
    ComingSoonView.*           segnaposto delle sezioni non fatte
```

## 5. Architettura e trappole già scoperte

### alphaTab nel controllo WPF
- Il controllo è `AlphaTab.Wpf.AlphaTab` (`x:Name="Tab"`). **Le impostazioni vanno assegnate dopo che il controllo ha applicato il template**: assegnarle in `Loaded` causa `NullReferenceException` in `WpfUiFacade.CreateCursors`. Soluzione attuale: `Dispatcher.BeginInvoke(Setup, DispatcherPriority.ContextIdle)` da `Loaded`. Servono anche `BarCursorFill`, `BeatCursorFill` e `SelectionFill` impostati in XAML.
- Lo `ScrollViewer` interno si ottiene con `Tab.Template.FindName("PART_ScrollView", Tab)`.
- `api.ScoreLoaded` scatta **anche a ogni `RenderScore`, in modo asincrono**. Dopo un annulla veloce può arrivare una partitura vecchia e azzerare il cursore. Soluzione: il flag `_expectingLoad`, impostato solo da Apri/Nuova/Demo; altrimenti la partitura in arrivo si ignora.
- **Attribuzione**: alphaTab disegna "♪ alphaTab.net" sulle partiture con più di 10 battute. La licenza richiede che resti visibile; toglierla richiede il permesso dei manutentori (https://alphatab.net/docs/attribution). **Non nasconderla.**

### Modello e modifiche (`TabEditor`)
- Ogni modifica passa da `Edit(action)`: snapshot locale per note/tempi/appunti locali,
  splice per battute e `.gp` completo per tracce/altre proprietà globali (max 100 passi). Pulisce collegamenti staccati,
  chiama `Score.Finish(settings)`, corregge ID duplicati e lancia Changed. La vista aggiorna
  subito i controlli, accorpa il render dopo 16 ms e il MIDI dopo 300 ms. Restano costosi
  esportazione, annulla di tracce/proprietà globali e render completo (vedi v0.4.1 e v0.4.2).
- **Numerazione delle corde**: in alphaTab `Note.String = 1` è la corda **più grave**. Nell'editor `Line = 1` è la corda **più acuta** (in alto nella tab). Conversione: `ModelString(line) = StringCount - line + 1`.
- Dopo inserimenti o cancellazioni di battute e tempi, `Relink()` ricostruisce a mano indici e collegamenti (MasterBar, Bar, Voice, Beat, Prev/Next). Funziona, ma dipende dai dettagli interni di alphaTab.
- `MasterBar.KeySignature` ha un setter che scrive sulle `Bar` collegate. Impostarlo su una MasterBar nuova senza battute dà `NullReferenceException`: l'armatura si copia sulle singole `Bar`.
- Tempo (BPM): `MasterBar.TempoAutomations` con `Automation.BuildTempoAutomation(false, 0, bpm, 0, true)`.
- Ritornelli: `IsRepeatStart`, `RepeatCount`, poi `Score.RebuildRepeatGroups()`. Sezioni: `MasterBar.Section = new Section { Text, Marker = "" }`.
- Nuova traccia: `Track` + `Staff` (`StringTuning`, `DisplayTranspositionPitch = -12`), canali MIDI liberi (evitando il 9, quello della batteria), battute di pause, `Score.AddTrack`.

### Audio
- ⚠️ **Crash del sintetizzatore**: suonare un'anteprima (`api.PlayNote`) su una traccia appena creata, prima che alphaTab abbia rigenerato il MIDI, causa `IndexOutOfRangeException` in `AlphaTab.Synth.Synthesis.Voice.Render`. Siccome avviene su un thread interno, **chiude il programma**. Soluzione attuale: `_midiReady = false` quando cambiano le tracce, torna `true` su `api.MidiLoaded`, e l'anteprima viene saltata finché non è pronto. Qualsiasi nuova funzione che cambia canali o programmi deve rispettare questa regola.

### Cursore dell'editor sopra lo spartito
- Si usa `api.BoundsLookup`. Per ogni battuta, `MasterBarBounds.Bars` contiene i riquadri dei pentagrammi; l'**ultimo** relativo allo staff corrente è la tab. Il suo `VisualBounds` va esattamente dalla prima all'ultima corda, quindi `y(corda) = Y + (line-1) * H/(n-1)`.
- Le coordinate sono relative al contenuto dello ScrollViewer. Si convertono con `content.TranslatePoint(..., Overlay)` e il ridisegno avviene su `ScrollChanged` e su `PostRenderFinished`.

## 6. Problemi segnalati dall'utente (da sistemare per primi)

### 6.1 "Non capisco se la riproduzione è fedele (bend ecc.)"
- **Diagnosi**: il sintetizzatore di alphaTab traduce bend, vibrato, slide e hammer-on in pitch bend e articolazioni MIDI, quindi le **note** e gli **effetti** vengono riprodotti. Non è stato verificato a orecchio. Il **timbro** invece è scarso per via del SoundFont SONiVOX (vedi 6.3).
- **Da fare**:
  1. Verificare a orecchio bend, release, vibrato, slide, hammer, palm mute, dead note e armonici con un file di prova.
  2. Rendere visibile all'utente cosa viene riprodotto (es. il manico che mostra le note in tempo reale c'è già: renderlo più evidente).
  3. Mettere una nota nella UI su cosa il motore audio supporta.

### 6.2 "Legato e slide non vanno / non capisco come funzionano"
- **Diagnosi**: H/P e Slide si applicano alla nota di **partenza** e la collegano alla **nota successiva sulla stessa corda**. Se dopo non c'è una nota sulla stessa corda, non compare né si sente niente. Se il cursore non è su una nota, l'unico avviso è una scritta piccola nella barra di stato, facile da non vedere.
- **Da fare**:
  - feedback chiaro e visibile quando l'effetto non si può applicare (messaggio vicino al cursore o toast);
  - descrizioni che spiegano "dalla nota sotto il cursore alla prossima sulla stessa corda";
  - opzionale: se il cursore è sulla nota di arrivo, applicare automaticamente alla nota precedente sulla stessa corda;
  - un'anteprima che suoni le due note collegate;
  - verificare che lo slide (`SlideOutType.Legato`/`Shift`) sia disegnato e suonato correttamente.

### 6.3 "I suoni delle chitarre non vanno"
- **Diagnosi**: SONiVOX è un General MIDI minimale. I programmi 24–36 (nylon, acciaio, pulita, distorta, basso…) esistono ma suonano quasi uguali e poco realistici. Non è un bug di codice: mancano campioni migliori.
- **Da fare**:
  - inserire un SoundFont GM migliore **con licenza che permetta di ridistribuirlo** (da verificare caso per caso: per esempio FluidR3_GM è MIT; GeneralUser GS ha una licenza permissiva);
  - in alternativa, far scegliere all'utente un `.sf2/.sf3` suo (impostazione persistente; `api.LoadSoundFont(bytes, append)`);
  - a lungo termine, per una distorsione credibile serve elaborazione audio (amp sim): fuori dalla portata del SoundFont.

### 6.4 "Le scorciatoie a sinistra sono illeggibili"
- Testo da 11,5 pt grigio su fondo scuro, nella tavolozza. Lo schermo dell'utente è ad alta risoluzione: la finestra catturata era 3862×2110.
- **Da fare**:
  - togliere il blocco "SCORCIATOIE" dalla tavolozza;
  - creare un pannello d'aiuto leggibile (F1 o pulsante "?") con le scorciatoie divise per categoria;
  - mettere la scorciatoia nei tooltip di ogni pulsante (in parte c'è già);
  - in generale alzare i caratteri minimi a 13–14 e aumentare il contrasto.

### 6.5 "Alcuni menu a tendina sono bianchi, vecchissimi"
- **Diagnosi**: `ComboBox`, `ContextMenu`/`MenuItem`, `Slider`, `CheckBox`, `ScrollBar`, `ToolTip` e il popup delle ComboBox usano ancora lo stile WPF predefinito (chiaro, aspetto Windows 7). Solo i pulsanti hanno template personalizzati.
- **Da fare**, una delle due strade:
  - (a) il **tema Fluent integrato di .NET 9/10** (`Application.ThemeMode = Dark`; API sperimentale, va soppressa la diagnostica `WPF0001`);
  - (b) una libreria di controlli moderna (es. **WPF-UI** di lepoco, licenza MIT, stile Windows 11), con i colori del progetto: sfondo `#1E1F24`, pannelli `#26282F`, accento `#E8A33D`.
  - In entrambi i casi controllare tutte le viste e le finestre di dialogo e togliere gli stili fatti a mano che entrano in conflitto.

### 6.6 Altro da sistemare
- Provare con mouse e tastiera **veri**: frecce con il focus nella partitura, clic su tab e manico, Maiusc+clic, e la finestra a dimensioni normali, non ingrandita.
- `PracticeView.xaml`: l'intestazione del pannello tracce usa un margine fisso (`Margin="432,0,0,0"`) per allinearsi alle colonne. Va legata alle colonne vere.
- Le larghezze di tavolozza (214) e ispettore (270) sono fisse: renderle ridimensionabili con `GridSplitter`.
- `TrackPropertiesWindow` duplica `InstrumentCatalog`: unificare.
- I nomi delle tracce nel pannello in basso sono stati allineati a sinistra dopo l'ultima foto, ma il risultato non è stato verificato a schermo.

## 7. Funzioni dell'editor già presenti

- Cursore battuta/tempo/corda disegnato sopra la tab; clic per posizionarlo; Maiusc+clic o Maiusc+frecce per selezionare.
- Cifre per il tasto (due cifre veloci = 10–24), anteprima audio della nota.
- Durate da 1 a 1/32, punto, terzina, pausa.
- Effetti:
  - legato e slide: hammer/pull, slide legato e shift, slide in/out, legatura di valore;
  - bend (1, ½, con rilascio, pre-bend) e vibrato (normale e largo);
  - tecniche: palm mute, nota stoppata, ghost note, let ring, staccato, accenti, tapping mano sinistra;
  - armonici naturali, artificiali e pinch;
  - sul tempo: tapping, pennata su/giù, tremolo picking.
- Copia, taglia e incolla (battute intere o tempi), seleziona tutto.
- Battute e tempi: inserisci/elimina tempo e battuta, "Correggi battuta".
- Proprietà della battuta: cambio BPM, tempo in chiave, ritornelli con numero di ripetizioni, sezione.
- Brano: titolo, artista, BPM iniziale.
- Tracce: aggiungi/elimina (chitarra 6/7, basso 4/5), suono, accordatura, capotasto, mute/solo/volume, panoramica battute cliccabile.
- Annulla/ripeti (100 passi); salvataggio `.gp` (Guitar Pro 7/8) e `.atex`; lettura di `.gp3/.gp4/.gp5/.gpx/.gp/MusicXML/.capx`.
- Pratica: velocità 25–150% senza cambiare l'intonazione, loop su selezione (trascinando sulla tab), speed trainer, metronomo, conteggio, zoom, vista tab/spartito.

### Scorciatoie attuali
| Tasto | Azione |
|---|---|
| 0–9 | tasto sulla corda (due cifre veloci = 10–24) |
| ↑ ↓ / ← → | corda / tempo |
| Maiusc+frecce, Maiusc+clic | selezione |
| Ctrl+← → | battuta precedente/successiva |
| Esc | annulla la selezione |
| + / − | durata più lunga / più corta |
| . | punto |
| Ctrl+3 | terzina |
| R | pausa |
| Canc / Backspace | cancella la nota (o la selezione) |
| Ins / Ctrl+Ins | inserisci tempo / battuta |
| Ctrl+Canc / Ctrl+Maiusc+Canc | elimina tempo / battuta |
| H S B V P X L T G A N | hammer, slide, bend, vibrato, palm mute, stoppata, let ring, legatura, ghost, accento, armonico |
| Ctrl+C / X / V / A | copia, taglia, incolla, seleziona tutto |
| Ctrl+Z / Y | annulla / ripeti |
| Ctrl+F | correggi battuta |
| Ctrl+B / Ctrl+T | pannello Battuta / Traccia |
| Ctrl+N / O / S, Ctrl+Maiusc+S | nuova, apri, salva, salva come |
| F2 | mostra/nascondi la tavolozza (= modalità modifica) |
| Spazio | play/pausa |

## 8. Cosa manca rispetto a Guitar Pro (backlog dell'editor)

In ordine di utilità indicativo:
1. Tema scuro completo e coerente (6.5) e leggibilità (6.4).
2. SoundFont migliore e scelta del SoundFont (6.3).
3. Voci: prime due modificabili dalla v0.3.0; eventuale estensione del selettore a quattro voci.
4. Diagrammi degli accordi e nomi degli accordi; testo della canzone.
5. Grace note, leva (whammy), dinamiche (p, mf, f…), crescendo.
6. Finali alternativi dei ritornelli (prima/seconda volta), cambio d'armatura.
7. Traccia di batteria modificabile.
8. Stampa/PDF; export MIDI e audio (alphaTab ha `ExportAudio` e la generazione MIDI).
9. Mixer più completo (pan, riverbero) e pannelli ridimensionabili.
10. Prestazioni dell'annulla su brani lunghi (§5).

## 9. Le altre sezioni (da fare)

- **Allenatore**: ingresso audio dalla Blackstar ID:Core V3 (WASAPI; meglio ASIO se c'è il driver Blackstar, per avere poca latenza) con NAudio 2.4. Riconoscimento dell'intonazione per le linee a nota singola (YIN o simili), cromagramma per gli accordi. Confronto con la tab aperta: note giuste o sbagliate, anticipo o ritardo in millisecondi, punteggio, storico.
- **Trascrivi MP3**: separazione delle tracce (Demucs), trascrizione in note (Basic Pitch di Spotify), riconoscimento accordi. Da MIDI a tab con un algoritmo di diteggiatura (minimizzare i salti sul manico). Il risultato si apre nell'editor come bozza. Si può usare ONNX in C# oppure un motore Python incluso.
- **Libreria**: SQLite locale con brani, file, BPM originale, BPM raggiunto, sessioni di pratica, note personali.

Non scaricare l'audio dei video di YouTube (è contro i loro termini d'uso): lavorare su file che l'utente possiede o che registra.
