using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using GuitarStudio.Ui;

namespace GuitarStudio.Views;

/// <summary>Guida rapida: scorciatoie divise per categoria e spiegazione di effetti e audio (F1).</summary>
public sealed class HelpWindow : Window
{
    private static readonly (string Title, (string Keys, string What)[] Rows)[] Shortcuts =
    {
        ("Scrivere le note", new[]
        {
            ("0–9", "tasto sulla corda del cursore; due cifre veloci = tasti 10–24"),
            ("↑ ↓", "cambia corda"),
            ("← →", "tempo precedente / successivo (oltre la fine crea una battuta)"),
            ("Ctrl+← →", "battuta precedente / successiva"),
            ("Home / Fine", "primo / ultimo tempo della battuta"),
            ("Canc / Backspace", "cancella la nota (o la selezione)"),
            ("R", "trasforma il tempo in pausa")
        }),
        ("Due voci nella stessa battuta", new[]
        {
            ("Ctrl+Alt+1 / 2", "seleziona voce 1 (melodia) o voce 2 (accompagnamento)"),
            ("Voce → Durata → Tasto", "ogni voce ha le proprie note, pause e durate: una melodia lunga può continuare sopra un arpeggio"),
            ("Copia / Taglia / Canc", "agiscono sulla voce selezionata; Elimina battuta rimuove tutte le voci")
        }),
        ("Durata", new[]
        {
            ("+ / −", "durata più lunga / più corta"),
            (".", "nota puntata"),
            ("Ctrl+3", "terzina")
        }),
        ("Effetti sulla nota", new[]
        {
            ("H", "hammer-on / pull-off"),
            ("S", "slide legato"),
            ("B", "bend di un tono"),
            ("V", "vibrato"),
            ("P", "palm mute"),
            ("X", "nota stoppata"),
            ("L", "let ring"),
            ("T", "legatura di valore"),
            ("G", "ghost note"),
            ("A", "accento"),
            ("N", "armonico naturale")
        }),
        ("Dinamiche e abbellimenti", new[]
        {
            ("D / Maiusc+D", "dinamica più forte / più debole (ppp … fff), dal tempo sotto il cursore fino al prossimo cambio"),
            ("Tavolozza → Dinamica", "un clic su ppp, pp, p, mp, mf, f, ff o fff; con una selezione vale per tutti i tempi selezionati"),
            ("Ctrl+G", "abbellimento prima del tempo: una nota piccola appena prima della nota sotto il cursore"),
            ("Ctrl+Maiusc+G", "abbellimento sul tempo: la nota piccola cade sul tempo e la nota principale arriva subito dopo"),
            ("Stesso tasto sull'abbellimento", "lo toglie; il tasto dell'altro tipo lo cambia. Scrivi una cifra per cambiare il tasto")
        }),
        ("Accordi e testo", new[]
        {
            ("C", "accordo sopra il tempo: nome e, se vuoi, diagramma proposto dalle note del tempo"),
            ("Maiusc+T", "testo libero sopra il tempo (es. rallentando, Strofa 2)"),
            ("Tavolozza → Accordo… / Testo…", "gli stessi dialoghi; «Togli» rimuove l'accordo o il testo esistente")
        }),
        ("Finali alternativi", new[]
        {
            ("Tavolozza → 1ª volta", "le battute selezionate si suonano solo al primo giro; se manca, aggiunge la fine del ritornello :‖"),
            ("Tavolozza → 2ª volta", "le battute subito dopo la 1ª volta si suonano al secondo giro, al suo posto"),
            ("Stesso pulsante", "toglie il finale dalle battute selezionate")
        }),
        ("Selezione e appunti", new[]
        {
            ("Maiusc+frecce", "estende la selezione"),
            ("Maiusc+clic", "seleziona fino al punto cliccato"),
            ("Ctrl+A", "seleziona tutta la traccia"),
            ("Esc", "annulla la selezione"),
            ("Ctrl+C / X / V", "copia / taglia / incolla"),
            ("Ctrl+Z / Y", "annulla / ripeti")
        }),
        ("Battute e tempi", new[]
        {
            ("Ins", "inserisce un tempo prima del cursore"),
            ("Ctrl+Ins", "aggiunge una battuta dopo questa"),
            ("Ctrl+Canc", "elimina il tempo"),
            ("Ctrl+Maiusc+Canc", "elimina la battuta"),
            ("Ctrl+F", "correggi battuta (pause mancanti o tempi in eccesso)"),
            ("Ctrl+B / Ctrl+T", "apre il pannello Battuta / Traccia")
        }),
        ("File, ascolto e finestra", new[]
        {
            ("Focus nella partitura", "i campi di testo e i selettori conservano i propri tasti; Alt+F4 e Maiusc+F10 mantengono il comportamento di Windows"),
            ("Spazio", "play / pausa dal cursore"),
            ("Maiusc+Spazio", "stop"),
            ("Ctrl+Home", "torna all'inizio del brano"),
            ("Ctrl+Pag↑ / Pag↓", "aumenta o diminuisce la velocità di 5 punti"),
            ("Ctrl+N / O", "nuova tablatura / apri"),
            ("Ctrl+S", "salva"),
            ("Ctrl+Maiusc+S", "salva come"),
            ("Ogni 60 secondi", "copia locale di recupero delle modifiche; all'avvio puoi recuperarla dopo un'interruzione. Salva e la chiusura pulita la eliminano"),
            ("F2", "mostra o nasconde la tavolozza (modalità modifica)"),
            ("F3 / F4 / F6", "mostra o nasconde tracce, proprietà e manico"),
            ("F7 / F8 / F9", "loop, metronomo e conteggio iniziale"),
            ("F10 / Ctrl+F10", "menu Altro / speed trainer"),
            ("Alt+F", "menu File, anche quando è dentro Altro"),
            ("Ctrl+Maiusc+N / D", "aggiunge / elimina una traccia"),
            ("F1", "questa guida")
        }),
        ("Spazio per scrivere", new[]
        {
            ("Solo tab", "vista iniziale con durate, punti e travature sotto le corde; in Tab + spartito il ritmo appare solo sul pentagramma"),
            ("Divisori orizzontali", "trascina il bordo tra partitura, manico e tracce: le altezze vengono ricordate"),
            ("Tab → frecce", "con il divisore selezionato, su e giù regolano l'altezza senza trascinare"),
            ("Altro → Spazio partitura", "riduci le tracce a una riga, regola il manico o ripristina le altezze predefinite"),
            ("Finestra stretta", "Play, Stop, display e velocità restano visibili; File, spostamento e strumenti per esercitarsi sono nel menu Altro"),
            ("Numeri sul manico", "le etichette si diradano se manca spazio; tutti i tasti restano cliccabili e il suggerimento mostra corda e numero"),
            ("Intestazioni della tavolozza", "un clic su VOCE, DURATA, DINAMICA… chiude o riapre la sezione; le sezioni chiuse restano chiuse ai prossimi avvii")
        })
    };

    private readonly Grid _pages = new();
    private readonly List<(RadioButton Tab, UIElement Page)> _tabs = new();

    public HelpWindow()
    {
        Title = "Guida rapida";
        Width = 980;
        Height = 760;
        MinWidth = 640;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = Res("BgDark");
        WindowTheme.Apply(this);

        var header = new DockPanel { Margin = new Thickness(28, 22, 28, 0) };
        var version = new TextBlock
        {
            Text = "GuitarStudio " + AppInfo.VersionLabel, Foreground = Res("TextDim"), FontSize = 13,
            VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 5)
        };
        DockPanel.SetDock(version, Dock.Right);
        header.Children.Add(version);
        header.Children.Add(new TextBlock { Text = "Guida rapida", FontSize = 26, FontWeight = FontWeights.SemiBold, Foreground = Res("TextMain") });

        var tabBar = new UniformGrid { Rows = 1, MaxWidth = 860 };
        var tabHost = new Border
        {
            Background = Res("BgPanel"), CornerRadius = new CornerRadius(7), Padding = new Thickness(3), Child = tabBar,
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(28, 16, 28, 0)
        };

        AddTab(tabBar, "SCORCIATOIE", BuildShortcuts());
        AddTab(tabBar, "EFFETTI: COME FUNZIONANO", BuildEffects());
        AddTab(tabBar, "AUDIO E SUONI", BuildAudio());
        AddTab(tabBar, "FOGLIO DI ACCORDI", BuildChordSheet());

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(tabHost, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(tabHost);
        root.Children.Add(_pages);
        Content = root;

        _tabs[0].Tab.IsChecked = true;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Escape or Key.F1) Close();
        };
    }

    private static Brush Res(string key) => (Brush)Application.Current.FindResource(key);

    private void AddTab(Panel bar, string title, UIElement content)
    {
        var page = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false,
            Content = content, Padding = new Thickness(28, 18, 16, 24), Visibility = Visibility.Collapsed
        };
        var tab = new RadioButton
        {
            Content = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center, Margin = new Thickness(8, 0, 8, 0) },
            GroupName = "help", Style = (Style)FindResource("InspectorTab")
        };
        tab.Checked += (_, _) =>
        {
            foreach (var (t, p) in _tabs) p.Visibility = t == tab ? Visibility.Visible : Visibility.Collapsed;
        };
        _tabs.Add((tab, page));
        bar.Children.Add(tab);
        _pages.Children.Add(page);
    }

    // ---------------------------------------------------------------- scorciatoie

    private UIElement BuildShortcuts()
    {
        var columns = new WrapPanel { VerticalAlignment = VerticalAlignment.Top };
        columns.SizeChanged += (_, _) =>
        {
            var count = columns.ActualWidth >= 840 ? 2 : 1;
            columns.ItemWidth = Math.Max(1, columns.ActualWidth / count);
            foreach (FrameworkElement card in columns.Children)
                card.Margin = new Thickness(0, 0, count == 2 ? 12 : 0, 16);
        };
        foreach (var (title, rows) in Shortcuts)
        {
            var card = Card(title);
            var body = (StackPanel)card.Child;
            foreach (var (keys, what) in rows) body.Children.Add(ShortcutRow(keys, what));
            columns.Children.Add(card);
        }
        return columns;
    }

    private Border Card(string title)
    {
        var body = new StackPanel();
        body.Children.Add(new TextBlock
        {
            Text = title, Foreground = Res("Accent"), FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10)
        });
        return new Border
        {
            Background = Res("BgPanel"), CornerRadius = new CornerRadius(8), Padding = new Thickness(18, 16, 18, 12),
            Margin = new Thickness(0, 0, 0, 16), Child = body
        };
    }

    private UIElement ShortcutRow(string keys, string what)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(176) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var caps = new WrapPanel { VerticalAlignment = VerticalAlignment.Top };
        // "Ctrl+C / X / V" → tasti separati; i simboli di collegamento restano testo.
        foreach (var part in keys.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "/")
            {
                caps.Children.Add(new TextBlock { Text = "/", Foreground = Res("TextDim"), FontSize = 14, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
                continue;
            }
            caps.Children.Add(new Border
            {
                Style = (Style)FindResource("KeyCap"),
                Child = new TextBlock { Text = part, Foreground = Res("TextMain"), FontSize = 14, FontWeight = FontWeights.SemiBold }
            });
        }
        grid.Children.Add(caps);

        var text = new TextBlock
        {
            Text = what, Foreground = Res("TextMain"), FontSize = 14.5, TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0)
        };
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    // ---------------------------------------------------------------- testi

    private UIElement BuildEffects()
    {
        var panel = new StackPanel { MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(Article("La regola generale",
            "Un effetto si applica alla nota sotto il cursore (la casella arancione sulla tab). Se hai selezionato più tempi, " +
            "vale per tutte le note selezionate. Premi di nuovo lo stesso tasto o pulsante per toglierlo.",
            "Se sotto il cursore non c'è una nota, l'effetto non si può applicare: scrivi prima il numero del tasto."));
        panel.Children.Add(Article("Hammer-on, pull-off e slide collegano DUE note",
            "Vanno messi sulla nota di PARTENZA e la collegano alla nota SUCCESSIVA sulla STESSA corda.",
            "Esempio: scrivi 5 sulla corda di Sol, vai al tempo dopo e scrivi 7 sulla stessa corda. Torna sul 5 e premi H: " +
            "compare l'arco e il 7 viene suonato senza pennata. Con S ottieni lo slide dal 5 al 7.",
            "Se dopo non c'è una nota sulla stessa corda l'effetto non si può mettere, e il programma te lo dice con un avviso: " +
            "scrivi prima la nota di arrivo, poi torna sulla nota di partenza.",
            "Slide «S/» (con nuova pennata): come lo slide, ma la nota di arrivo viene ripennata."));
        panel.Children.Add(Article("Legatura di valore (T)",
            "Al contrario di hammer e slide, si mette sulla nota di ARRIVO: la lega alla nota precedente sulla stessa corda, " +
            "che così continua a suonare senza essere ripetuta."));
        panel.Children.Add(Article("Bend",
            "↑1 = un tono, ↑½ = mezzo tono, ↑↓ = bend e rilascio, ⇡↓ = pre-bend (la corda è già tirata) e rilascio.",
            "Il bend dura quanto la nota: per un bend lento usa una nota più lunga."));
        panel.Children.Add(Article("Effetti sul tempo intero",
            "Pennata in giù/su, tremolo picking e tapping valgono per tutte le note del tempo (l'accordo), non per la singola corda."));
        panel.Children.Add(Article("Dinamiche (ppp … fff)",
            "La dinamica dice quanto forte suonare. Come sulla carta, il segno vale dal tempo in cui lo metti fino al prossimo cambio " +
            "nella stessa voce: mettere p a metà brano rende piano tutto quello che segue, finché non trovi un altro segno.",
            "In riproduzione cambia la forza (velocity) delle note: ppp è molto piano, fff molto forte. Il valore del tempo sotto il " +
            "cursore è nel pannello Battuta (Ctrl+B); la riga di stato in basso la mostra solo sui tempi in cui cambia."));
        panel.Children.Add(Article("Note di abbellimento",
            "Mettono una nota piccola, un tono sotto sulla stessa corda, davanti alla nota sotto il cursore; poi puoi scrivere un altro tasto.",
            "«Prima del tempo» (Ctrl+G) ruba un attimo alla nota precedente: la nota principale resta sul tempo. «Sul tempo» " +
            "(Ctrl+Maiusc+G) suona l'abbellimento sul tempo e ritarda appena la nota principale.",
            "Gli abbellimenti non contano nella durata della battuta. Se cancelli la nota principale in fondo alla battuta, " +
            "anche il suo abbellimento viene tolto. Tutto si salva nel file Guitar Pro."));
        panel.Children.Add(Article("Accordi e diagrammi",
            "Con il cursore su un tempo premi C: il dialogo propone il diagramma con le note già scritte in quel tempo, " +
            "dalla corda più grave alla più acuta (x = corda non suonata). Scrivi il nome, ad esempio Do, Am7 o G/B.",
            "Il nome compare sopra il tempo. Se «Mostra il diagramma» è attivo, il diagramma compare in cima alla partitura; " +
            "lo stesso accordo usato più volte ha un solo diagramma. Gli accordi non cambiano il suono: sono un'indicazione da leggere.",
            "Maiusc+T aggiunge un testo libero sopra il tempo. Accordi e testo si salvano nel file Guitar Pro e si annullano con Ctrl+Z."));
        panel.Children.Add(Article("Prima e seconda volta",
            "Servono quando un ritornello finisce in due modi diversi. Seleziona le battute che si suonano solo al primo giro " +
            "e premi «1ª volta»: l'ultima riceve il segno :‖ se non c'era. Poi seleziona le battute che vengono subito dopo e premi «2ª volta».",
            "In riproduzione si sente: parte comune, 1ª volta, di nuovo la parte comune, 2ª volta, poi il resto del brano. " +
            "Il ritornello parte dal segno ‖: (pannello Battuta) oppure dall'inizio del brano.",
            "Se le battute scelte non sono coerenti con i ritornelli, un avviso spiega cosa manca e nulla viene cambiato. " +
            "Con un ritornello ripetuto tre volte puoi mettere 1ª e 2ª volta sulle stesse battute. Il finale della battuta è nel pannello Battuta."));
        return panel;
    }

    private UIElement BuildAudio()
    {
        var panel = new StackPanel { MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(Article("Che cosa senti nella riproduzione",
            "Il motore audio trasforma la tablatura in MIDI e la suona con un SoundFont (una raccolta di campioni).",
            "Vengono riprodotti: altezza e durata di ogni nota, bend, pre-bend e rilascio, vibrato, tremolo picking, armonici, " +
            "legature di valore, let ring, accenti e ghost note (più forte / più piano), palm mute e note stoppate (note accorciate).",
            "Hammer-on, pull-off e slide collegano le due note; il glissato continuo dello slide può non sentirsi come su una chitarra vera: " +
            "è un limite della sintesi MIDI, non un errore della tua tablatura."));
        panel.Children.Add(Article("Anteprima mentre scrivi",
            "La nota appena scritta usa un motore audio separato: non aspetta il MIDI del brano. " +
            "Usa il SoundFont scelto, il suono della traccia, l'accordatura e il capotasto. " +
            "Le due cifre aggiornano subito il tasto: potresti sentire anche la prima cifra.",
            "È una nota breve per controllare l'altezza; per ascoltare durate ed effetti nel loro contesto premi Play. " +
            "Durante il caricamento dei suoni attende l'ultima richiesta ancora valida. In riproduzione l'anteprima è sospesa."));
        panel.Children.Add(Article("I suoni di chitarra e basso",
            "Il SoundFont incluso è GeneralUser GS 1.471 di S. Christian Collins, con campioni distinti per nylon, acciaio, elettriche e bassi. " +
            "È distribuito con la sua licenza v2.0 nella cartella Assets; sorgente e testo integrale sono documentati in docs/SOUNDFONT.md.",
            "Puoi usare un tuo SoundFont General MIDI: menu File → «Scegli un SoundFont…» e seleziona un file .sf2 o .sf3 che hai sul computer. " +
            "Il programma lo ricorda per le volte successive; «Usa il SoundFont incluso» torna a quello di partenza.",
            "Una distorsione credibile richiede comunque un amplificatore simulato: nessun SoundFont la rende alla perfezione."));
        panel.Children.Add(Article("Controllare a occhio cosa viene suonato",
            "Attiva «Manico» nella barra in alto: durante la riproduzione le note suonate si accendono in azzurro sul manico, " +
            "in tempo reale; da fermo mostra in arancione le note del tempo sotto il cursore."));
        panel.Children.Add(Article("Esercitarsi",
            "Velocità: il cursore in alto a destra rallenta o accelera senza cambiare l'intonazione (25–150%).",
            "Loop: attivalo e trascina sulla tab per scegliere il pezzo da ripetere.",
            "Speed trainer: a ogni giro del loop la velocità sale del passo che scegli, fino all'obiettivo.",
            "Metronomo e conteggio iniziale si attivano dai pulsanti accanto al loop. Nella finestra stretta gli stessi comandi sono in Altro → Esercitazione.",
            "Play, Stop, display e velocità rimangono sempre visibili; F10 apre Altro e Alt+F apre File."));
        return panel;
    }

    private static readonly (string Keys, string What)[] ChordSheetKeys =
    {
        ("Ctrl+K", "inserisce un accordo nel punto del cursore del testo (Invio conferma, Esc annulla)"),
        ("Ctrl+Invio", "nuova sezione di testo dopo quella in cui scrivi"),
        ("Ctrl+Maiusc+Invio", "nuova sezione con un giro di soli accordi"),
        ("F5", "passa dalla scrittura alla lettura e ritorno"),
        ("Ctrl+Z / Y", "annulla / ripeti (la scrittura continua è un solo passo)"),
        ("Ctrl+N / O / S", "nuovo foglio, apri, salva; Ctrl+Maiusc+S salva come")
    };

    private UIElement BuildChordSheet()
    {
        var panel = new StackPanel { MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Left };
        var keys = Card("Tasti del foglio di accordi");
        foreach (var (k, what) in ChordSheetKeys) ((StackPanel)keys.Child).Children.Add(ShortcutRow(k, what));
        panel.Children.Add(keys);
        panel.Children.Add(Article("Che cos'è",
            "Un documento diverso dalla tablatura, nella sezione «Accordi» a sinistra: titolo, artista, tonalità, capotasto " +
            "e sezioni con un nome (Intro, Strofa, Ritornello…). Si crea da File → «Nuovo foglio di accordi», anche dalla tablatura, " +
            "che resta aperta così com'è.",
            "Ogni sezione è testo della canzone con gli accordi sopra le sillabe, oppure un giro di soli accordi diviso in battute."));
        panel.Children.Add(Article("Scrivere in fretta",
            "Scrivi il testo come in un qualsiasi editor, una riga per verso. Quando il cursore è davanti alla sillaba giusta premi " +
            "Ctrl+K, scrivi il nome (Do, Lam7, Sol/Si…) e Invio: nel riquadro compare tra parentesi quadre, [Do]Nel mezzo.",
            "Puoi anche scrivere le parentesi a mano. Nei giri ogni riga è una fila di battute: | Do | Sol Lam | Fa |, con più accordi " +
            "nella stessa battuta separati da spazi. Il tipo della sezione si cambia dal suo menu; le frecce la spostano, il cestino la elimina.",
            "Con la finestra larga, a destra c'è l'anteprima sulla carta."));
        panel.Children.Add(Article("Leggere mentre suoni",
            "«Lettura» (o F5) mostra il foglio su carta chiara a caratteri grandi, con gli accordi in grassetto sopra le sillabe. " +
            "La grandezza si sceglie in basso a destra (80–200%); le righe vanno a capo tra le parole.",
            "Il foglio si salva in un file «.accordi»: testo in formato JSON, leggibile anche con il Blocco note. " +
            "Come le tablature, ogni 60 secondi c'è una copia di recupero e i file recenti elencano entrambi i tipi."));
        return panel;
    }

    private Border Article(string title, params string[] paragraphs)
    {
        var card = Card(title);
        var body = (StackPanel)card.Child;
        foreach (var p in paragraphs)
            body.Children.Add(new TextBlock
            {
                Text = p, Foreground = Res("TextMain"), FontSize = 14.5, LineHeight = 22, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });
        return card;
    }
}
