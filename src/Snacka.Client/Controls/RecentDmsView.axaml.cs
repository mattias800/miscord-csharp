using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Snacka.Client.Services;
using ReactiveUI;

namespace Snacka.Client.Controls;

/// <summary>
/// Displays a collapsible list of recent direct message conversations,
/// sorted by last message time, with unread counts.
/// </summary>
public partial class RecentDmsView : UserControl
{
    public static readonly StyledProperty<ObservableCollection<ConversationSummary>?> RecentConversationsProperty =
        AvaloniaProperty.Register<RecentDmsView, ObservableCollection<ConversationSummary>?>(nameof(RecentConversations));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<RecentDmsView, bool>(nameof(IsExpanded), true);

    public static readonly StyledProperty<int> TotalUnreadCountProperty =
        AvaloniaProperty.Register<RecentDmsView, int>(nameof(TotalUnreadCount), 0);

    public static readonly StyledProperty<ICommand?> SelectConversationCommandProperty =
        AvaloniaProperty.Register<RecentDmsView, ICommand?>(nameof(SelectConversationCommand));

    public static readonly StyledProperty<ICommand?> ViewAllCommandProperty =
        AvaloniaProperty.Register<RecentDmsView, ICommand?>(nameof(ViewAllCommand));

    public static readonly StyledProperty<ICommand?> ToggleExpandedCommandProperty =
        AvaloniaProperty.Register<RecentDmsView, ICommand?>(nameof(ToggleExpandedCommand));

    public RecentDmsView()
    {
        InitializeComponent();

        // Create internal toggle command if not provided externally
        ToggleExpandedCommand = ReactiveCommand.Create(() => IsExpanded = !IsExpanded);
    }

    public ObservableCollection<ConversationSummary>? RecentConversations
    {
        get => GetValue(RecentConversationsProperty);
        set => SetValue(RecentConversationsProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public int TotalUnreadCount
    {
        get => GetValue(TotalUnreadCountProperty);
        set => SetValue(TotalUnreadCountProperty, value);
    }

    public ICommand? SelectConversationCommand
    {
        get => GetValue(SelectConversationCommandProperty);
        set => SetValue(SelectConversationCommandProperty, value);
    }

    public ICommand? ViewAllCommand
    {
        get => GetValue(ViewAllCommandProperty);
        set => SetValue(ViewAllCommandProperty, value);
    }

    public ICommand? ToggleExpandedCommand
    {
        get => GetValue(ToggleExpandedCommandProperty);
        set => SetValue(ToggleExpandedCommandProperty, value);
    }
}
