using System.Windows;
using System.Windows.Controls;

namespace GuitarStudio.Views;

public partial class ComingSoonView : UserControl
{
    public static readonly DependencyProperty HeadingProperty =
        DependencyProperty.Register(nameof(Heading), typeof(string), typeof(ComingSoonView), new PropertyMetadata(""));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(ComingSoonView), new PropertyMetadata(""));

    public string Heading
    {
        get => (string)GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public ComingSoonView()
    {
        InitializeComponent();
    }
}
