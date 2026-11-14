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

## Typography

UiFont per controlli e istruzioni; MonoFont per tempi e posizioni; MusicFont per simboli.
Testi almeno 13 px, spiegazioni brevi e ad alto contrasto. Non affidarsi ai soli simboli:
etichette accessibili e tooltip spiegano voce, durata e scorciatoia.

## Layout

Tavolozza e proprietà ridimensionabili, partitura centrale scorrevole, mixer in basso.
Il selettore delle voci precede le durate: si sceglie la linea da scrivere, poi la durata,
poi il tasto. Due pulsanti compatti rimangono visibili anche con la tavolozza minima di 190 px.

## Elevation & Depth

Pannelli separati da bordi sobri; popup e toast mantengono lo stile scuro esistente.
Gli avvisi si sovrappongono alla partitura senza spostarla.

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

Il recupero automatico usa lo stesso dialogo: Recupera apre una copia da salvare con un
nome scelto dall'utente; Elimina copia la scarta; Più tardi, Esc e chiusura del dialogo la
conservano. La copia locale aggiornata ogni 60 secondi non toglie il segno delle modifiche
non salvate. Solo Salva o la chiusura pulita eliminano la copia della sessione.

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

## Do's and Don'ts

- Riutilizzare ToolToggle, SectionHeader e le risorse del tema, come le durate accanto.
- Confermare lo stato della voce anche nel testo del cursore e nella guida F1.
- Conservare le note delle altre voci e l'attribuzione alphaTab.net.
- Non avviare anteprime audio finché il MIDI non è pronto.
- Non cambiare l'identità visiva o aggiungere controlli WPF chiari per una nuova funzione.
