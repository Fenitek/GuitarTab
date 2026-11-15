# Prestazioni dell'editor

## v0.6.1 — anteprima indipendente dal MIDI del brano

15 novembre 2026, .NET 10.0.204 Release, Windows, stessa macchina delle misure precedenti.
SoundFont GeneralUser GS 1.471 (31.281.186 byte). L'autotest genera 200 battute × 4 tracce,
scrive 12 tasti nella battuta 101 e misura ogni richiesta. La preparazione invalida il vecchio MIDI e attende esplicitamente Score e MIDI correnti
prima di spostarsi nella battuta 101; una prova che leggeva la disponibilit� del vecchio
documento contava erroneamente anche il caricamento. Nessuna prova concorrente
durante i campioni; nessun controllo di mouse o tastiera dell'utente.

| Punto raggiunto dall'anteprima | Prima v0.6.0 (ms) | Dopo v0.6.1 mediana (ms) | Massimo (ms) |
|---|---:|---:|---:|
| Ritorno da Api.PlayNote, una richiesta | 304,0 | — | — |
| Primo campione non silenzioso consegnato all'uscita, 12 richieste | non misurato | 17,66 | 23,91 |
| Campione raggiunto dall'AudioClock del dispositivo, 12 richieste | non misurato | 29,70 | 35,99 |

Il prima è il contatore dell'autotest v0.6.0, sullo stesso SoundFont: non misurava il
dispositivo. I due punti di misura sono esplicitamente diversi. Tutti i 12 campioni
finali e le prove di nota iniziale, basso nuovo e SoundFont personale sono sotto 50 ms.
Mediane calcolate come media dei due valori centrali. Campioni JSON e resoconti esterni
in GuitarStudio-v0.6.1/anteprima-finale.json e autotest-finale.txt; prima in
GuitarStudio-v0.6.0/autotest.txt. Le foto finali sono nella sottocartella foto.

Il cronometro parte all'ingresso di TypeDigit, prima di SetFret. Il thread audio trova
il primo campione oltre 0,0001, registra la coda WASAPI e attende che l'AudioClock lo
raggiunga. Sottrae il solo ritardo di osservazione usando l'avanzamento del clock;
non sottrae buffering o tempo di sintesi. La posizione viene riferita al padding
corrente dopo la sintesi, evitando di contare come audio consegnato eventuali underrun.
Non è una misura della tastiera fisica, del DAC o della propagazione acustica/Bluetooth.
Il client richiede il buffer minimo supportato (durata e periodicità a zero, come da
[documentazione Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/audioclient/nf-audioclient-iaudioclient-initialize));
quello effettivo della macchina è 22 ms, riportato nell'autotest.

Il renderer usa l'API pubblica AlphaSynthAudioExporter, precaricata con tutti i programmi;
crea solo brevi MIDI sul canale privato 0, senza rigenerare quello del brano. Tutta la
sintesi e l'uscita condivisa WASAPI sono su un thread dedicato. Il dispatcher accoda
solo valori immutabili. Nessuna Api.PlayNote; `_midiReady`, debounce del lettore di 300 ms,
UseWorkers=false e NAudio 2.4.0 restano validi. Due cifre aggiornano immediatamente
l'anteprima: se sono separate da un ciclo audio può sentirsi anche la prima cifra.

Il test di cambio nota ha rilevato una coda residua: AllSoundOff fa un rilascio breve.
Prima della nuova nota si sintetizzano e scartano 30 ms di rilascio, senza attendere
30 ms reali. Il PCM successivo coincide con quello di un renderer appena inizializzato.
I test coprono interruzione, fine naturale, Stop, programma, altezza, slice del buffer,
campioni finiti e SoundFont invalido. L'autotest copre anche nuova traccia, capotasto,
richieste staccate/annullate durante il caricamento e SoundFont personale nei due motori.

Per riprodurre: GS_SELFTEST=1, GS_SELFTEST_EXIT=1 e GS_SELFTEST_PREVIEW_REPORT con un
percorso JSON; GS_SELFTEST_PREVIEW_ONLY=1 limita la prova all'audio iniziale e all'anteprima.

## v0.4.2 — battute e anteprima

14 novembre 2026, stessa macchina e carico descritti sotto: 200 battute, quattro tracce,
6.400 tempi e 8.000 note; .NET 10.0.204 Release. Mediane di 10 campioni dopo riscaldamento.
Misure senza UI, con assert e preparazione esclusi dal cronometro. I processi di misura
iniziale e finale sono stati eseguiti separatamente, senza autotest concorrente.
Prima = sorgenti bf73f88 con la sola nuova misura; dopo = snapshot strutturali v0.4.2.
Campioni: [benchmarks/v0.4.2.csv](benchmarks/v0.4.2.csv).

| Operazione nel modello | Prima (ms) | Dopo (ms) | Massimo dopo (ms) |
|---|---:|---:|---:|
| InsertBarAfter | 605,28 | 9,73 | 13,97 |
| DeleteBar | 592,29 | 7,35 | 11,24 |
| Incolla battuta intera | 571,94 | 3,51 | 6,76 |
| MoveRight oltre fine brano | 600,11 | 3,74 | 21,26 |

| Operazione | Undo prima → dopo (ms) | Redo prima → dopo (ms) |
|---|---:|---:|
| InsertBarAfter | 847,74 → 10,86 | 827,01 → 10,07 |
| DeleteBar | 824,04 → 6,91 | 813,87 → 6,53 |
| Incolla battuta intera | 812,16 → 5,13 | 821,07 → 3,66 |
| MoveRight oltre fine brano | 845,98 → 2,31 | 773,65 → 2,17 |

Tutti i 120 tempi finali (modifica, Undo e Redo) sono sotto 50 ms; massimo complessivo
26,75 ms. L'incolla misurato è di una battuta su una battuta popolata. I test funzionali
coprono anche più battute e la sostituzione della prima battuta vuota, conservando le altre voci.
La soglia riguarda il modello: rendering, MIDI e pannello dell'app sono esclusi.

Lo snapshot salva indice/conteggi dello splice, MasterBar rimosse e Bar in tutti gli staff,
più il contesto limitato dei collegamenti ai margini. Undo e Redo mantengono lo stesso Score.
I test confrontano tutti i membri del `.gp` con il riferimento dello snapshot completo,
normalizzati con un identico roundtrip Full per eliminare differenze di ID e timestamp ZIP.
Ripetono Undo/Redo tre volte, con quattro tracce, secondo staff, due voci, armature diverse,
sezioni, ritornelli, BPM e note collegate. Non è un confronto limitato ai soli tasti/durate.

### Anteprima: difetto e correzione

Il controllo aggiunto prima della correzione in GS_SELFTEST ha fallito con **1 richiesta,
0 PlayNote riusciti**, benché il MIDI tornasse pronto. Dopo: **1 richiesta, 1 PlayNote**,
circa **310 ms** dalla richiesta. Il contatore misura il ritorno riuscito da PlayNote,
non una verifica a orecchio dell'uscita audio.

L'ultima richiesta conserva Score, revisione e identità della nota. OnMidiLoaded la esegue
solo per il MIDI corrente, con SoundFont pronto e nota ancora presente nel modello.
Altre modifiche, Undo, cambio documento o chiusura annullano la richiesta; spostare soltanto
il cursore la conserva. Due cifre rapide sostituiscono la richiesta e suonano solo il tasto finale.
L'autotest copre anche eventi MIDI vecchi, nota staccata e nuovi canali dopo Aggiungi traccia.
Il singolo punto PlayNote resta protetto da `_midiReady` e dal controllo della revisione.

**Proposta allora non applicata (realizzata in v0.6.1, sopra):** circa 310 ms sono percepibili per il feedback di scrittura.
Un motore separato per le sole anteprime, già inizializzato con SoundFont e canali propri,
permetterebbe di preparare una breve nota senza rigenerare il MIDI dell'intero brano.
Richiederebbe un proprio stato di disponibilità e test di suono/canali. In questa versione
restano il motore attuale e il debounce di 300 ms, senza anticipare PlayNote sul sintetizzatore non pronto.

### Riprodurre le nuove verifiche

```powershell
$env:GS_PERF_STRUCTURAL_REPORT = Join-Path $PWD 'artifacts/structural.json'
dotnet test -c Release --filter FullyQualifiedName~StructuralOperationsOn200BarsAnd4Tracks
$env:GS_SELFTEST = '1'
$env:GS_SELFTEST_EXIT = '1'
dotnet run -c Release --project src/GuitarStudio
```

`GS_SELFTEST_PREVIEW_ONLY=1` esegue soltanto le nuove verifiche di anteprima e chiude.
Il normale GS_SELFTEST comprende questi controlli, quelli precedenti e l'autosalvataggio reale.

## Misura storica v0.4.1

14 novembre 2026; Windows x64, AMD Ryzen 9 9900X, SDK .NET 10.0.204, Release.
Tempi in millisecondi: mediane di 10 campioni dopo riscaldamento, su questa macchina.
Campioni: [benchmarks/v0.4.1.csv](benchmarks/v0.4.1.csv).

## Metodo

PerformanceScore genera alphaTex con 200 battute in 4/4 e 4 tracce: 6.400 tempi, 8.000 note,
con note e accordi. Nell'app è mostrata la traccia selezionata, come nell'uso normale;
il modello e il MIDI comprendono tutte le tracce. Si modifica la battuta 100.
Nessuna paginazione o riduzione del brano; UseWorkers rimane false.

Prima: commit ba0ff90 con sola strumentazione. Il test senza UI esclude importazione,
spostamento del cursore e assert dai cronometri. Nell'app SetFret misura TypeDigit completo,
inclusi modello, OnEditorChanged e controllo dell'anteprima. Undo segue una modifica locale.
L'annulla strutturale conserva lo snapshot completo. I processi iniziali di test e app si sono
parzialmente sovrapposti; il carico concorrente può influenzare i campioni iniziali.
Le misure finali di modello e app sono state eseguite in sequenza.

## Prima e dopo

| Contesto e operazione | Prima (ms) | Dopo (ms) |
|---|---:|---:|
| Modello: SetFret | 697,48 | 3,81 |
| Modello: InsertBarAfter | 698,46 | 599,70 |
| Modello: Undo dopo SetFret | 978,69 | 3,67 |
| App: cifra, gestore completo | 849,27 | 8,28 |
| App: InsertBarAfter | 919,08 | 649,90 |
| App: Undo dopo cifra | 1.170,04 | 6,32 |

Massimi finali nell'app: cifra **17,76 ms**, Undo locale **37,04 ms**.
Il budget di 50 ms riguarda il gestore: non comprende tastiera fisica, attesa nella coda
Windows o presentazione dell'immagine al monitor.

| OnEditorChanged dopo una cifra | Prima (ms) | Dopo (ms) |
|---|---:|---:|
| Totale | 175,09 | 0,18 |
| Render / accodamento Render | 93,28 | 0,002 |
| LoadMidiForScore diretto | 51,87 | 0 |
| RebuildTracksPanel / aggiornamento celle | 16,49 | 0,028 |

**Lavoro differito ancora presente:** render completo **86,24 ms**, generazione MIDI **23,95 ms**.
Si svolgono sul dispatcher dopo circa 16 e 300 ms dall'ultima modifica. Il render può ancora
provocare una pausa visiva: non si dichiara una latenza tasto → immagine sotto i 50 ms.
I comandi strutturali e il salvataggio continuano a elaborare il brano completo.

## Interventi in ordine

| Stato | Cifra nell'app (ms) |
|---|---:|
| Prima | 849,27 |
| 1. Snapshot locali | 157,64 |
| 2. MIDI dopo 300 ms | 101,61 |
| 3. Riuso pannello tracce | 82,73 |
| Render accorpato dopo l'input | 8,28 |

Dopo il punto 3 il render costava ancora 65,54 ms. Il percorso incrementale alphaTab richiede
lazy loading, disabilitato esplicitamente dal suo host WPF. Per rispettare il budget del
gestore senza riattivare i worker, il render è quindi accorpato dopo l'input sullo stesso
dispatcher e sul modello corrente. Cursore, stato e panoramica si aggiornano subito.

Una raffica di 12 modifiche verifica **1 render, 1 MIDI, 0 ricostruzioni pannello**;
le anteprime restano bloccate fino al MIDI della revisione corrente.

## Riproduzione

In PowerShell dalla radice del progetto:

```powershell
New-Item -ItemType Directory -Force artifacts | Out-Null
$env:GS_PERF_MODEL_REPORT = Join-Path $PWD 'artifacts/perf-model.json'
dotnet test -c Release --filter FullyQualifiedName~PerformanceTests
$env:GS_BENCHMARK = '1'
$env:GS_PERF_APP_REPORT = Join-Path $PWD 'artifacts/perf-app.json'
dotnet run -c Release --project src/GuitarStudio
```

L'istanza genera il documento, registra il JSON e si chiude. Togliere GS_BENCHMARK prima
dell'autotest ordinario. La suite registra i tempi senza limiti dipendenti dalla macchina;
la verifica dei 50 ms usa i campioni dell'app riportati qui.
