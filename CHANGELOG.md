# Registro delle versioni

La versione corrente è in `src/GuitarStudio/GuitarStudio.csproj` (`<Version>`).
Regole: vedi `AGENTS.md`.

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
