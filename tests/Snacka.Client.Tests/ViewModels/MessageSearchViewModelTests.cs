using Moq;
using Snacka.Client.Services;
using Snacka.Client.ViewModels;

namespace Snacka.Client.Tests.ViewModels;

public class MessageSearchViewModelTests : IDisposable
{
    private readonly Mock<IApiClient> _mockApiClient;
    private readonly Guid _communityId;
    private readonly List<MessageSearchResult> _selectedResults;
    private readonly List<object> _closeCalls;

    public MessageSearchViewModelTests()
    {
        _mockApiClient = new Mock<IApiClient>();
        _communityId = Guid.NewGuid();
        _selectedResults = new List<MessageSearchResult>();
        _closeCalls = new List<object>();
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    private MessageSearchViewModel CreateViewModel()
    {
        return new MessageSearchViewModel(
            _mockApiClient.Object,
            _communityId,
            result => _selectedResults.Add(result),
            () => _closeCalls.Add(new object())
        );
    }

    private static MessageResponse CreateMessage(
        Guid? id = null,
        string content = "Test message",
        string authorUsername = "testuser")
    {
        return new MessageResponse(
            Id: id ?? Guid.NewGuid(),
            Content: content,
            AuthorId: Guid.NewGuid(),
            AuthorUsername: authorUsername,
            AuthorEffectiveDisplayName: authorUsername,
            AuthorAvatar: null,
            ChannelId: Guid.NewGuid(),
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            IsEdited: false
        );
    }

    private static MessageSearchResult CreateSearchResult(
        string content = "Test message",
        string channelName = "general")
    {
        return new MessageSearchResult(
            Message: CreateMessage(content: content),
            ChannelName: channelName
        );
    }

    private static MessageSearchResponse CreateSearchResponse(
        int count = 1,
        int totalCount = 1,
        string query = "test")
    {
        var results = Enumerable.Range(0, count)
            .Select(i => CreateSearchResult(content: $"Message {i}"))
            .ToList();

        return new MessageSearchResponse(
            Results: results,
            TotalCount: totalCount,
            Query: query
        );
    }

    #region Initial State Tests

    [Fact]
    public void Constructor_InitialState_IsCorrect()
    {
        // Arrange & Act
        using var vm = CreateViewModel();

        // Assert
        Assert.Equal(string.Empty, vm.SearchQuery);
        Assert.False(vm.IsLoading);
        Assert.Equal(-1, vm.SelectedIndex);
        Assert.Equal(0, vm.TotalCount);
        Assert.Empty(vm.Results);
        Assert.False(vm.HasResults);
        Assert.False(vm.ShowNoResults);
        Assert.Equal("", vm.StatusText);
    }

    #endregion

    #region Search Tests

    [Fact]
    public async Task SearchAsync_WithValidQuery_PopulatesResults()
    {
        // Arrange
        var searchResponse = CreateSearchResponse(count: 3, totalCount: 3);
        _mockApiClient
            .Setup(x => x.SearchMessagesAsync(_communityId, "test", null, null, 25))
            .ReturnsAsync(new ApiResult<MessageSearchResponse> { Success = true, Data = searchResponse });

        using var vm = CreateViewModel();
        vm.SearchQuery = "test";

        // Act
        await vm.SearchAsync();

        // Assert
        Assert.Equal(3, vm.Results.Count);
        Assert.Equal(3, vm.TotalCount);
        Assert.True(vm.HasResults);
        Assert.False(vm.ShowNoResults);
        Assert.Equal("3 results found", vm.StatusText);
    }

    [Fact]
    public async Task SearchAsync_WithSingleResult_ShowsSingularStatusText()
    {
        // Arrange
        var searchResponse = CreateSearchResponse(count: 1, totalCount: 1);
        _mockApiClient
            .Setup(x => x.SearchMessagesAsync(_communityId, "test", null, null, 25))
            .ReturnsAsync(new ApiResult<MessageSearchResponse> { Success = true, Data = searchResponse });

        using var vm = CreateViewModel();
        vm.SearchQuery = "test";

        // Act
        await vm.SearchAsync();

        // Assert
        Assert.Equal("1 result found", vm.StatusText);
    }

    [Fact]
    public async Task SearchAsync_WithNoResults_ShowsNoResults()
    {
        // Arrange
        var searchResponse = new MessageSearchResponse(
            Results: new List<MessageSearchResult>(),
            TotalCount: 0,
            Query: "nonexistent"
        );
        _mockApiClient
            .Setup(x => x.SearchMessagesAsync(_communityId, "nonexistent", null, null, 25))
            .ReturnsAsync(new ApiResult<MessageSearchResponse> { Success = true, Data = searchResponse });

        using var vm = CreateViewModel();
        vm.SearchQuery = "nonexistent";

        // Act
        await vm.SearchAsync();

        // Assert
        Assert.Empty(vm.Results);
        Assert.Equal(0, vm.TotalCount);
        Assert.False(vm.HasResults);
        Assert.True(vm.ShowNoResults);
    }

    [Fact]
    public async Task SearchAsync_WithEmptyQuery_ClearsResults()
    {
        // Arrange
        var searchResponse = CreateSearchResponse(count: 3, totalCount: 3);
        _mockApiClient
            .Setup(x => x.SearchMessagesAsync(_communityId, "test", null, null, 25))
            .ReturnsAsync(new ApiResult<MessageSearchResponse> { Success = true, Data = searchResponse });

        using var vm = CreateViewModel();
        vm.SearchQuery = "test";
        await vm.SearchAsync();
        Assert.Equal(3, vm.Results.Count);

        // Act
        vm.SearchQuery = "";
        await vm.SearchAsync();

        // Assert
        Assert.Empty(vm.Results);
        Assert.Equal(0, vm.TotalCount);
        Assert.False(vm.ShowNoResults); // Should not show "no results" for empty query
    }

    [Fact]
    public async Task SearchAsync_WithFailedApiCall_ClearsResults()
    {
        // Arrange
        _mockApiClient
            .Setup(x => x.SearchMessagesAsync(_communityId, "test", null, null, 25))
            .ReturnsAsync(new ApiResult<MessageSearchResponse> { Success = false, Error = "Server error" });

        using var vm = CreateViewModel();
        vm.SearchQuery = "test";

        // Act
        await vm.SearchAsync();

        // Assert
        Assert.Empty(vm.Results);
        Assert.Equal(0, vm.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_SetsIsLoading_DuringExecution()
    {
        // Arrange
        var isLoadingValues = new List<bool>();
        var tcs = new TaskCompletionSource<ApiResult<MessageSearchResponse>>();
        _mockApiClient
            .Setup(x => x.SearchMessagesAsync(_communityId, "test", null, null, 25))
            .Returns(tcs.Task);

        using var vm = CreateViewModel();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsLoading))
                isLoadingValues.Add(vm.IsLoading);
        };
        vm.SearchQuery = "test";

        // Act
        var searchTask = vm.SearchAsync();
        Assert.True(vm.IsLoading);

        tcs.SetResult(new ApiResult<MessageSearchResponse>
        {
            Success = true,
            Data = CreateSearchResponse()
        });
        await searchTask;

        // Assert
        Assert.False(vm.IsLoading);
        Assert.Equal(2, isLoadingValues.Count);
        Assert.True(isLoadingValues[0]); // Set to true at start
        Assert.False(isLoadingValues[1]); // Set to false at end
    }

    [Fact]
    public async Task SearchAsync_ResetsSelectedIndex()
    {
        // Arrange
        var searchResponse = CreateSearchResponse(count: 3, totalCount: 3);
        _mockApiClient
            .Setup(x => x.SearchMessagesAsync(_communityId, It.IsAny<string>(), null, null, 25))
            .ReturnsAsync(new ApiResult<MessageSearchResponse> { Success = true, Data = searchResponse });

        using var vm = CreateViewModel();
        vm.SearchQuery = "test";
        await vm.SearchAsync();
        vm.SelectedIndex = 1;
        Assert.Equal(1, vm.SelectedIndex);

        // Act
        await vm.SearchAsync();

        // Assert
        Assert.Equal(-1, vm.SelectedIndex);
    }

    #endregion

    #region Navigation Tests

    [Fact]
    public async Task MoveDown_WithNoSelection_SelectsFirstItem()
    {
        // Arrange
        var searchResponse = CreateSearchResponse(count: 3, totalCount: 3);
        _mockApiClient
            .Setup(x => x.SearchMessagesAsync(_communityId, "test", null, null, 25))
            .ReturnsAsync(new ApiResult<MessageSearchResponse> { Success = true, Data = searchResponse });

        using var vm = CreateViewModel();
        vm.SearchQuery = "test";
        await vm.SearchAsync();

        // Act
        vm.MoveDown();

        // Assert
        Assert.Equal(0, vm.SelectedIndex);
    }

    [Fact]
    public async Task MoveDown_WithSelection_MovesToNextItem()
    {
        // Arrange
        var searchResponse = CreateSearchResponse(count: 3, totalCount: 3);
        _mockApiClient
            .Setup(x => x.SearchMessagesAsync(_communityId, "test", null, null, 25))
            .ReturnsAsync(new ApiResult<MessageSearchResponse> { Success = true, Data = searchResponse });

        using var vm = CreateViewModel();
        vm.SearchQuery = "test";
        await vm.SearchAsync();
        vm.SelectedIndex = 0;

        // Act
        vm.MoveDown();

        // Assert
        Assert.Equal(1, vm.SelectedIndex);
    }

    [Fact]
    public async Task MoveDown_AtLastItem_StaysAtLastItem()
    {
        // Arrange
        var searchResponse = CreateSearchResponse(count: 3, totalCount: 3);
        _mockApiClient
            .Setup(x => x.SearchMessagesAsync(_communityId, "test", null, null, 25))
            .ReturnsAsync(new ApiResult<MessageSearchResponse> { Success = true, Data = searchResponse });

        using var vm = CreateViewModel();
        vm.SearchQuery = "test";
        await vm.SearchAsync();
        vm.SelectedIndex = 2;

        // Act
        vm.MoveDown();

        // Assert
        Assert.Equal(2, vm.SelectedIndex);
    }

    [Fact]
    public async Task MoveUp_AtFirstItem_StaysAtFirstItem()
    {
        // Arrange
        var searchResponse = CreateSearchResponse(count: 3, totalCount: 3);
        _mockApiClient
            .Setup(x => x.SearchMessagesAsync(_communityId, "test", null, null, 25))
            .ReturnsAsync(new ApiResult<MessageSearchResponse> { Success = true, Data = searchResponse });

        using var vm = CreateViewModel();
        vm.SearchQuery = "test";
        await vm.SearchAsync();
        vm.SelectedIndex = 0;

        // Act
        vm.MoveUp();

        // Assert
        Assert.Equal(0, vm.SelectedIndex);
    }

    [Fact]
    public async Task MoveUp_WithNoSelection_SelectsFirstItem()
    {
        // Arrange
        var searchResponse = CreateSearchResponse(count: 3, totalCount: 3);
        _mockApiClient
            .Setup(x => x.SearchMessagesAsync(_communityId, "test", null, null, 25))
            .ReturnsAsync(new ApiResult<MessageSearchResponse> { Success = true, Data = searchResponse });

        using var vm = CreateViewModel();
        vm.SearchQuery = "test";
        await vm.SearchAsync();

        // Act
        vm.MoveUp();

        // Assert
        Assert.Equal(0, vm.SelectedIndex);
    }

    #endregion

    #region Selection Tests

    [Fact]
    public async Task SelectCurrent_WithResults_CallsOnResultSelected()
    {
        // Arrange
        var searchResponse = CreateSearchResponse(count: 3, totalCount: 3);
        _mockApiClient
            .Setup(x => x.SearchMessagesAsync(_communityId, "test", null, null, 25))
            .ReturnsAsync(new ApiResult<MessageSearchResponse> { Success = true, Data = searchResponse });

        using var vm = CreateViewModel();
        vm.SearchQuery = "test";
        await vm.SearchAsync();
        vm.SelectedIndex = 1;

        // Act
        vm.SelectCurrent();

        // Assert
        Assert.Single(_selectedResults);
        Assert.Equal(vm.Results[1], _selectedResults[0]);
    }

    [Fact]
    public async Task SelectCurrent_WithNoSelection_SelectsFirstItem()
    {
        // Arrange
        var searchResponse = CreateSearchResponse(count: 3, totalCount: 3);
        _mockApiClient
            .Setup(x => x.SearchMessagesAsync(_communityId, "test", null, null, 25))
            .ReturnsAsync(new ApiResult<MessageSearchResponse> { Success = true, Data = searchResponse });

        using var vm = CreateViewModel();
        vm.SearchQuery = "test";
        await vm.SearchAsync();
        Assert.Equal(-1, vm.SelectedIndex);

        // Act
        vm.SelectCurrent();

        // Assert
        Assert.Single(_selectedResults);
        Assert.Equal(vm.Results[0], _selectedResults[0]);
    }

    [Fact]
    public void SelectCurrent_WithNoResults_DoesNothing()
    {
        // Arrange
        using var vm = CreateViewModel();

        // Act
        vm.SelectCurrent();

        // Assert
        Assert.Empty(_selectedResults);
    }

    [Fact]
    public async Task SelectResult_CallsOnResultSelected()
    {
        // Arrange
        var searchResponse = CreateSearchResponse(count: 3, totalCount: 3);
        _mockApiClient
            .Setup(x => x.SearchMessagesAsync(_communityId, "test", null, null, 25))
            .ReturnsAsync(new ApiResult<MessageSearchResponse> { Success = true, Data = searchResponse });

        using var vm = CreateViewModel();
        vm.SearchQuery = "test";
        await vm.SearchAsync();
        var result = vm.Results[2];

        // Act
        vm.SelectResult(result);

        // Assert
        Assert.Single(_selectedResults);
        Assert.Equal(result, _selectedResults[0]);
    }

    #endregion

    #region Close Tests

    [Fact]
    public void Close_CallsOnClose()
    {
        // Arrange
        using var vm = CreateViewModel();

        // Act
        vm.Close();

        // Assert
        Assert.Single(_closeCalls);
    }

    #endregion

    #region SelectedIndex Clamping Tests

    [Fact]
    public async Task SelectedIndex_Clamped_ToValidRange()
    {
        // Arrange
        var searchResponse = CreateSearchResponse(count: 3, totalCount: 3);
        _mockApiClient
            .Setup(x => x.SearchMessagesAsync(_communityId, "test", null, null, 25))
            .ReturnsAsync(new ApiResult<MessageSearchResponse> { Success = true, Data = searchResponse });

        using var vm = CreateViewModel();
        vm.SearchQuery = "test";
        await vm.SearchAsync();

        // Act & Assert - try to set beyond range
        vm.SelectedIndex = 10;
        Assert.Equal(2, vm.SelectedIndex); // Should be clamped to last index

        vm.SelectedIndex = -5;
        Assert.Equal(-1, vm.SelectedIndex); // Should be clamped to -1
    }

    #endregion
}
