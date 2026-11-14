# Prova a mano in cinque minuti

Da eseguire con mouse e tastiera veri; le foto dell'autotest non verificano il focus reale.
Usa una tablatura di prova, senza altre istanze aperte. Tempo indicativo: quattro minuti
di operazioni e un minuto di attesa per la copia di recupero.

| # | Cosa fare | Cosa deve succedere |
|---|---|---|
| 1 | Apri l'esercizio demo dal menu ☰ e attiva la modalità modifica. | Tavolozza, cursore nella tab e proprietà sono visibili; nessun controllo chiaro. |
| 2 | Clicca su una nota nella **partitura**, poi premi le quattro frecce. | Sinistra/destra cambiano tempo, su/giù cambiano corda. Il cursore e la barra di stato seguono; il focus resta utilizzabile senza cliccare sullo sfondo. |
| 3 | Clicca su un altro numero nella tab, anche dopo aver fatto scorrere lo spartito. | Si selezionano quel tempo e quella corda, senza uno spostamento rispetto al punto cliccato. |
| 4 | Mostra «Manico», clicca su una corda e un tasto liberi, poi riclicca nello stesso punto. | Compare una nota sulla corda scelta; il secondo clic la toglie. L'anteprima arriva dopo circa 300 ms. |
| 5 | Clicca su un tempo e fai **Maiusc+clic** su un tempo successivo. | L'intervallo è evidenziato fino al tempo cliccato; il resto della traccia resta fuori dalla selezione. |
| 6 | Togli la selezione con un clic. Premi rapidamente **1**, **2**, poi freccia destra e **7**. | Sul primo tempo compare **12**, sul successivo **7**; il 12 non diventa due note separate. |
| 7 | Premi Ctrl+Z e Ctrl+Y. | La modifica sparisce e ritorna; cursore, manico e panoramica restano coerenti. |
| 8 | Cambia durata dalla tavolozza, attiva punto e terzina, poi annulla le modifiche. | Simboli e durata cambiano; Annulla riporta la situazione iniziale senza errori. |
| 9 | Passa alla voce 2 con Ctrl+Alt+2, scrivi una nota e cancellala; torna alla voce 1 con Ctrl+Alt+1. | Si vedono voce e posizione correnti; cancellare nella voce 2 conserva le note della voce 1. |
| 10 | Seleziona alcuni tempi con Maiusc+clic, copia con Ctrl+C e incolla in un'altra battuta; annulla. | Si incolla la selezione nella voce attiva e Annulla rimuove l'incolla. |
| 11 | Trascina i due bordi verticali della partitura per allargare e restringere tavolozza e proprietà. | Entrambi i bordi rispondono, rispettano i limiti e non coprono la partitura; testo e controlli tornano leggibili allargando. |
| 12 | Apri ☰, un menu a tendina, F1 e «Nuova traccia» dal + del mixer; chiudi con Esc/Annulla. | Menu e finestre sono scuri, il focus torna all'editor e non si aggiungono tracce. Al minimo annota i limiti già elencati in CONTROLLO_VISIVO.md. |
| 13 | Salva come `Prova recupero.gp`, chiudi normalmente e riapri il file. | Note e voci sono conservate, il segno di modifica sparisce e non viene proposto un recupero per la sessione chiusa normalmente. |
| 14 | Cambia una nota, **attendi 65 secondi**, termina GuitarStudio da Gestione attività senza chiuderlo normalmente, riavvia e scegli «Recupera». Infine salva con un altro nome. | Compare «Recupera tablatura»; recuperando ritrovi l'ultima modifica, ancora da salvare. Il file originale conserva l'ultima versione salvata; dopo Salva e una chiusura pulita non viene riproposta la copia. |

Segna il numero del controllo e cosa è successo se qualcosa non coincide. I tagli del trasporto,
la guida troppo stretta e l'affollamento dei tasti alti sul manico sono già documentati nel controllo visivo.
