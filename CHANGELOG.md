# Registro delle versioni

La versione corrente è in `src/GuitarStudio/GuitarStudio.csproj` (`<Version>`).
Regole: vedi `AGENTS.md`.

## 0.11.2 — 2026-11-23

- Anche i fogli di accordi (.accordi) si salvano in modo sicuro, con Ctrl+S e con «Salva con nome»:
  il file temporaneo ha un nome unico, quindi un tuo file «nome.accordi.tmp» non viene più
  sovrascritto né cancellato.
- Se il salvataggio non riesce (per esempio il file è aperto in un altro programma) il foglio di
  prima resta intatto, quello aperto resta «modificato» sul percorso di prima e la copia di
  recupero si aggiorna subito. L'avviso dice che il file non è stato toccato e perché.

## 0.11.1 — 2026-11-23

- Salvataggio più sicuro delle tablature (.gp e .atex), sia con Ctrl+S sia con «Salva con nome»:
  il programma scrive prima un file temporaneo nella stessa cartella e sostituisce il tuo file
  solo quando la scrittura è completa. Se qualcosa va storto (disco pieno, file aperto in un altro
  programma, file in sola lettura) il file di prima resta intatto.
- Se il salvataggio non riesce, il brano resta «modificato» (●), il percorso non cambia e la copia
  di recupero viene subito aggiornata con le ultime modifiche. L'avviso dice che il file non è
  stato toccato e perché.

## 0.11.0 — 2026-11-15

- Trasposizione di tutto il foglio per semitoni («♭ −1», «♯ +1», Ctrl+Maiusc+↓/↑): accordi,
  bassi («Re/Fa♯») e tonalità. Diesis o bemolle seguono la nuova tonalità (Mi +4 diventa La♭ in
  Mi maggiore, Sol♯ in Mi minore); senza tonalità si sale con i diesis e si scende con i bemolle.
- Capotasto che cambia le forme: con il capotasto al 2° tasto un brano in La si suona con le
  forme di Sol e suona uguale; la tonalità indicata resta quella reale. Si può anche solo annotare.
- Nomi degli accordi italiani (Do, Re, Mi) o internazionali (C, D, E): un clic riscrive tutto il
  foglio. Si può scrivere in entrambi i modi; in lettura le alterazioni compaiono come ♯ e ♭.
- Diagrammi degli accordi usati in cima al foglio (posizioni aperte o barrè). Dall'anteprima un
  clic apre lo stesso dialogo degli accordi della tablatura per scegliere un'altra diteggiatura.
- Importazione ed esportazione in ChordPro (.cho, .chordpro), formato di testo aperto.
  Guida F1 aggiornata.

## 0.10.0 — 2026-11-15

- Nuovo tipo di documento: il **foglio di accordi**, nella nuova sezione «Accordi» della barra
  laterale. Si crea da File → «Nuovo foglio di accordi» (anche dalla tablatura, che resta aperta).
- Titolo, artista, tonalità, capotasto e sezioni con un nome (Intro, Strofa, Ritornello…).
  Ogni sezione è testo con gli accordi sopra le sillabe oppure un giro di soli accordi in battute;
  le sezioni si aggiungono (Ctrl+Invio, Ctrl+Maiusc+Invio), si spostano, si rinominano e si eliminano.
- Scrittura rapida: scrivi il testo e premi Ctrl+K per mettere l'accordo nel punto del cursore.
  Annulla e ripeti per tutto, con la scrittura continua raccolta in un solo passo.
- Lettura (F5): carta chiara come la partitura, caratteri grandi, grandezza da 80 a 200%.
  Con la finestra larga l'anteprima sulla carta accompagna la scrittura.
- File «.accordi» in testo JSON leggibile; copia di recupero ogni 60 secondi come per le
  tablature; i file recenti elencano tablature e fogli, con il tipo accanto al nome.
  Guida F1: nuova scheda «Foglio di accordi».

## 0.9.1 — 2026-11-15

- Tavolozza: le intestazioni (VOCE, DURATA, DINAMICA…) si chiudono e si riaprono con un clic,
  e il programma ricorda quali hai chiuso. Le otto dinamiche stanno in due righe da quattro e
  le spiegazioni sono più corte: a 1920×1080 con i contenuti al 200% basta chiudere una sezione
  per vedere gli Abbellimenti senza scorrere.
- Etichette dei pulsanti Abbellimenti, Accordo e Testo non più tagliate con la tavolozza stretta.
- Tolta la barra di scorrimento orizzontale che compariva sulla carta quando la partitura era
  più alta della finestra (per esempio con i finali alternativi).
- La riga di stato mostra la dinamica solo sul tempo in cui cambia rispetto al precedente.

## 0.9.0 — 2026-11-15

- Finali alternativi: «1ª volta» e «2ª volta» nella tavolozza, sulle battute selezionate.
  La 1ª volta chiude il ritornello (il segno :‖ si aggiunge se manca); la 2ª va subito dopo.
- La riproduzione segue i finali: parte comune, 1ª volta, ripetizione, 2ª volta, resto del brano.
  Funziona anche con ritornelli ripetuti tre volte (1ª e 2ª volta sulle stesse battute).
- Se le battute scelte non sono coerenti con i ritornelli, un avviso spiega cosa manca e
  niente cambia. Il finale della battuta è nel pannello Battuta; si salva nei file Guitar Pro
  e si annulla con Ctrl+Z senza copiare l'intero brano. Guida F1 aggiornata.

## 0.8.0 — 2026-11-15

- Accordi sopra il tempo (tasto C o «Accordo…» nella tavolozza): nome e, a scelta, diagramma.
  Il dialogo propone il diagramma con le note già scritte nel tempo; lo stesso accordo usato
  più volte ha un solo diagramma in cima alla partitura.
- Testo libero sopra il tempo (Maiusc+T o «Testo…»), ad esempio «rallentando» o «Strofa 2».
- Dialoghi nel tema scuro, con controllo dei valori e messaggio d'errore nel dialogo;
  «Togli» rimuove accordo o testo. Valori mostrati nel pannello Battuta.
- Accordi e testo si salvano nei file Guitar Pro, si annullano con Ctrl+Z e seguono il tempo
  copiato anche in un'altra traccia. Guida F1 aggiornata.

## 0.7.0 — 2026-11-15

- Dinamiche da ppp a fff: otto pulsanti nella tavolozza, D per più forte e Maiusc+D per più
  debole. Il segno vale dal tempo scelto (o dalla selezione) fino al prossimo cambio, si sente
  in riproduzione e compare nel pannello Battuta e nella riga di stato.
- Note di abbellimento prima del tempo (Ctrl+G) e sul tempo (Ctrl+Maiusc+G), un tono sotto la
  nota sotto il cursore e modificabili con le cifre. Non contano nella durata della battuta,
  si sentono in riproduzione e si tolgono con lo stesso comando.
- Dinamiche e abbellimenti si salvano nei file Guitar Pro; annulla e ripeti salvano solo le
  battute coinvolte. Guida F1 aggiornata.

## 0.6.2 — 2026-11-15

- Nuova icona del programma, disegnata apposta: le corde di una tablatura con il cursore
  dell'editor e due note ambra. Compare nella barra del titolo, nella barra delle applicazioni
  e sull'eseguibile; leggibile anche a 16 pixel.
- Misurati memoria e tempi del caricamento dei suoni nei due sintetizzatori: «suoni pronti»
  in circa 0,57 s dall'inizio del caricamento, memoria massima circa 346 MB (sotto 400 MB).
  Numeri e metodo in `docs/PERFORMANCE.md`.

## 0.6.1 — 2026-11-15

- La nota appena scritta suona con un sintetizzatore dedicato, senza attendere il MIDI del brano.
  Su 200 battute e 4 tracce: mediana 29,70 ms, massimo 35,99 ms fino al dispositivo audio.
- Anteprime con suono, volume, posizione stereo, accordatura e capotasto della traccia;
  il SoundFont scelto dal menu viene caricato in entrambi i motori.
- Cifre successive aggiornano subito l'anteprima; annulla, cambio documento e Play
  interrompono quella precedente. Nessuna chiamata PlayNote sul lettore del brano.
- Corretta la coda della vecchia nota durante un cambio rapido. Aggiunti test audio
  e misure fino al campione non silenzioso raggiunto dal dispositivo; guida F1 aggiornata.

## 0.6.0 — 2026-11-15

- Suoni predefiniti sostituiti con GeneralUser GS 1.471 di S. Christian Collins:
  31,3 MB di campioni per chitarre, bassi e gli altri strumenti General MIDI.
- Licenza originale inclusa accanto al SoundFont; provenienza e verifiche in docs/SOUNDFONT.md.
- Scelta del SoundFont personale e ripristino dell'incluso conservati; caricamenti rapidi
  non possono ripristinare lo stato di un caricamento precedente.
- Autotest dei 13 programmi per chitarra e basso: campioni non silenziosi e diversi.

## 0.5.1 — 2026-11-15

- In «Solo tab» le durate e i punti si leggono sotto le corde, con le travature del ritmo.
  In «Tab + spartito» il ritmo rimane soltanto sul pentagramma.
- Scena visiva dedicata e controllo dei due sistemi interi a 1920×1080 con contenuti al 200%.

## 0.5.0 — 2026-11-15

- Barra di trasporto adattiva: Play, Stop, display e velocità rimangono visibili.
  Nella finestra stretta File, spostamento e strumenti per esercitarsi passano in «Altro».
  Icone con nome e scorciatoia; menu e pulsanti condividono lo stato dei comandi.
- Divisori trascinabili per manico e tracce, con altezze ricordate; il mixer si riduce
  a una riga. Le stesse regolazioni sono disponibili da tastiera e in «Altro → Spazio partitura».
- Vista iniziale «Solo tab», a grandezza normale, per vedere almeno due sistemi a 1920×1080
  con contenuti al 200%. «Tab + spartito» resta nel selettore; titolo e accordatura sono nelle proprietà.
- Guida F1 leggibile anche al minimo; etichette del manico diradate senza ridurre i caratteri.
  Tutti i tasti restano cliccabili e il suggerimento mostra corda e numero.
- Protetto lo scorrimento del cursore contro i cicli di aggiornamento; lo scorrimento manuale
  non viene annullato. Attribuzione alphaTab.net leggibile anche mentre si scorre la partitura.
- Prove automatiche su trasporto, divisori, preferenze e scorrimento, con foto prima/dopo
  nelle tre dimensioni e resoconto in `docs/CONTROLLO_VISIVO.md`.

## 0.4.3 — 2026-11-15

- Colori di manico, partitura, selezioni e mixer raccolti nel tema, mantenendo i valori esistenti.
- Lettere e numeri del manico portati ad almeno 13 px; testo selezionato nei menu più leggibile.
  Gli avvisi vanno a capo anche quando lo spazio della partitura è stretto.
- Le schede della guida rispettano la larghezza disponibile nella finestra minima.
- Dopo un recupero, un avviso indica il nome del file richiesto all'avvio che non è stato aperto.
- Controllo visivo a 1920×1080 e alla dimensione minima, con foto prima/dopo tramite autotest
  e PrintWindow, senza controllare mouse o tastiera. Difetti residui in `docs/CONTROLLO_VISIVO.md`.
- Aggiunta una prova a mano di cinque minuti in `docs/PROVA_MANUALE.md`.

## 0.4.2 — 2026-11-14

- Inserire, eliminare e incollare battute intere, anche avanzando oltre la fine del brano,
  non richiede più una copia dell'intera partitura per Annulla. Sul brano di prova da
  200 battute e quattro tracce le operazioni nel modello scendono da circa 600 a 4–10 ms.
- Annulla e Ripeti delle battute conservano voci, tracce, pentagrammi, ritornelli, sezioni,
  cambi di tempo e collegamenti alle note vicine; test di confronto dei file Guitar Pro.
- Ripristinata l'anteprima delle note dopo una modifica: l'ultima nota richiesta suona
  quando il MIDI aggiornato è pronto. Note eliminate e richieste superate vengono scartate.
- Autotest esteso per anteprime e nuovi canali; misure e proposta per ridurre l'attesa
  dell'anteprima in `docs/PERFORMANCE.md` (proposta non applicata).

## 0.4.1 — 2026-11-14

- Scrivere note e annullarle è più rapido sui brani lunghi: le modifiche locali conservano
  le battute coinvolte, invece di esportare tutto il brano a ogni cifra.
- Aggiornamenti audio dopo circa 300 ms dall'ultima modifica e ridisegno accorpato delle
  cifre ravvicinate; le anteprime attendono sempre il MIDI aggiornato.
- Il pannello tracce riusa i controlli e aggiorna le celle interessate; si ricostruisce
  quando cambiano le tracce o il numero di battute.
- Test aggiunti su 200 battute e 4 tracce, annulla/ripeti misti ed effetti tra battute.
  Corrette collisioni degli identificatori e riferimenti a note eliminate.
- Misure in `docs/PERFORMANCE.md`: cifra nell'app da circa 849 a 8 ms per il gestore.
  Il ridisegno completo resta differito e può ancora provocare una pausa visiva.

## 0.4.0 — 2026-11-14

- **Salvataggio automatico di recupero**: ogni 60 secondi le nuove modifiche vengono copiate
  in formato Guitar Pro in `%LOCALAPPDATA%\GuitarStudio\autosave\`, senza cambiare il file originale.
- Dopo un'interruzione, all'avvio puoi recuperare la copia, eliminarla oppure decidere più tardi.
  Una tablatura recuperata resta da salvare: scegli tu dove conservarla.
- Salva e la chiusura pulita eliminano la copia della sessione; annullare la chiusura la conserva.
  Le istanze aperte usano copie separate e non interferiscono tra loro.
- Scrittura atomica per conservare la copia precedente se l'aggiornamento non riesce;
  test aggiunti per copie, recupero, errori di scrittura e isolamento tra sessioni.

## 0.3.1 — 2026-11-14

- Aggiunta una suite di test automatici per note, durate, battute, appunti, voci, tracce,
  annulla/ripeti e salvataggio Guitar Pro, con verifica dei collegamenti tra i tempi.
- «Correggi battuta» completa correttamente anche le battute rimaste corte dopo il taglio
  dei tempi in eccesso e riduce una nota che supera da sola la durata della battuta.
- Le nuove battute e tracce riempiono esattamente anche tempi come 3/8.
- Risolto un errore durante modifiche rapide alle voci: il disegno della partitura poteva
  ricevere collegamenti riferiti a una versione precedente del brano.

## 0.3.0 — 2026-11-14

- **Due voci indipendenti nella stessa traccia**: con «1 ↑» e «2 ↓» nella tavolozza
  puoi scrivere una melodia lunga mentre sotto continua un arpeggio più breve.
  Scorciatoie: Ctrl+Alt+1 e Ctrl+Alt+2. La barra di stato indica la voce selezionata.
- Durate, note, pause, effetti, selezione e copia/incolla seguono la voce scelta.
  Taglia e Canc conservano le note dell'altra voce; «Elimina battuta» elimina tutta la battuta.
- Salvataggio Guitar Pro e riproduzione mantengono le durate indipendenti delle due linee.
- Guida F1 aggiornata con il procedimento per scrivere melodia e accompagnamento.

## 0.2.1 — 2026-11-14

- **Riproduzione dopo le modifiche**: Play usa le note, le durate, i BPM e il suono attuali.
  Prima poteva continuare a riprodurre la vecchia versione del brano, anche le sole pause
  della tablatura appena creata.
- Risolto un errore del lettore dopo l'aggiunta o la rimozione di battute; conservate le
  impostazioni del mixer quando si avvia la riproduzione.
- Se il motore audio non è ancora pronto, Play mostra un avviso invece di non rispondere.

## 0.2.0 — 2026-11-14

Interfaccia rifatta e prime risposte ai problemi segnalati.

- **Tema scuro completo**: menu a tendina, menu, cursori, caselle di spunta, campi di testo, barre
  di scorrimento, suggerimenti e finestre di messaggio non sono più bianchi; anche la barra del
  titolo di Windows è scura. Tutto lo stile sta in `Themes/Dark.xaml`.
- **Leggibilità**: caratteri minimi a 13, testi secondari più chiari, pulsanti della tavolozza
  più grandi.
- **Guida rapida (F1 o pulsante «?»)**: scorciatoie divise per categoria, spiegazione di come
  funzionano gli effetti e di cosa riproduce il motore audio. Tolto il blocco di scorciatoie
  illeggibile dalla tavolozza.
- **Avvisi visibili** sopra lo spartito al posto delle scritte piccole nella barra di stato.
- **Hammer-on, slide e legatura**: se manca la nota di arrivo (o di partenza) il programma lo
  spiega, invece di non fare niente in silenzio.
- **SoundFont a scelta**: menu ☰ → «Scegli un SoundFont…» (.sf2/.sf3), ricordato tra le sessioni.
- **File**: file recenti, trascinamento di una tablatura sulla finestra, apertura da riga di
  comando, esportazione MIDI. Un file non valido non chiude più la tablatura aperta.
- **Pannelli**: tavolozza e pannello proprietà si allargano trascinando il bordo; larghezze,
  posizione e dimensione della finestra vengono ricordate.
- Navigazione laterale compatta con icone; zoom e vista spostati nella barra di stato;
  intestazione del pannello tracce allineata alle colonne; pulsante per eliminare la traccia.
- Pulizia: finestra «Nuova traccia» unificata con `InstrumentCatalog`, build senza avvisi.

## 0.1.0 — 2026-11-14

Prima versione sotto controllo di versione.

- Sezione **Tab & Pratica**: apertura di file Guitar Pro / MusicXML, riproduzione, editor di
  tablature (note, durate, effetti, copia/incolla, battute, tracce, annulla/ripeti), pratica
  (velocità, loop, speed trainer, metronomo, conteggio), manico.
- Sezioni Allenatore, Trascrivi MP3 e Libreria: solo segnaposto.
- La versione mostrata nella barra laterale viene letta dall'assembly.
