namespace HotChocolate.Fusion.Shared.Accounts;

public class UserRepository
{
    private readonly Dictionary<int, User> _users;

    public UserRepository()
    {
        _users = new User[]
        {
            new User(1, "Ada Lovelace", new DateTime(1815, 12, 10), "@ada"),
            new User(2, "Alan Turing", new DateTime(1912, 06, 23), "@complete")
        }.ToDictionary(t => t.Id);
    }

    public User? GetUser(int id)
    {
        if (_users.TryGetValue(id, out var value))
        {
            return value;
        }

        return null;
    }

    public IEnumerable<User> GetUsers() => _users.Values;
}
