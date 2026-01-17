using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Snacka.Client.Services;

namespace Snacka.Client.Controls;

public partial class CommunityDiscoveryModal : UserControl
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<CommunityDiscoveryModal, bool>(nameof(IsOpen));

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<CommunityDiscoveryModal, bool>(nameof(IsLoading));

    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<CommunityDiscoveryModal, string?>(nameof(ErrorMessage));

    public static readonly StyledProperty<ObservableCollection<CommunityResponse>?> CommunitiesProperty =
        AvaloniaProperty.Register<CommunityDiscoveryModal, ObservableCollection<CommunityResponse>?>(nameof(Communities));

    public static readonly StyledProperty<bool> HasNoCommunitiesProperty =
        AvaloniaProperty.Register<CommunityDiscoveryModal, bool>(nameof(HasNoCommunities));

    public static readonly StyledProperty<Guid?> JoiningCommunityIdProperty =
        AvaloniaProperty.Register<CommunityDiscoveryModal, Guid?>(nameof(JoiningCommunityId));

    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<CommunityDiscoveryModal, ICommand?>(nameof(CloseCommand));

    public static readonly StyledProperty<ICommand?> RefreshCommandProperty =
        AvaloniaProperty.Register<CommunityDiscoveryModal, ICommand?>(nameof(RefreshCommand));

    public static readonly StyledProperty<ICommand?> JoinCommunityCommandProperty =
        AvaloniaProperty.Register<CommunityDiscoveryModal, ICommand?>(nameof(JoinCommunityCommand));

    public static readonly StyledProperty<ICommand?> CreateCommunityCommandProperty =
        AvaloniaProperty.Register<CommunityDiscoveryModal, ICommand?>(nameof(CreateCommunityCommand));

    public CommunityDiscoveryModal()
    {
        InitializeComponent();
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public string? ErrorMessage
    {
        get => GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    public ObservableCollection<CommunityResponse>? Communities
    {
        get => GetValue(CommunitiesProperty);
        set => SetValue(CommunitiesProperty, value);
    }

    public bool HasNoCommunities
    {
        get => GetValue(HasNoCommunitiesProperty);
        set => SetValue(HasNoCommunitiesProperty, value);
    }

    public Guid? JoiningCommunityId
    {
        get => GetValue(JoiningCommunityIdProperty);
        set => SetValue(JoiningCommunityIdProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public ICommand? RefreshCommand
    {
        get => GetValue(RefreshCommandProperty);
        set => SetValue(RefreshCommandProperty, value);
    }

    public ICommand? JoinCommunityCommand
    {
        get => GetValue(JoinCommunityCommandProperty);
        set => SetValue(JoinCommunityCommandProperty, value);
    }

    public ICommand? CreateCommunityCommand
    {
        get => GetValue(CreateCommunityCommandProperty);
        set => SetValue(CreateCommunityCommandProperty, value);
    }
}
