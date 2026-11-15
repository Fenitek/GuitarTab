# Controllo visivo v0.5.0 — 15 novembre 2026

**T, G2 e M1 chiusi.** La barra dà priorità ai comandi essenziali, la guida si dispone
su una colonna al minimo e il manico dirada le etichette senza rimpicciolirle.
La verifica v0.4.3 in fondo a questo documento è storica: i suoi difetti residui descrivono
la situazione precedente, non lo stato attuale.

## Dimensioni e metodo

Foto esterne al repository, ottenute soltanto con l'autotest e PrintWindow:
`%USERPROFILE%/.codex/visualizations/2026/11/14/01a1dbd8-afd1-78d3-82a7-0e4fa2e0579a/GuitarStudio-v0.5.0/`.

- `prima/`: baseline della vecchia impaginazione, con sola strumentazione/versione aggiornata;
  11 scene, 17 PNG. Al minimo il vecchio manico provocava un ciclo di scroll: quella sola
  foto è stata saltata dopo aver riprodotto il difetto. La vista principale minima è presente.
- `dopo/`: 32 scene, 59 PNG, inclusi menu completi, sottomenu, speed trainer e tutte le pagine
  della guida. `manifest.json` riporta dimensioni, DPI nativo e DPI effettivo dei contenuti;
  `layout-metrics.json` contiene i numeri del layout.
- `dopo-report.txt` e `autotest-completo-finale.txt`: autotest visivo e completo, esito OK.
  `xunit-inprocess.txt`: 61 test eseguiti, 61 superati, zero falliti/saltati/errori.

Nessun mouse o tasto inviato al sistema, nessuna impostazione del desktop modificata.
L'autotest calibra **i contenuti al 200% (192 DPI)** tramite LayoutTransform rispetto al DPI
reale; anche i popup finali sono calibrati. Le cornici native conservano la scala del desktop
(quella rilevata nel manifest), quindi questa è una prova della geometria dell'app al 200%,
non un cambio della scala globale di Windows. I vecchi popup della baseline conservano la
loro scala nativa; la vista principale, il manico e la guida sono calibrati al 200% nelle due serie.
Le finestre GS_SELFTEST hanno ShowActivated=false e WS_EX_NOACTIVATE; il fotografo verifica
che la finestra principale non sia in primo piano. I PNG delle finestre/popup completi
accompagnano i compositi: un popup oltre il bordo dell'ospite non è una parte persa dell'elenco.

| Profilo | PNG | Larghezza al 200% | Barra | Viewport partitura (DIP) | Sistemi interi |
|---|---|---|---|---|---|
| Principale | 1920×1080 | 960 DIP | Due righe, tutti i gruppi | 295,3 | 2 |
| Intermedio | 1840×1080 | 920 DIP | Compatta, gruppi in Altro | 334,7 | 2 |
| Minimo | 1760×960 | 880 DIP | Compatta, gruppi in Altro | 274,7 | 1 |

La guida è fotografata anche a **1280×840 pixel**, corrispondenti al suo minimo 640×420 DIP.
Non sono stati ridotti i caratteri per ottenere lo spazio: la vista iniziale è **Solo tab al 100%**,
con titolo e accordatura nelle proprietà anziché sopra il foglio. Tab + spartito e Solo spartito
restano nel selettore; il limite di due sistemi riguarda la vista iniziale. Il manico è nascosto
all'avvio e viene fotografato anche aperto; le tracce iniziano con una riga e scorrono sulle altre.

## Difetti chiusi e contratti verificati

| Difetto | Prima | Dopo e prova |
|---|---|---|
| T | Trasporto e display tagliati, già a 150%; al 200% quasi tutti i comandi centrali invisibili | Prima riga stabile con Play, Stop, display e velocità; seconda riga da 820 DIP interni, overflow sotto. Bounds di tutti i controlli verificati nei tre profili. |
| G2 | Spiegazioni della guida in colonne troppo strette | Riquadri su una colonna sotto 840 DIP utili; spiegazioni larghe almeno 180 DIP, font almeno 13, titoli a capo e fine raggiungibile con scorrimento. |
| M1 | Numeri 17/19/21 sovrapposti sul manico stretto | Misura con il font effettivo a 13; almeno 4 DIP tra i rettangoli, 24 sempre presente. Le 24 posizioni restano cliccabili e il tooltip dà corda e numero. |
| Ciclo di scroll | Richieste ricorsive da ScrollChanged quando manca una zona stabile, riprodotte nella baseline minima | ScrollChanged ridisegna senza scorrere; guardia di rientranza fino a ContextIdle, viewport minimo 90 DIP, destinazione limitata a ScrollableHeight ed epsilon 1. Prova forzata a 58 DIP: 64 aggiornamenti/0 scroll; prova normale: 1 correzione, poi stabilità e rispetto dello scroll manuale. |
| Attribuzione | Credito originale poco contrastato, e fuori vista scorrendo | Originale conservato; riga alphaTab.net fissa, a 13, LineStrong su Paper, leggibile anche scorrendo. |

I comandi File vengono spostati tra i contenitori, conservando gli stessi MenuItem e il loro
ordine. Loop, metronomo e conteggio sono testati sia dal menu verso i pulsanti/player, sia
nel ritorno dello stato verso il menu. Speed trainer viene ancorato ad Altro quando il suo
pulsante è nascosto. Anche nella finestra larga Altro offre Pannelli, Tracce e Spazio partitura.
Aggiungi/Elimina traccia restano raggiungibili quando l'intestazione del mixer è nascosta.
Ogni icona della barra ha nome accessibile e tooltip con nome e scorciatoia.
Le combinazioni sono esatte: Alt+F4 e Maiusc+F10 non sono intercettati; F4 e i tasti
dei selettori/campi di testo conservano il comportamento nativo. L’autotest verifica
anche che lettere e frecce sui selettori non modifichino note o cursore.

I due GridSplitter sono provati con DragStarted/DragDelta/DragCompleted WPF, senza input
nativo; settings.json viene scritto, riletto e riapplicato. Il minimo centrale di 140 DIP e
il minimo del manico dipendente dalle corde impediscono di azzerare il foglio. Ridurre la
finestra limita le altezze senza cancellare quelle desiderate. Alternative senza trascinamento:
frecce sul divisore e Altro → Spazio partitura. Il focus e il trascinamento con dispositivi
reali restano nella lista [PROVA_MANUALE.md](PROVA_MANUALE.md), non vengono dichiarati provati.

## Foto della verifica attuale

Per ogni PNG della serie finale il CSV [CONTROLLO_VISIVO_v0.5.0.csv](CONTROLLO_VISIVO_v0.5.0.csv)
riporta superficie, confronto prima e risultato. L'elenco qui sotto include anche gli HWND
completi dei popup. Nei compositi valgono i risultati della superficie in primo piano.
La carta chiara è intenzionale; le zone nere esterne ai popup sono trasparenza/ombra di PrintWindow.

| Foto in dopo/ | Prima omonima | Risultato |
|---|---|---|
| `1920-altro-popup-1.png` | Nuovo stato | Menu del tema; gruppi secondari e regolazioni raggiungibili |
| `1920-altro.png` | Nuovo stato | Menu del tema; gruppi secondari e regolazioni raggiungibili |
| `1920-file-popup-1.png` | Sì | Comandi File originali, ordine conservato; raggiungibili anche da Altro |
| `1920-file.png` | Sì | Comandi File originali, ordine conservato; raggiungibili anche da Altro |
| `1920-guida-audio-popup-1.png` | Nuovo stato | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `1920-guida-audio.png` | Nuovo stato | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `1920-guida-effetti-popup-1.png` | Nuovo stato | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `1920-guida-effetti.png` | Nuovo stato | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `1920-guida-fondo-popup-1.png` | Nuovo stato | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `1920-guida-fondo.png` | Nuovo stato | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `1920-guida-popup-1.png` | Sì | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `1920-guida.png` | Sì | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `1920-manico.png` | Sì | M1 chiuso; etichette misurate a 13, distanza >=4 DIP, 24 presente |
| `1920-predefinita.png` | Sì | T chiuso; Play/Stop/display/velocità interi; due sistemi nel profilo principale |
| `1920-tracce-estese.png` | Nuovo stato | Altezza regolata, intestazione visibile e preferenza conservata |
| `1920-trainer-popup-1.png` | Nuovo stato | Speed trainer raggiungibile, popup ancorato al pulsante visibile |
| `1920-trainer.png` | Nuovo stato | Speed trainer raggiungibile, popup ancorato al pulsante visibile |
| `intermedia-altro-popup-1.png` | Nuovo stato | Menu del tema; gruppi secondari e regolazioni raggiungibili |
| `intermedia-altro.png` | Nuovo stato | Menu del tema; gruppi secondari e regolazioni raggiungibili |
| `intermedia-esercitazione-popup-1.png` | Nuovo stato | Toggle e player sincronizzati; segni di spunta visibili |
| `intermedia-esercitazione-popup-2.png` | Nuovo stato | Toggle e player sincronizzati; segni di spunta visibili |
| `intermedia-esercitazione.png` | Nuovo stato | Toggle e player sincronizzati; segni di spunta visibili |
| `intermedia-file-popup-1.png` | Sì | Comandi File originali, ordine conservato; raggiungibili anche da Altro |
| `intermedia-file-popup-2.png` | Nuovo stato | Comandi File originali, ordine conservato; raggiungibili anche da Altro |
| `intermedia-file.png` | Sì | Comandi File originali, ordine conservato; raggiungibili anche da Altro |
| `intermedia-guida-audio-popup-1.png` | Nuovo stato | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `intermedia-guida-audio.png` | Nuovo stato | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `intermedia-guida-effetti-popup-1.png` | Nuovo stato | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `intermedia-guida-effetti.png` | Nuovo stato | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `intermedia-guida-fondo-popup-1.png` | Nuovo stato | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `intermedia-guida-fondo.png` | Nuovo stato | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `intermedia-guida-popup-1.png` | Sì | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `intermedia-guida.png` | Sì | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `intermedia-manico.png` | Sì | M1 chiuso; etichette misurate a 13, distanza >=4 DIP, 24 presente |
| `intermedia-predefinita.png` | Sì | T chiuso; Play/Stop/display/velocità interi; due sistemi nel profilo principale |
| `intermedia-tracce-estese.png` | Nuovo stato | Altezza regolata, intestazione visibile e preferenza conservata |
| `intermedia-trainer-popup-1.png` | Nuovo stato | Speed trainer raggiungibile, popup ancorato al pulsante visibile |
| `intermedia-trainer.png` | Nuovo stato | Speed trainer raggiungibile, popup ancorato al pulsante visibile |
| `minimo-altro-popup-1.png` | Nuovo stato | Menu del tema; gruppi secondari e regolazioni raggiungibili |
| `minimo-altro.png` | Nuovo stato | Menu del tema; gruppi secondari e regolazioni raggiungibili |
| `minimo-esercitazione-popup-1.png` | Nuovo stato | Toggle e player sincronizzati; segni di spunta visibili |
| `minimo-esercitazione-popup-2.png` | Nuovo stato | Toggle e player sincronizzati; segni di spunta visibili |
| `minimo-esercitazione.png` | Nuovo stato | Toggle e player sincronizzati; segni di spunta visibili |
| `minimo-file-popup-1.png` | Sì | Comandi File originali, ordine conservato; raggiungibili anche da Altro |
| `minimo-file-popup-2.png` | Nuovo stato | Comandi File originali, ordine conservato; raggiungibili anche da Altro |
| `minimo-file.png` | Sì | Comandi File originali, ordine conservato; raggiungibili anche da Altro |
| `minimo-guida-audio-popup-1.png` | Nuovo stato | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `minimo-guida-audio.png` | Nuovo stato | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `minimo-guida-effetti-popup-1.png` | Nuovo stato | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `minimo-guida-effetti.png` | Nuovo stato | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `minimo-guida-fondo-popup-1.png` | Nuovo stato | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `minimo-guida-fondo.png` | Nuovo stato | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `minimo-guida-popup-1.png` | Sì | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `minimo-guida.png` | Sì | G2 chiuso; testi >=13, spiegazioni >=180 DIP, scorrimento verticale fino alla fine |
| `minimo-manico.png` | Nuovo stato | M1 chiuso; etichette misurate a 13, distanza >=4 DIP, 24 presente |
| `minimo-predefinita.png` | Sì | T chiuso; Play/Stop/display/velocità interi; due sistemi nel profilo principale |
| `minimo-tracce-estese.png` | Nuovo stato | Altezza regolata, intestazione visibile e preferenza conservata |
| `minimo-trainer-popup-1.png` | Nuovo stato | Speed trainer raggiungibile, popup ancorato al pulsante visibile |
| `minimo-trainer.png` | Nuovo stato | Speed trainer raggiungibile, popup ancorato al pulsante visibile |

## Esecuzione, build e limiti dell'ambiente

Per ripetere la verifica: GS_SELFTEST=1, GS_SELFTEST_UI=1, GS_SELFTEST_LAYOUT=1,
GS_UI_CAPTURE_DIR esterno al repository, GS_SELFTEST_DATA_DIR per i dati sintetici e
GS_SELFTEST_REPORT per il resoconto. Senza GS_UI_CAPTURE_DIR si esegue l'autotest completo;
GS_SELFTEST_EXIT=1 lo chiude. L'avvio con `dotnet GuitarStudio.dll` mantiene il processo
nella sessione di esecuzione del sandbox; un processo in background può essere terminato
quando finisce il comando che lo ha avviato.

Build Debug e Release: **0 avvisi, 0 errori**, con:

```powershell
dotnet build --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet build -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
```

VSTest ordinario va in timeout prima della suite perché testhost non si collega al runner
in questo ambiente; non è riportato come verde. È stata invece eseguita la **stessa suite
xUnit di 61 test** nello stesso processo, usando l'AssemblyRunner già incluso nell'adattatore
installato (reflection, perché incorporato come tipo interno), senza dipendenze nuove.
Sorgenti del piccolo launcher e log sono fuori repository in runner-xunit e xunit-inprocess.txt.
Due test nuovi coprono le vecchie preferenze e il roundtrip delle due altezze. L'autotest
completo è OK, inclusi audio, anteprima e timer reale di autosave. Audit statico UI strict:
zero violazioni; non sostituisce i controlli runtime appena descritti.

Ricerca di colori letterali C#/XAML fuori da Dark.xaml: zero corrispondenze (inclusi HEX,
COLORREF, Colors/Brushes e nomi diretti). UseWorkers=false, NAudio 2.4 e le guardie MIDI
non sono stati modificati. build-diagnostic.log è stato eliminato e non è tra le modifiche.

Il sandbox nega Git: prima FETCH_HEAD per pull, poi `.git/index.lock: Permission denied`
per git add. Nessun cambio di permessi o forzatura; modifiche lasciate nell'albero di lavoro,
nessun commit e nessun push. Messaggio previsto:
`v0.5.0: trasporto adattivo e spazio per la partitura`.

---

# Verifica v0.4.3 conservata come storico

Le tabelle seguenti descrivono la situazione della v0.4.3 e le sue fotografie, non i difetti
residui della v0.5.0. Il CSV storico CONTROLLO_VISIVO.csv conserva le sue 69 righe originali.

# Controllo visivo v0.4.3 — 14–15 novembre 2026

Foto ottenute esclusivamente nell'autotest con PrintWindow, senza spostare il mouse,
inviare tasti al sistema, attivare finestre o catturare il desktop. Le operazioni nei
controlli sono eseguite dal dispatcher dell'app; il focus reale va provato a mano.

## Foto e dimensioni

Radice esterna al repository:
`%USERPROFILE%/.codex/visualizations/2026/11/14/01a1dbd8-afd1-78d3-82a7-0e4fa2e0579a/GuitarStudio-v0.4.3/`.

| Serie | Contenuto |
|---|---|
| `prima-completa/` | Prima della centralizzazione dei colori e dei ritocchi; manico visibile |
| `solo-tema/` | Stesse scene dopo il solo trasferimento dei colori, senza ritocchi |
| `dopo/` | Risultato finale con font, contrasto, larghezza delle schede e toast corretti |
| `cli-verificato/` | Apertura normale di un file richiesto, senza copia da recuperare |

Ogni serie principale contiene **33 scene e 69 PNG**, inclusi gli HWND dei popup.
`1920-*` è la finestra ospite a **1920×1080 pixel fisici**, cioè 1280×720 DIP a 150%.
`minimo-*` è il minimo principale **1000×620 DIP**, cioè **1500×930 pixel** a 150%.
La guida è fotografata anche al proprio minimo **640×420 DIP** (960×630 pixel).
Nuova traccia e recupero non sono ridimensionabili: dimensione naturale, ospite nelle due dimensioni.
`manifest.json` registra dimensioni, DPI e TextBlock visibili sotto 13 px: nessuno;
il vecchio testo disegnato manualmente sul manico è stato verificato anche nel codice.

Le scene sono composte dalle catture PrintWindow dei singoli HWND nelle loro posizioni reali.
I file `*-popup-N.png` conservano la finestra completa. Alcuni elenchi sporgono sotto il bordo
inferiore dell'ospite: la scena li ritaglia, il PNG del popup mostra l'elenco e la scrollbar
interi. Questo limite della composizione non è un controllo tagliato nell'app. Le aree nere
esterne alle cornici dei popup sono la trasparenza/ombra non restituita da PrintWindow.
Le barre del titolo native sono gestite da Windows; il minimo 13 px si riferisce ai testi dell'app.

## Confronto dei colori e ritocchi

`confronto-solo-tema.json` confronta i 69 PNG di prima e del solo tema: **42 sono identici**;
**23** differiscono soltanto nei pixel dell'attribuzione alphaTab.net (252–259 pixel;
45/34 con avviso sovrapposto). Gli altri **4** sono le foto del recupero, con data,
cornice nativa e sfondo in caricamento variabili. Fuori da quelle aree il confronto del
solo trasferimento non mostra differenze. Il gradiente del manico, i cursori e le tracce
conservano i valori originali, riusando risorse esistenti dove coincidevano. Anche i due
COLORREF della barra del titolo sono ricavati da BgPanel e Line, senza valori BGR duplicati.
Le barre del titolo delle due foto principali prima/dopo sono identiche pixel per pixel
(`verifica-finale.json`).

I ritocchi successivi sono intenzionali e separati dal confronto del solo tema:

- Manico: lettere da 12 a **13 px**, numeri da 10,5 a **13 px**; margine inferiore da 16 a 20 px.
- ComboBox: testo selezionato **TextMain/SemiBold**. Il rapporto sul grigio evidenziato passa
  da **4,30:1 a 8,15:1**; sullo sfondo ambrato della selezione da circa 4,38:1 a 8:1.
  Nessun valore nuovo nella palette. Testo principale su pannello 12,93:1,
  secondario 7,51:1, etichette del manico 5,43:1.
- Toast: un vincolo di larghezza tramite DockPanel permette il ritorno a capo.
  Prima l'avviso lungo al minimo, incluso il nome del file richiesto, era tagliato;
  dopo viene mostrato per intero, a font 14,5 px. La barra di stato usa volutamente l'ellissi.
- Guida: larghezza **massima**, anziché fissa, per le tre schede. Il titolo «Audio e suoni»
  ora rientra anche al minimo; i titoli lunghi vanno a capo con margine laterale. Il corpo a due colonne richiede ancora un impaginato adattivo.

## Legenda dei difetti

| Codice | Osservazione e risultato |
|---|---|
| F1 | Manico: testo 10,5/12 px. Corretto a 13 px. Non confondere con la guida aperta da F1. |
| C1 | Attribuzione alphaTab.net poco contrastata sulla carta, circa 2:1. Lasciata visibile e invariata: richiede intervenire sulle risorse del renderer, oltre i ritocchi a stili/margini. |
| C2 | Testo selezionato ambra su sfondo evidenziato, 4,30:1. Corretto nello stile del ComboBoxItem. I comandi disabilitati del menu restano attenuati e non sono errori di contrasto dei comandi attivi. |
| T | Trasporto troppo largo: già a 1920×1080 alcuni comandi e la parte destra del display sono tagliati; al minimo spariscono quasi tutti i comandi centrali. Serve riprogettare la barra, non bastano margini. Invariato. |
| G1 | Guida minima: terza scheda tagliata dalla larghezza fissa. Corretto il limite di larghezza. |
| G2 | Guida minima: colonne fisse e spazio per scorciatoie lasciano troppo poco posto alle spiegazioni, spezzate in frammenti. Serve impaginato adattivo. Invariato. Lo scorrimento verticale nella guida grande è previsto. |
| M1 | Manico minimo: numeri dei tasti 17/19/21 troppo ravvicinati, più evidenti a 13 px. Serve adattare il disegno/l'intervallo visibile, non ridurre il carattere. Invariato. |
| A1 | Avviso lungo tagliato nel riquadro stretto. Corretto il ritorno a capo; testo completo nelle foto finali. |
| R1 | Recupero con file da CLI: mancava il nome del file ignorato; al primo controllo con il nuovo testo il toast risultava tagliato. Ora nome e avviso sono completi. |

Non sono emersi controlli WPF chiari, nuovi disallineamenti di form o testi applicativi
sotto 13 px dopo i ritocchi. La carta chiara della partitura è intenzionale.
Il basso contrasto dell'attribuzione e i problemi T/G2/M1 restano documentati, senza ridisegni.

## Risultato per ogni foto

Ogni riga si applica ai due file omonimi in `prima-completa/` e `dopo/`.
La freccia indica prima → dopo; «—» indica nessun difetto di quella categoria osservato.
Nei compositi i codici del manico/trasporto/attribuzione si riferiscono allo sfondo **quando
non coperto dalla finestra in primo piano**; i PNG dei singoli HWND hanno un esito proprio.
Il CSV omonimo contiene le stesse 69 righe. Nessuna foto è stata inserita nel repository.

| Foto | Testo <13 px | Contrasto basso | Tagli/disposizione | Controlli chiari |
|---|---|---|---|---|
| `1920-avviso.png` | F1 → — | — | T → T | Nessuno → nessuno |
| `1920-combo-TrackCapoCombo-popup-1.png` | — | C2 → — | — | Nessuno → nessuno |
| `1920-combo-TrackCapoCombo.png` | F1 → — | C1 → C1; C2 → — | T → T | Nessuno → nessuno |
| `1920-combo-TrackSoundCombo-popup-1.png` | — | C2 → — | — | Nessuno → nessuno |
| `1920-combo-TrackSoundCombo.png` | F1 → — | C1 → C1; C2 → — | T → T | Nessuno → nessuno |
| `1920-combo-TrackTuningCombo-popup-1.png` | — | C2 → — | — | Nessuno → nessuno |
| `1920-combo-TrackTuningCombo.png` | F1 → — | C1 → C1; C2 → — | T → T | Nessuno → nessuno |
| `1920-combo-ViewCombo-popup-1.png` | — | C2 → — | — | Nessuno → nessuno |
| `1920-combo-ViewCombo.png` | F1 → — | C1 → C1; C2 → — | T → T | Nessuno → nessuno |
| `1920-combo-ZoomCombo-popup-1.png` | — | C2 → — | — | Nessuno → nessuno |
| `1920-combo-ZoomCombo.png` | F1 → — | C1 → C1; C2 → — | T → T | Nessuno → nessuno |
| `1920-combo-battuta-BarDenCombo-popup-1.png` | — | C2 → — | — | Nessuno → nessuno |
| `1920-combo-battuta-BarDenCombo.png` | F1 → — | C1 → C1; C2 → — | T → T | Nessuno → nessuno |
| `1920-guida-popup-1.png` | — | — | — | Nessuno → nessuno |
| `1920-guida.png` | — | — | — | Nessuno → nessuno |
| `1920-menu-file-popup-1.png` | — | — | — | Nessuno → nessuno |
| `1920-menu-file.png` | F1 → — | C1 → C1 | T → T | Nessuno → nessuno |
| `1920-nuova-traccia-CapoCombo-popup-1.png` | — | — | — | Nessuno → nessuno |
| `1920-nuova-traccia-CapoCombo-popup-2.png` | — | C2 → — | — | Nessuno → nessuno |
| `1920-nuova-traccia-CapoCombo.png` | F1 → — | C1 → C1; C2 → — | T → T | Nessuno → nessuno |
| `1920-nuova-traccia-InstrumentCombo-popup-1.png` | — | — | — | Nessuno → nessuno |
| `1920-nuova-traccia-InstrumentCombo-popup-2.png` | — | C2 → — | — | Nessuno → nessuno |
| `1920-nuova-traccia-InstrumentCombo.png` | F1 → — | C1 → C1; C2 → — | T → T | Nessuno → nessuno |
| `1920-nuova-traccia-TuningCombo-popup-1.png` | — | — | — | Nessuno → nessuno |
| `1920-nuova-traccia-TuningCombo-popup-2.png` | — | C2 → — | — | Nessuno → nessuno |
| `1920-nuova-traccia-TuningCombo.png` | F1 → — | C1 → C1; C2 → — | T → T | Nessuno → nessuno |
| `1920-nuova-traccia-TypeCombo-popup-1.png` | — | — | — | Nessuno → nessuno |
| `1920-nuova-traccia-TypeCombo-popup-2.png` | — | C2 → — | — | Nessuno → nessuno |
| `1920-nuova-traccia-TypeCombo.png` | F1 → — | C1 → C1; C2 → — | T → T | Nessuno → nessuno |
| `1920-nuova-traccia-popup-1.png` | — | — | — | Nessuno → nessuno |
| `1920-nuova-traccia.png` | F1 → — | C1 → C1 | T → T | Nessuno → nessuno |
| `1920-principale.png` | F1 → — | C1 → C1 | T → T | Nessuno → nessuno |
| `1920-recupera-popup-1.png` | — | — | — | Nessuno → nessuno |
| `1920-recupera.png` | — | — | T → T (ospite) | Nessuno → nessuno |
| `avvio-recuperato.png` | — | — | T, R1 → T | Nessuno → nessuno |
| `minimo-avviso.png` | F1 → — | — | T → T; A1 → —; M1 → M1 | Nessuno → nessuno |
| `minimo-combo-TrackCapoCombo-popup-1.png` | — | C2 → — | — | Nessuno → nessuno |
| `minimo-combo-TrackCapoCombo.png` | F1 → — | C1 → C1; C2 → — | T → T; M1 → M1 | Nessuno → nessuno |
| `minimo-combo-TrackSoundCombo-popup-1.png` | — | C2 → — | — | Nessuno → nessuno |
| `minimo-combo-TrackSoundCombo.png` | F1 → — | C1 → C1; C2 → — | T → T; M1 → M1 | Nessuno → nessuno |
| `minimo-combo-TrackTuningCombo-popup-1.png` | — | C2 → — | — | Nessuno → nessuno |
| `minimo-combo-TrackTuningCombo.png` | F1 → — | C1 → C1; C2 → — | T → T; M1 → M1 | Nessuno → nessuno |
| `minimo-combo-ViewCombo-popup-1.png` | — | C2 → — | — | Nessuno → nessuno |
| `minimo-combo-ViewCombo.png` | F1 → — | C1 → C1; C2 → — | T → T; M1 → M1 | Nessuno → nessuno |
| `minimo-combo-ZoomCombo-popup-1.png` | — | C2 → — | — | Nessuno → nessuno |
| `minimo-combo-ZoomCombo.png` | F1 → — | C1 → C1; C2 → — | T → T; M1 → M1 | Nessuno → nessuno |
| `minimo-combo-battuta-BarDenCombo-popup-1.png` | — | C2 → — | — | Nessuno → nessuno |
| `minimo-combo-battuta-BarDenCombo.png` | F1 → — | C1 → C1; C2 → — | T → T; M1 → M1 | Nessuno → nessuno |
| `minimo-guida-popup-1.png` | — | — | G1, G2 → G2 | Nessuno → nessuno |
| `minimo-guida.png` | — | — | G1, G2 → G2 | Nessuno → nessuno |
| `minimo-menu-file-popup-1.png` | — | — | — | Nessuno → nessuno |
| `minimo-menu-file.png` | F1 → — | C1 → C1 | T → T; M1 → M1 | Nessuno → nessuno |
| `minimo-nuova-traccia-CapoCombo-popup-1.png` | — | — | — | Nessuno → nessuno |
| `minimo-nuova-traccia-CapoCombo-popup-2.png` | — | C2 → — | — | Nessuno → nessuno |
| `minimo-nuova-traccia-CapoCombo.png` | — | —; C2 → — | T → T | Nessuno → nessuno |
| `minimo-nuova-traccia-InstrumentCombo-popup-1.png` | — | — | — | Nessuno → nessuno |
| `minimo-nuova-traccia-InstrumentCombo-popup-2.png` | — | C2 → — | — | Nessuno → nessuno |
| `minimo-nuova-traccia-InstrumentCombo.png` | — | —; C2 → — | T → T | Nessuno → nessuno |
| `minimo-nuova-traccia-TuningCombo-popup-1.png` | — | — | — | Nessuno → nessuno |
| `minimo-nuova-traccia-TuningCombo-popup-2.png` | — | C2 → — | — | Nessuno → nessuno |
| `minimo-nuova-traccia-TuningCombo.png` | — | —; C2 → — | T → T | Nessuno → nessuno |
| `minimo-nuova-traccia-TypeCombo-popup-1.png` | — | — | — | Nessuno → nessuno |
| `minimo-nuova-traccia-TypeCombo-popup-2.png` | — | C2 → — | — | Nessuno → nessuno |
| `minimo-nuova-traccia-TypeCombo.png` | — | —; C2 → — | T → T | Nessuno → nessuno |
| `minimo-nuova-traccia-popup-1.png` | — | — | — | Nessuno → nessuno |
| `minimo-nuova-traccia.png` | — | — | T → T | Nessuno → nessuno |
| `minimo-principale.png` | F1 → — | C1 → C1 | T → T; M1 → M1 | Nessuno → nessuno |
| `minimo-recupera-popup-1.png` | — | — | — | Nessuno → nessuno |
| `minimo-recupera.png` | — | — | T → T (ospite) | Nessuno → nessuno |

## Esecuzione e verifiche

La cattura richiede `GS_SELFTEST=1`, `GS_SELFTEST_UI=1`, `GS_UI_CAPTURE_DIR` esterno al
repository e **GS_SELFTEST_DATA_DIR** con dati sintetici. Il presentatore automatico dei
dialoghi è disponibile solo in questa modalità; sceglie Recupera, fotografa e chiude.
AppPaths mantiene impostazioni, recuperi e log personali fuori dalla prova.
Le fixture usate sono una copia orfana «Bozza recuperata» di quattro battute e un file
«Brano richiesto.gp» con titolo diverso. Il piccolo generatore e i suoi sorgenti sono
nella radice esterna, `fixture-helper/`; crea i dati tramite TabEditor/AutosaveStore.
Senza la copia orfana è stata verificata anche l'apertura ordinaria da CLI.

Nei resoconti di esecuzione sono controllati: documento recuperato non salvato, percorso
ancora vuoto, toast visibile con nome del file non aperto e testo entro la sua larghezza.
Il controllo CLI normalizza entrambi i percorsi (slash e backslash equivalgono su Windows).
`dopo-report.txt` e `cli-verificato-report.txt` terminano con OK. Il test completo è separato,
senza `GS_UI_CAPTURE_DIR`, con `GS_SELFTEST_EXIT=1`: `%TEMP%/gs_selftest.txt`, esito OK,
incluso il timer reale di autosave e le anteprime audio. Build Debug e Release senza avvisi
né errori; suite invariata di 59 test verdi. UseWorkers=false e le guardie MIDI sono invariati.

Ricerca eseguita sui sorgenti, escludendo solamente tema e cartelle generate:

```powershell
rg -n --glob '*.cs' --glob '*.xaml' --glob '!**/Dark.xaml' --glob '!**/obj/**' --glob '!**/bin/**' '(#[0-9A-Fa-f]{3,8}\b|0x[0-9A-Fa-f]{6,8}\b|Color\.(FromRgb|FromArgb|FromScRgb|FromValues)|Colors\.|Brushes\.|ColorConverter|(?:Color|Background|Foreground|Fill|Stroke|BorderBrush)="(?:Black|White|Transparent|Red|Blue|Green|Gray|Grey|Orange|Yellow)")' src
# Nessuna corrispondenza, exit code 1.
```

La lista per mouse e tastiera veri è in [PROVA_MANUALE.md](PROVA_MANUALE.md): non è stata
eseguita automaticamente né sostituita dalle foto.
