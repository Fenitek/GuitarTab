---
version: alpha
name: GuitarStudio
description: Editor Windows di tablature, leggibile e denso, con strumenti scuri intorno alla partitura.
colors:
  background: "#1E1F24"
  panel: "#26282F"
  input: "#2E3139"
  primary: "#E8A33D"
  text: "#F0F0F2"
  secondary-text: "#B6B9C3"
  border: "#3D404A"
  danger: "#E0624F"
  paper: "#F4F1EA"
typography:
  sans:
    fontFamily: "Segoe UI Variable Text, Segoe UI"
    fontSize: "13.5px"
  mono:
    fontFamily: "Cascadia Mono, Consolas"
  music:
    fontFamily: "Segoe UI Symbol"
rounded:
  DEFAULT: "5px"
  panel: "6px"
spacing:
  field-gap: "6px"
  panel-padding: "12px"
components:
  button:
    height: "32px"
    backgroundColor: "{colors.primary}"
    textColor: "{colors.background}"
  voice-selector:
    width: "76px"
    backgroundColor: "{colors.panel}"
    textColor: "{colors.text}"
  input:
    backgroundColor: "{colors.input}"
    textColor: "{colors.text}"
  paper:
    backgroundColor: "{colors.paper}"
  caption:
    textColor: "{colors.secondary-text}"
  separator:
    backgroundColor: "{colors.border}"
  warning:
    textColor: "{colors.danger}"
---

# GuitarStudio

## Overview

Programma WPF per un chitarrista che ricopia tablature e le studia al rallentatore su uno
schermo ad alta risoluzione. Interfaccia italiana; riferimento: convenzioni della notazione
musicale (trasporto, tavolozza, partitura, proprietà, mixer), senza riprodurre grafica o marchi
di altri programmi. Fonti: AGENTS.md, HANDOFF.md e il riferimento musicale fornito dall'utente.
La partitura resta il centro dell'applicazione; i controlli servono alla scrittura.

## Colors

Il sistema esistente è canonico: `src/GuitarStudio/Themes/Dark.xaml` → risorse WPF → stili
condivisi → viste. Questo documento rispecchia i valori, non genera un secondo tema.
Mapping: background/BgDark, panel/BgPanel, input/BgInput, primary/Accent, text/TextMain,
secondary-text/TextDim, border/Line, danger/Danger, paper/Paper. Nuovi colori solo nel tema.

Manico: famiglia Fretboard*, Accent per la nota selezionata e PlaybackNote durante Play.
Partitura: Score* per alphaTab, Editor* per cursore e intervallo; ToastBackground e ShadowColor
per gli avvisi. Mixer: TrackPrimary/Bass/Fifth/Sixth, con Accent e PlaybackNote riusati;
TrackActiveRow, TrackRowSeparator e TrackSelectionBorder per lo stato. TransparentFill è condiviso.
Le voci selezionate dei menu usano TextMain e SemiBold: ambra sul grigio evidenziato non
raggiungeva 4,5:1. I valori della palette restano quelli precedenti.

## Typography

UiFont per controlli e istruzioni; MonoFont per tempi e posizioni; MusicFont per simboli.
Testi almeno 13 px, spiegazioni brevi e ad alto contrasto. Non affidarsi ai soli simboli:
etichette accessibili e tooltip spiegano voce, durata e scorciatoia.
Anche il disegno manuale del manico usa 13 px, con 20 px riservati ai numeri inferiori.

## Layout

Tavolozza e proprietà ridimensionabili, partitura centrale scorrevole, mixer in basso.
Il selettore delle voci precede le durate: si sceglie la linea da scrivere, poi la durata,
poi il tasto. Due pulsanti compatti rimangono visibili anche con la tavolozza minima di 190 px.

La barra mantiene Play, Stop, display e velocità nella prima riga. Da 820 DIP interni
mostra una seconda riga completa; sotto quella soglia File, Modifiche, Spostamento ed
Esercitazione confluiscono in Altro. Lo stesso menu ospita Pannelli, Tracce e Spazio partitura
anche nella finestra larga. I MenuItem File cambiano contenitore, senza copie dei comandi.
Nome accessibile, tooltip e scorciatoia accompagnano ogni icona della barra.

Minimo della finestra 880×480 DIP; larghezze iniziali dei pannelli 190/250 DIP.
La vista iniziale è Solo tab al 100%, senza il grande titolo e l'accordatura sopra i sistemi:
quelle informazioni restano nella finestra e nelle proprietà. Tab + spartito e Solo spartito
restano disponibili nel selettore. La partitura mantiene almeno 140 DIP di altezza di layout.
Solo tab mostra esplicitamente durate e travature sotto le corde; le viste con pentagramma
non duplicano il ritmo. Margini verticali di carta 10 DIP e separazione dei sistemi 5+5 DIP
mantengono due sistemi interi a 1920×1080 con contenuti al 200%, senza rimpicciolire i glifi.

Due divisori orizzontali, alti 6 DIP, regolano manico e tracce. Le altezze preferite
(162/34 DIP iniziali) sopravvivono alla chiusura; una finestra più corta le limita senza
perdere la preferenza. Il mixer a 34 DIP mostra una traccia, scorre sulle altre e nasconde
l'intestazione; Aggiungi/Elimina rimangono in Altro → Tracce. I divisori accettano le frecce;
Altro → Spazio partitura offre alternative al trascinamento e il ripristino delle altezze.

La guida passa a una colonna quando lo spazio utile è inferiore a 840 DIP. Il manico misura
le etichette a 13 px, mantiene 4 DIP di distanza e conserva il 24; le etichette intermedie
possono essere omesse, mantenendo tutte le posizioni e il numero nel tooltip.

## Elevation & Depth

Pannelli separati da bordi sobri; popup e toast mantengono lo stile scuro esistente.
Gli avvisi si sovrappongono alla partitura senza spostarla.
Il testo degli avvisi riceve la larghezza disponibile tramite DockPanel: deve andare a capo
anche con i pannelli laterali aperti al minimo della finestra.

## Shapes

Raggio dei controlli 5 px, pannelli 6 px; niente nuovi ornamenti per una singola funzione.

## Components

### Canonical UI Map

| Capacità | Proprietario | Contratto e verifica |
|---|---|---|
| Pulsanti e selettore voci | Dark.xaml / ToolToggle | Stato attivo ambra, hover e focus; nome accessibile; mouse e Ctrl+Alt+1/2 |
| Select/Listbox | Dark.xaml / ComboBox | Popup scuro condiviso; navigazione nativa da tastiera |
| Scrollbar | Dark.xaml / ScrollBar | Tema globale, visibile e trascinabile; nessuna regola locale |
| Toast | PracticeView.Shell.cs / ShowToast | Stesso feedback di salvataggio, effetti e riproduzione |
| Form | PracticeView.Layout.cs / ispettori | Campi e risorse del tema, lingua italiana |
| Dialoghi | Ui/Dialogs.cs | Salvataggio prima della chiusura, pulsanti espliciti e focus |
| Trasporto e overflow | PracticeView.Toolbar.cs / Dark.xaml | Comandi condivisi, stato sincronizzato, nessun controllo principale tagliato |
| Divisori | Dark.xaml / HorizontalPanelSplitter | Mouse, frecce e menu alternativi; altezze in AppSettings |

Il recupero automatico usa lo stesso dialogo: Recupera apre una copia da salvare con un
nome scelto dall'utente; Elimina copia la scarta; Più tardi, Esc e chiusura del dialogo la
conservano. La copia locale aggiornata ogni 60 secondi non toglie il segno delle modifiche
non salvate. Solo Salva o la chiusura pulita eliminano la copia della sessione.
Se il recupero ha precedenza su un file passato all'avvio, un toast nomina il file non aperto.

Le voci hanno tempi e selezioni indipendenti. Cambiare voce mantiene la posizione ritmica
quando possibile. Attivare una voce vuota crea pause modificabili ed è annullabile.
Copia, taglia e cancella agiscono sulla voce attiva; il comando esplicito Elimina battuta
rimuove la battuta in tutte le tracce. Salvare .gp conserva tutte le voci.

Gli edit aggiornano subito stato, cursore e celle della panoramica. Il mixer riusa i suoi
controlli finché tracce e numero battute non cambiano. Il ridisegno completo è accorpato
dopo circa 16 ms; il MIDI dopo circa 300 ms dall'ultimo tasto. Durante l'attesa le anteprime
sono bloccate. Il renderer resta sincrono sul dispatcher, senza worker.
L'ultima anteprima richiesta viene eseguita quando MIDI corrente e suoni sono pronti;
un'altra modifica o il cambio documento la scartano, lo spostamento del cursore la conserva.

Verifica visiva v0.5.0: contenuti al 200% a 1920×1080, 1840×1080 e 1760×960 pixel.
T, G2 e M1 chiusi; due sistemi interi nel profilo principale, senza ridurre i caratteri.
La cornice nativa conserva il DPI del desktop; l'autotest calibra i contenuti a 192 DPI
senza modificare impostazioni dello schermo, mouse o tastiera. Le finestre di prova non
si attivano. Inventario, misure e limiti in docs/CONTROLLO_VISIVO.md.
L'attribuzione originale resta nel rendering; una riga fissa alphaTab.net sulla carta
(LineStrong su Paper, font 13) rimane leggibile anche dopo lo scorrimento.
ScrollChanged ridisegna l'overlay senza avviare altri scroll; le correzioni del cursore
sono limitate a una per giro del dispatcher, con margine stabile e destinazione raggiungibile.

## Do's and Don'ts

- Riutilizzare ToolToggle, SectionHeader e le risorse del tema, come le durate accanto.
- Confermare lo stato della voce anche nel testo del cursore e nella guida F1.
- Conservare le note delle altre voci e l'attribuzione alphaTab.net.
- Non avviare anteprime audio finché il MIDI non è pronto.
- Non cambiare l'identità visiva o aggiungere controlli WPF chiari per una nuova funzione.
