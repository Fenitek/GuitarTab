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
