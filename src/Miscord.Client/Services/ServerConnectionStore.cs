using System.Text.Json;

namespace Miscord.Client.Services;

public interface IServerConnectionStore
{
    IReadOnlyList<ServerConnection> GetAll();
    ServerConnection? Get(string id);
    void Save(ServerConnection connection);
    void Remove(string id);
    ServerConnection? GetLastConnected();
}

public class ServerConnectionStore : IServerConnectionStore
{
    private readonly string _filePath;
    private List<ServerConnection> _connections = [];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ServerConnectionStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var profile = Program.Profile;
        var miscordDir = string.IsNullOrEmpty(profile)
            ? Path.Combine(appData, "Miscord")
            : Path.Combine(appData, "Miscord", $"profile-{profile}");
        Directory.CreateDirectory(miscordDir);
        _filePath = Path.Combine(miscordDir, "servers.json");
        Load();
    }

    public IReadOnlyList<ServerConnection> GetAll() => _connections.AsReadOnly();

    public ServerConnection? Get(string id) => _connections.FirstOrDefault(c => c.Id == id);

    public void Save(ServerConnection connection)
    {
        var existing = _connections.FindIndex(c => c.Id == connection.Id);
        if (existing >= 0)
            _connections[existing] = connection;
        else
            _connections.Add(connection);
        Persist();
    }

    public void Remove(string id)
    {
        _connections.RemoveAll(c => c.Id == id);
        Persist();
    }

    public ServerConnection? GetLastConnected()
    {
        return _connections
            .Where(c => c.LastConnected.HasValue)
            .OrderByDescending(c => c.LastConnected)
            .FirstOrDefault();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _connections = JsonSerializer.Deserialize<List<ServerConnection>>(json, JsonOptions) ?? [];
            }
        }
        catch
        {
            _connections = [];
        }
    }

    private void Persist()
    {
        try
        {
            var json = JsonSerializer.Serialize(_connections, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Silently fail - storage is best-effort
        }
    }
}
