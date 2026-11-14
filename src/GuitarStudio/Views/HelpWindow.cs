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
            ("Spazio", "play / pausa dal cursore"),
            ("Ctrl+N / O", "nuova tablatura / apri"),
            ("Ctrl+S", "salva"),
            ("Ctrl+Maiusc+S", "salva come"),
            ("Ogni 60 secondi", "copia locale di recupero delle modifiche; all'avvio puoi recuperarla dopo un'interruzione. Salva e la chiusura pulita la eliminano"),
            ("F2", "mostra o nasconde la tavolozza (modalità modifica)"),
            ("F1", "questa guida")
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

        var tabBar = new UniformGrid { Rows = 1, Width = 660 };
        var tabHost = new Border
        {
            Background = Res("BgPanel"), CornerRadius = new CornerRadius(7), Padding = new Thickness(3), Child = tabBar,
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(28, 16, 28, 0)
        };

        AddTab(tabBar, "SCORCIATOIE", BuildShortcuts());
        AddTab(tabBar, "EFFETTI: COME FUNZIONANO", BuildEffects());
        AddTab(tabBar, "AUDIO E SUONI", BuildAudio());

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
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false,
            Content = content, Padding = new Thickness(28, 18, 16, 24), Visibility = Visibility.Collapsed
        };
        var tab = new RadioButton
        {
            Content = title, GroupName = "help", Style = (Style)FindResource("InspectorTab"), Padding = new Thickness(10, 0, 10, 0)
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
        var columns = new UniformGrid { Columns = 2, VerticalAlignment = VerticalAlignment.Top };
        var left = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        var right = new StackPanel { Margin = new Thickness(12, 0, 12, 0) };
        columns.Children.Add(left);
        columns.Children.Add(right);

        // Bilancia le due colonne per numero di righe.
        int leftRows = 0, rightRows = 0;
        foreach (var (title, rows) in Shortcuts)
        {
            var card = Card(title);
            var body = (StackPanel)card.Child;
            foreach (var (keys, what) in rows) body.Children.Add(ShortcutRow(keys, what));
            if (leftRows <= rightRows) { left.Children.Add(card); leftRows += rows.Length + 2; }
            else { right.Children.Add(card); rightRows += rows.Length + 2; }
        }
        return columns;
    }

    private Border Card(string title)
    {
        var body = new StackPanel();
        body.Children.Add(new TextBlock
        {
            Text = title, Foreground = Res("Accent"), FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10)
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
        panel.Children.Add(Article("Perché le chitarre suonano «finte»",
            "Il SoundFont incluso (SONiVOX) è piccolo e generico: chitarra classica, acustica, pulita e distorta si assomigliano molto.",
            "Puoi usare un SoundFont General MIDI migliore: menu ☰ → «Scegli un SoundFont…» e seleziona un file .sf2 o .sf3 che hai sul computer. " +
            "Il programma lo ricorda per le volte successive; «Usa il SoundFont incluso» torna a quello di partenza.",
            "Una distorsione credibile richiede comunque un amplificatore simulato: nessun SoundFont la rende alla perfezione."));
        panel.Children.Add(Article("Controllare a occhio cosa viene suonato",
            "Attiva «Manico» nella barra in alto: durante la riproduzione le note suonate si accendono in azzurro sul manico, " +
            "in tempo reale; da fermo mostra in arancione le note del tempo sotto il cursore."));
        panel.Children.Add(Article("Esercitarsi",
            "Velocità: il cursore in alto a destra rallenta o accelera senza cambiare l'intonazione (25–150%).",
            "Loop: attivalo e trascina sulla tab per scegliere il pezzo da ripetere.",
            "Speed trainer: a ogni giro del loop la velocità sale del passo che scegli, fino all'obiettivo.",
            "Metronomo e conteggio iniziale si attivano dai pulsanti accanto al loop."));
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
