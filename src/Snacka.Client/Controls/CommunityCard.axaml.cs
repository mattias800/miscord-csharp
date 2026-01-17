using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Snacka.Client.Services;

namespace Snacka.Client.Controls;

public partial class CommunityCard : UserControl
{
    public static readonly StyledProperty<CommunityResponse?> CommunityProperty =
        AvaloniaProperty.Register<CommunityCard, CommunityResponse?>(nameof(Community));

    public static readonly StyledProperty<bool> IsJoiningProperty =
        AvaloniaProperty.Register<CommunityCard, bool>(nameof(IsJoining));

    public static readonly StyledProperty<ICommand?> JoinCommandProperty =
        AvaloniaProperty.Register<CommunityCard, ICommand?>(nameof(JoinCommand));

    public CommunityCard()
    {
        InitializeComponent();
    }

    public CommunityResponse? Community
    {
        get => GetValue(CommunityProperty);
        set => SetValue(CommunityProperty, value);
    }

    public bool IsJoining
    {
        get => GetValue(IsJoiningProperty);
        set => SetValue(IsJoiningProperty, value);
    }

    public ICommand? JoinCommand
    {
        get => GetValue(JoinCommandProperty);
        set => SetValue(JoinCommandProperty, value);
    }
}
