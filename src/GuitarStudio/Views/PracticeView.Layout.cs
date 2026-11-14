using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using AlphaTab.Model;
using GuitarStudio.Editing;
using Track = AlphaTab.Model.Track;

namespace GuitarStudio.Views;

/// <summary>
/// Parte "impaginazione" della sezione: barra superiore, tavolozza, pannello Brano/Traccia/Battuta
/// e pannello tracce in basso.
/// </summary>
public partial class PracticeView
{
    // ================================================================ tavolozza degli effetti

    private static readonly (string Group, (NoteEffect Effect, string Glyph, string Tip)[] Items)[] EffectGroups =
    {
        ("LEGATO E SLIDE", new[]
        {
            (NoteEffect.HammerPull, "H", "Hammer-on / pull-off (H): dalla nota sotto il cursore alla nota successiva sulla stessa corda"),
            (NoteEffect.Slide, "/", "Slide legato (S): dalla nota sotto il cursore alla nota successiva sulla stessa corda"),
            (NoteEffect.SlideShift, "S/", "Slide con nuova pennata: come lo slide, ma la nota di arrivo viene ripennata"),
            (NoteEffect.SlideInBelow, "↗", "Slide in entrata dal basso"),
            (NoteEffect.SlideOutDown, "↘", "Slide in uscita verso il basso"),
            (NoteEffect.Tie, "⁀", "Legatura di valore (T): va messa sulla seconda nota, la unisce a quella precedente sulla stessa corda")
        }),
        ("BEND E VIBRATO", new[]
        {
            (NoteEffect.Bend, "↑1", "Bend di un tono (B)"),
            (NoteEffect.BendHalf, "↑½", "Bend di mezzo tono"),
            (NoteEffect.BendRelease, "↑↓", "Bend e rilascio"),
            (NoteEffect.PrebendRelease, "⇡↓", "Pre-bend e rilascio"),
            (NoteEffect.Vibrato, "∿", "Vibrato (V)"),
            (NoteEffect.VibratoWide, "≋", "Vibrato largo")
        }),
        ("TECNICHE", new[]
        {
            (NoteEffect.PalmMute, "P.M.", "Palm mute (P)"),
            (NoteEffect.Dead, "✕", "Nota stoppata (X)"),
            (NoteEffect.Ghost, "( )", "Ghost note (G)"),
            (NoteEffect.LetRing, "L.R.", "Let ring (L)"),
            (NoteEffect.Staccato, "·", "Staccato"),
            (NoteEffect.Accent, ">", "Accento (A)"),
            (NoteEffect.HeavyAccent, "^", "Accento forte"),
            (NoteEffect.LeftHandTap, "T", "Tapping con la mano sinistra")
        }),
        ("ARMONICI", new[]
        {
            (NoteEffect.HarmonicNatural, "◇", "Armonico naturale (N)"),
            (NoteEffect.HarmonicArtificial, "A.H.", "Armonico artificiale (+12)"),
            (NoteEffect.HarmonicPinch, "P.H.", "Armonico pinch (+12)")
        }),
        ("MANO DESTRA", new[]
        {
            (NoteEffect.PickDown, "⊓", "Pennata in giù (vale per tutto il tempo)"),
            (NoteEffect.PickUp, "V", "Pennata in su (vale per tutto il tempo)"),
            (NoteEffect.Tremolo, "≡", "Tremolo picking"),
            (NoteEffect.Tap, "Tap", "Tapping con la mano destra")
        })
    };

    private readonly List<ToggleButton> _effectToggles = new();

    private void BuildEffectButtons()
    {
        var style = (Style)FindResource("PaletteToggle");
        var header = (Style)FindResource("SectionHeader");
        foreach (var (group, items) in EffectGroups)
        {
            EffectsHost.Children.Add(new TextBlock { Text = group, Style = header });
            var wrap = new WrapPanel();
            foreach (var (effect, glyph, tip) in items)
            {
                var t = new ToggleButton
                {
                    Content = new TextBlock { Text = glyph, FontFamily = (FontFamily)FindResource("MusicFont"), FontSize = glyph.Length > 2 ? 13 : 16 },
                    ToolTip = tip,
                    Tag = effect,
                    Style = style
                };
                t.Click += (_, _) =>
                {
                    ApplyEffect(effect);
                    UpdateEditUi();
                    Focus();
                };
                _effectToggles.Add(t);
                wrap.Children.Add(t);
            }
            EffectsHost.Children.Add(wrap);
        }
    }

    private void Rest_Click(object sender, RoutedEventArgs e) { _editor.MakeRest(); Focus(); }
    private void SelectAll_Click(object sender, RoutedEventArgs e) { _editor.SelectAll(); Focus(); }

    // ================================================================ pannelli

    /// <summary>La modalità modifica coincide con la tavolozza visibile, come nei programmi di notazione.</summary>
    private void SetEditMode(bool on)
    {
        PaletteToggle.IsChecked = on;
        ShowPalette(on);
        if (on)
        {
            // Il cursore si disegna sulla tab: se è nascosta, la mostriamo.
            if (ViewCombo.SelectedIndex == 2) ViewCombo.SelectedIndex = 0;
            if (_editor?.IsEditable == false)
                ShowToast("Questa traccia non è a corde: scegli una chitarra o un basso per modificarla.", ToastKind.Warning);
            Focus();
        }
        UpdateEditUi();
        UpdateOverlay();
    }

    private void PanelToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender == PaletteToggle) SetEditMode(PaletteToggle.IsChecked == true);
        TracksPanel.Visibility = TracksToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ShowInspectorPanel(InspectorToggle.IsChecked == true);
        UpdateEditUi();
        Focus();
    }

    private void FretToggle_Click(object sender, RoutedEventArgs e)
    {
        FretPanel.Visibility = FretToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        UpdateEditUi();
        Focus();
    }

    private void FileMenu_Click(object sender, RoutedEventArgs e)
    {
        FileMenuButton.ContextMenu.PlacementTarget = FileMenuButton;
        FileMenuButton.ContextMenu.Placement = PlacementMode.Bottom;
        FileMenuButton.ContextMenu.IsOpen = true;
    }

    private void ZoomCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || Tab?.Api == null || ZoomCombo.SelectedItem is not ComboBoxItem item) return;
        Api.Settings.Display.Scale = double.Parse((string)item.Tag, CultureInfo.InvariantCulture);
        Api.UpdateSettings();
        Render();
        Focus();
    }

    // ================================================================ trasporto

    private void GoStart_Click(object sender, RoutedEventArgs e)
    {
        if (_editor.Score == null) return;
        _editor.MoveTo(0, 0, _editor.Line);
        if (Api.IsReadyForPlayback) Api.TickPosition = 0;
        Focus();
    }

    private void PrevBar_Click(object sender, RoutedEventArgs e) => JumpBar(-1);
    private void NextBar_Click(object sender, RoutedEventArgs e) => JumpBar(+1);

    private void JumpBar(int delta)
    {
        if (_editor.Score == null) return;
        _editor.MoveTo(_editor.BarIndex + delta, 0, _editor.Line);
        if (_playing && _editor.CurrentBeat is { } b) Api.TickPosition = b.AbsolutePlaybackStart;
        Focus();
    }

    // Speed trainer --------------------------------------------------

    private bool _trainerOn;

    private void TrainerToggle_Click(object sender, RoutedEventArgs e)
    {
        TrainerToggle.IsChecked = _trainerOn; // lo stato lo decide il pulsante nel riquadro
        TrainerActionButton.Content = _trainerOn ? "Disattiva" : "Attiva";
        TrainerPopup.IsOpen = true;
    }

    private void TrainerStart_Click(object sender, RoutedEventArgs e)
    {
        _trainerOn = !_trainerOn;
        TrainerToggle.IsChecked = _trainerOn;
        TrainerPopup.IsOpen = false;
        if (_trainerOn)
        {
            // Lo speed trainer lavora sul loop.
            LoopToggle.IsChecked = true;
            Api.IsLooping = true;
            SetSpeed(ReadInt(TrainerStart, 60));
            ShowToast("Speed trainer attivo: trascina sulla tab per scegliere il pezzo da ripetere, poi premi Play.", ToastKind.Ok);
        }
        else
        {
            StatusText.Text = "Speed trainer disattivato.";
        }
        Focus();
    }

    private void TrainerPopup_Closed(object sender, EventArgs e) => TrainerToggle.IsChecked = _trainerOn;

    // ================================================================ display e titolo

    private void UpdateTitle()
    {
        var score = _editor.Score;
        if (score == null) return;
        var name = string.IsNullOrWhiteSpace(score.Artist) ? score.Title : $"{score.Artist} – {score.Title}";
        if (string.IsNullOrWhiteSpace(name)) name = "Senza titolo";
        if (Window.GetWindow(this) is { } w)
            w.Title = $"GuitarStudio — {name}{(_editor.IsDirty ? "  ●" : "")}";
    }

    private void UpdateLcd(Beat? playing = null)
    {
        if (_editor?.Score is not { } score) return;
        var track = _editor.Track;
        LcdTrack.Text = track == null ? "—" : $"{_editor.TrackIndex + 1}. {track.Name}{(_editor.IsDirty ? "  ●" : "")}";
        var bar = playing != null ? (int)playing.Voice.Bar.Index : _editor.BarIndex;
        bar = Math.Clamp(bar, 0, score.MasterBars.Count - 1);
        var mb = score.MasterBars[bar];
        LcdBar.Text = $"{bar + 1}/{score.MasterBars.Count}";
        LcdSig.Text = $"{mb.TimeSignatureNumerator}/{mb.TimeSignatureDenominator}";
        LcdTempo.Text = $"♩ = {_editor.TempoAt(bar):0}";
    }

    // ================================================================ pannello Brano / Traccia / Battuta

    private bool _updatingInspector;
    private string? _trackKey;
    private string? _barKey;
    private int _revision;

    private void InspectorTab_Checked(object sender, RoutedEventArgs e)
    {
        if (SongPage == null) return;
        SongPage.Visibility = TabSong.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        TrackPage.Visibility = TabTrack.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        BarPage.Visibility = TabBar.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowInspector(RadioButton tab)
    {
        InspectorToggle.IsChecked = true;
        ShowInspectorPanel(true);
        UpdateEditUi();
        tab.IsChecked = true;
    }

    private void UpdateInspector()
    {
        if (_editor?.Score is not { } score || InspectorPanel.Visibility != Visibility.Visible) return;
        _updatingInspector = true;
        try
        {
            // Brano
            if (!TitleBox.IsKeyboardFocused) TitleBox.Text = score.Title;
            if (!ArtistBox.IsKeyboardFocused) ArtistBox.Text = score.Artist;
            if (!TempoStartBox.IsKeyboardFocused) TempoStartBox.Text = _editor.TempoAt(0).ToString("0", CultureInfo.InvariantCulture);
            SongSummary.Text = $"{score.MasterBars.Count} battute · {score.Tracks.Count} tracce\n" +
                               string.Join("\n", score.Tracks.Select(t => $"• {t.Name} — {InstrumentCatalog.InstrumentName((int)t.PlaybackInfo.Program)}"));

            // Traccia (si ricarica solo se è cambiata, per non disturbare chi sta scrivendo)
            if (_editor.Track is { } track && _editor.Staff is { } staff)
            {
                var key = $"{score.GetHashCode()}|{_editor.TrackIndex}|{track.Name}|{track.PlaybackInfo.Program}|{string.Join(",", staff.Tuning)}|{staff.Capo}";
                if (key != _trackKey)
                {
                    _trackKey = key;
                    FillTrackPage(track, staff);
                }
            }

            // Battuta
            var barKey = $"{score.GetHashCode()}|{_editor.BarIndex}|{_revision}";
            if (barKey != _barKey)
            {
                _barKey = barKey;
                FillBarPage();
            }
        }
        finally
        {
            _updatingInspector = false;
        }
    }

    private void FillTrackPage(Track track, Staff staff)
    {
        TrackNameBox.Text = track.Name;
        var editable = _editor.IsEditable;
        TrackSoundCombo.IsEnabled = TrackTuningCombo.IsEnabled = TrackCapoCombo.IsEnabled = editable;

        TrackSoundCombo.Items.Clear();
        var program = (int)track.PlaybackInfo.Program;
        foreach (var (p, name) in InstrumentCatalog.Instruments)
            TrackSoundCombo.Items.Add(new ComboBoxItem { Content = name, Tag = p });
        if (InstrumentCatalog.Instruments.All(i => i.Program != program))
            TrackSoundCombo.Items.Add(new ComboBoxItem { Content = $"Strumento MIDI {program}", Tag = program });
        TrackSoundCombo.SelectedItem = TrackSoundCombo.Items.Cast<ComboBoxItem>().First(i => (int)i.Tag == program);

        TrackTuningCombo.Items.Clear();
        if (editable)
        {
            var tuning = staff.Tuning;
            var presets = InstrumentCatalog.PresetsFor(tuning.Count, InstrumentCatalog.IsBassTuning(tuning));
            ComboBoxItem? selected = null;
            if (!presets.Any(p => p.Tunings.SequenceEqual(tuning)))
            {
                selected = new ComboBoxItem
                {
                    Content = InstrumentCatalog.Describe(staff.StringTuning.Name, tuning),
                    Tag = new Tuning(staff.StringTuning.Name, tuning.ToList(), false)
                };
                TrackTuningCombo.Items.Add(selected);
            }
            foreach (var p in presets)
            {
                var item = new ComboBoxItem { Content = InstrumentCatalog.Describe(p), Tag = p };
                TrackTuningCombo.Items.Add(item);
                if (p.Tunings.SequenceEqual(tuning)) selected = item;
            }
            TrackTuningCombo.SelectedItem = selected;
        }

        if (TrackCapoCombo.Items.Count == 0)
            for (var i = 0; i <= 12; i++) TrackCapoCombo.Items.Add(i == 0 ? "nessuno" : $"{i}° tasto");
        TrackCapoCombo.SelectedIndex = Math.Clamp((int)staff.Capo, 0, 12);

        TrackInfo.Text = editable
            ? $"{staff.Tuning.Count} corde. Cambiare accordatura non sposta i numeri nella tab: cambia il suono delle note."
            : "Traccia non a corde (es. batteria): si può solo ascoltare.";
    }

    private void FillBarPage()
    {
        var info = _editor.GetBarInfo();
        BarHeader.Text = $"Battuta {_editor.BarIndex + 1}";
        BarTempoCheck.IsChecked = info.TempoChange != null;
        BarTempoBox.Text = (info.TempoChange ?? info.CurrentTempo).ToString("0", CultureInfo.InvariantCulture);
        BarNumBox.Text = info.Numerator.ToString();
        BarDenCombo.SelectedIndex = info.Denominator switch { 2 => 0, 8 => 2, 16 => 3, _ => 1 };
        BarRepeatStartCheck.IsChecked = info.RepeatStart;
        BarRepeatEndCheck.IsChecked = info.RepeatCount > 0;
        BarRepeatCountBox.Text = info.RepeatCount > 0 ? info.RepeatCount.ToString() : "2";
        BarSectionBox.Text = info.Section ?? "";
    }

    // Brano -----------------------------------------------------------

    private void SongField_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        CommitSongInfo();
        Focus();
    }

    private void SongField_LostFocus(object sender, RoutedEventArgs e) => CommitSongInfo();

    private void CommitSongInfo()
    {
        if (_editor?.Score == null || _updatingInspector) return;
        _editor.SetTitle(TitleBox.Text.Trim());
        _editor.SetArtist(ArtistBox.Text.Trim());
        if (double.TryParse(TempoStartBox.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var bpm)
            && bpm is >= 20 and <= 400 && Math.Abs(bpm - _editor.TempoAt(0)) > 0.01)
            _editor.SetStartTempo(bpm);
    }

    // Traccia ---------------------------------------------------------

    private void TrackField_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        TrackField_Changed(sender, e);
        Focus();
    }

    private void TrackField_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingInspector || _editor?.Track is not { } track || _editor.Staff is not { } staff) return;
        var name = TrackNameBox.Text.Trim();
        if (name.Length == 0) name = track.Name;
        var program = TrackSoundCombo.SelectedItem is ComboBoxItem s ? (int)s.Tag : (int)track.PlaybackInfo.Program;
        var tuning = TrackTuningCombo.SelectedItem is ComboBoxItem t ? (Tuning)t.Tag : staff.StringTuning;
        var capo = Math.Max(0, TrackCapoCombo.SelectedIndex);

        var same = name == track.Name && program == (int)track.PlaybackInfo.Program &&
                   tuning.Tunings.SequenceEqual(staff.Tuning) && capo == (int)staff.Capo;
        if (same) return;
        _editor.SetTrackProperties(name, program, tuning.Tunings.ToList(), tuning.Name, capo);
        StatusText.Text = $"Traccia aggiornata: {name}.";
    }

    // Battuta ---------------------------------------------------------

    private void BarApply_Click(object sender, RoutedEventArgs e)
    {
        if (_editor?.Score == null) return;
        double? tempo = null;
        if (BarTempoCheck.IsChecked == true)
        {
            if (!double.TryParse(BarTempoBox.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var t) || t < 20 || t > 400)
            {
                ShowToast("Il BPM deve essere un numero tra 20 e 400.", ToastKind.Warning);
                return;
            }
            tempo = t;
        }
        if (!int.TryParse(BarNumBox.Text, out var num) || num < 1 || num > 32)
        {
            ShowToast("Il numero di movimenti deve essere tra 1 e 32.", ToastKind.Warning);
            return;
        }
        var den = int.Parse((string)((ComboBoxItem)BarDenCombo.SelectedItem).Content);
        var repeat = 0;
        if (BarRepeatEndCheck.IsChecked == true && (!int.TryParse(BarRepeatCountBox.Text, out repeat) || repeat < 2 || repeat > 20))
        {
            ShowToast("Le ripetizioni devono essere tra 2 e 20.", ToastKind.Warning);
            return;
        }
        _editor.SetBarInfo(new BarInfo(tempo, tempo ?? 0, num, den, BarRepeatStartCheck.IsChecked == true, repeat,
            string.IsNullOrWhiteSpace(BarSectionBox.Text) ? null : BarSectionBox.Text.Trim()));
        _barKey = null;
        StatusText.Text = $"Battuta {_editor.BarIndex + 1} aggiornata.";
        Focus();
    }

    // ================================================================ pannello tracce

    private static readonly string[] TrackBrushKeys = ["TrackPrimary", "Accent", "PlaybackNote", "TrackBass", "TrackFifth", "TrackSixth"];
    private Brush TrackBrush(int index) => (Brush)FindResource(TrackBrushKeys[index % TrackBrushKeys.Length]);

    private readonly List<(Border Row, List<Border> Cells)> _trackRows = new();
    private readonly Dictionary<int, double> _trackVolumes = new();
    private sealed record TrackPanelEntry(Track Track, string Name, double Program);
    private TrackPanelEntry[] _trackPanelEntries = [];
    private int _trackPanelBars = -1;
    private int _tracksPanelBuildCount;
    private (int Track, int Bar) _trackHighlight = (-1, -1);

    private void RebuildTracksPanel()
    {
        if (_editor?.Score is { } current)
        {
            var entries = current.Tracks.Select(t => new TrackPanelEntry(t, t.Name, t.PlaybackInfo.Program)).ToArray();
            if (_trackPanelBars == current.MasterBars.Count && _trackPanelEntries.SequenceEqual(entries))
            {
                RefreshTrackCells();
                RefreshTrackHighlight();
                return;
            }
            _trackPanelEntries = entries;
            _trackPanelBars = current.MasterBars.Count;
        }
        _tracksPanelBuildCount++;
        _trackHighlight = (-1, -1);
        TrackRows.Children.Clear();
        _trackRows.Clear();
        if (_editor?.Score is not { } score) return;

        for (var i = 0; i < score.Tracks.Count; i++)
        {
            var track = score.Tracks[i];
            var index = i;
            var brush = TrackBrush(i);

            var grid = new Grid { Height = 34 };
            AddTrackColumns(grid);

            grid.Children.Add(new Border { Background = brush });

            var name = new Button
            {
                Content = new TextBlock { Text = $"{i + 1}. {track.Name}", TextTrimming = TextTrimming.CharacterEllipsis },
                Style = (Style)FindResource("IconButton"),
                FontFamily = (FontFamily)FindResource("UiFont"),
                FontSize = 13.5,
                Width = double.NaN,
                Height = 30,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 0, 0, 0),
                ToolTip = InstrumentCatalog.InstrumentName((int)track.PlaybackInfo.Program)
            };
            name.Click += (_, _) => SelectTrackFromPanel(index, null);
            Grid.SetColumn(name, 1);
            grid.Children.Add(name);

            var mute = MakeSmallToggle("M", "Silenzia (mute)", track.PlaybackInfo.IsMute);
            mute.Click += (_, _) =>
            {
                track.PlaybackInfo.IsMute = mute.IsChecked == true;
                Api.ChangeTrackMute(new List<Track> { track }, mute.IsChecked == true);
                Focus();
            };
            Grid.SetColumn(mute, 2);
            grid.Children.Add(mute);

            var solo = MakeSmallToggle("S", "Solo: ascolta solo questa traccia", track.PlaybackInfo.IsSolo);
            solo.Click += (_, _) =>
            {
                track.PlaybackInfo.IsSolo = solo.IsChecked == true;
                Api.ChangeTrackSolo(new List<Track> { track }, solo.IsChecked == true);
                Focus();
            };
            Grid.SetColumn(solo, 3);
            grid.Children.Add(solo);

            var volume = new Slider
            {
                Minimum = 0, Maximum = 2, Value = _trackVolumes.GetValueOrDefault(i, 1),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0),
                Focusable = false, ToolTip = "Volume della traccia"
            };
            volume.ValueChanged += (_, e) =>
            {
                _trackVolumes[index] = e.NewValue;
                Api.ChangeTrackVolume(new List<Track> { track }, e.NewValue);
            };
            Grid.SetColumn(volume, 4);
            grid.Children.Add(volume);

            // Panoramica: una casella per battuta, piena se ci sono note.
            var cells = new List<Border>();
            var bars = new UniformGrid { Rows = 1, Columns = score.MasterBars.Count, Margin = new Thickness(0, 7, 12, 7) };
            var staff = track.Staves.Count > 0 ? track.Staves[0] : null;
            for (var b = 0; b < score.MasterBars.Count; b++)
            {
                var bar = b;
                var hasNotes = staff != null && b < staff.Bars.Count &&
                               staff.Bars[b].Voices.Any(v => v.Beats.Any(x => x.Notes.Count > 0));
                var cell = new Border
                {
                    Background = hasNotes ? brush : (Brush)FindResource("BgDeep"),
                    Tag = hasNotes,
                    Margin = new Thickness(0.5, 0, 0.5, 0),
                    CornerRadius = new CornerRadius(1.5),
                    BorderBrush = (Brush)FindResource("TrackSelectionBorder"),
                    Cursor = Cursors.Hand,
                    ToolTip = $"Battuta {b + 1}" + (score.MasterBars[b].Section is { } sec ? $" — {sec.Text}" : "")
                };
                cell.MouseLeftButtonDown += (_, e) =>
                {
                    SelectTrackFromPanel(index, bar);
                    e.Handled = true;
                };
                cells.Add(cell);
                bars.Children.Add(cell);
            }
            Grid.SetColumn(bars, 6);
            grid.Children.Add(bars);

            var row = new Border { Child = grid, BorderBrush = (Brush)FindResource("TrackRowSeparator"), BorderThickness = new Thickness(0, 0, 0, 1) };
            _trackRows.Add((row, cells));
            TrackRows.Children.Add(row);
        }
        RefreshTrackHighlight();
    }

    private void RefreshTrackCells()
    {
        if (_editor.Score is not { } score) return;
        var bars = _editor.ChangedBars ?? Enumerable.Range(0, score.MasterBars.Count).ToArray();
        for (var t = 0; t < _trackRows.Count; t++)
        {
            var staff = score.Tracks[t].Staves.FirstOrDefault();
            foreach (var b in bars)
            {
                var cell = _trackRows[t].Cells[b];
                var hasNotes = staff != null && staff.Bars[b].Voices.Any(v => v.Beats.Any(beat => beat.Notes.Count > 0));
                if (cell.Tag is not bool previous || previous != hasNotes)
                {
                    cell.Background = hasNotes ? TrackBrush(t) : (Brush)FindResource("BgDeep");
                    cell.Tag = hasNotes;
                }
                var tip = $"Battuta {b + 1}" + (score.MasterBars[b].Section is { } sec ? $" — {sec.Text}" : "");
                if (!Equals(cell.ToolTip, tip)) cell.ToolTip = tip;
            }
        }
    }

    private ToggleButton MakeSmallToggle(string text, string tip, bool isChecked) => new()
    {
        Content = new TextBlock { Text = text, FontFamily = (FontFamily)FindResource("UiFont"), FontWeight = FontWeights.SemiBold, FontSize = 13 },
        Style = (Style)FindResource("IconToggle"),
        MinWidth = 28, Height = 26, Padding = new Thickness(0),
        ToolTip = tip, IsChecked = isChecked
    };

    private void RefreshTrackHighlight()
    {
        var next = (_editor.TrackIndex, _editor.BarIndex);
        if (_trackHighlight == next) return;
        var previous = _trackHighlight;
        _trackHighlight = next;
        for (var i = 0; i < _trackRows.Count; i++)
        {
            var (row, cells) = _trackRows[i];
            var current = i == _editor.TrackIndex;
            row.Background = (Brush)FindResource(current ? "TrackActiveRow" : "TransparentFill");
            if (previous.Track == i && previous.Bar >= 0 && previous.Bar < cells.Count)
                cells[previous.Bar].BorderThickness = new Thickness(0);
            if (current && _editor.BarIndex < cells.Count)
                cells[_editor.BarIndex].BorderThickness = new Thickness(1.5);
        }
    }

    private void SelectTrackFromPanel(int trackIndex, int? bar)
    {
        if (_editor.Score == null) return;
        if (trackIndex != _editor.TrackIndex)
        {
            _editor.SelectTrack(trackIndex);
            Render();
        }
        if (bar is { } b) JumpBar(b - _editor.BarIndex);
        _trackKey = null;
        UpdateEditUi();
        Focus();
    }
}
