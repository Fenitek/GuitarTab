# Prestazioni dell'editor — v0.4.1

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
