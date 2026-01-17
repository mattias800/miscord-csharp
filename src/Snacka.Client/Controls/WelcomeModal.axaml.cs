using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace Snacka.Client.Controls;

public partial class WelcomeModal : UserControl
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<WelcomeModal, bool>(nameof(IsOpen));

    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<WelcomeModal, ICommand?>(nameof(CloseCommand));

    public static readonly StyledProperty<ICommand?> BrowseCommunitiesCommandProperty =
        AvaloniaProperty.Register<WelcomeModal, ICommand?>(nameof(BrowseCommunitiesCommand));

    public static readonly StyledProperty<ICommand?> CreateCommunityCommandProperty =
        AvaloniaProperty.Register<WelcomeModal, ICommand?>(nameof(CreateCommunityCommand));

    public WelcomeModal()
    {
        InitializeComponent();
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public ICommand? BrowseCommunitiesCommand
    {
        get => GetValue(BrowseCommunitiesCommandProperty);
        set => SetValue(BrowseCommunitiesCommandProperty, value);
    }

    public ICommand? CreateCommunityCommand
    {
        get => GetValue(CreateCommunityCommandProperty);
        set => SetValue(CreateCommunityCommandProperty, value);
    }
}
