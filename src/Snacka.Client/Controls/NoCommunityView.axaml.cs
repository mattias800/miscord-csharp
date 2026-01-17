using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace Snacka.Client.Controls;

public partial class NoCommunityView : UserControl
{
    public static readonly StyledProperty<ICommand?> CreateCommunityCommandProperty =
        AvaloniaProperty.Register<NoCommunityView, ICommand?>(nameof(CreateCommunityCommand));

    public static readonly StyledProperty<ICommand?> BrowseCommunitiesCommandProperty =
        AvaloniaProperty.Register<NoCommunityView, ICommand?>(nameof(BrowseCommunitiesCommand));

    public NoCommunityView()
    {
        InitializeComponent();
    }

    public ICommand? CreateCommunityCommand
    {
        get => GetValue(CreateCommunityCommandProperty);
        set => SetValue(CreateCommunityCommandProperty, value);
    }

    public ICommand? BrowseCommunitiesCommand
    {
        get => GetValue(BrowseCommunitiesCommandProperty);
        set => SetValue(BrowseCommunitiesCommandProperty, value);
    }
}
