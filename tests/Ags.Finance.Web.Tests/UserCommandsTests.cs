using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Commands;
using Microsoft.Extensions.Configuration;

namespace Ags.Finance.Web.Tests;

/// <summary>Os comandos <c>user create</c> e <c>user reset-password</c> de ponta a ponta, sobre um banco temporário.</summary>
public sealed class UserCommandsTests : IAsyncLifetime
{
    private const string Password = "senha-segura-123";

    private PostgresTestDatabase? _database;

    private PostgresTestDatabase Database => _database ?? throw new InvalidOperationException("Fixture not initialized.");

    public async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    [Fact]
    public async Task CreateStoresTheUserWithOnlyTheHashOfThePassword()
    {
        var terminal = new FakeTerminal("maria", Password, Password);

        var exitCode = await RunAsync(["create"], terminal);

        Assert.Equal(UserCommands.Success, exitCode);
        Assert.Equal("maria", await ScalarAsync("SELECT \"UserName\" FROM identity.users"));
        var hash = await ScalarAsync("SELECT \"PasswordHash\" FROM identity.users");
        Assert.NotEqual(Password, hash);
        Assert.DoesNotContain(Password, hash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateNeverPrintsThePassword()
    {
        var terminal = new FakeTerminal("maria", "curta", Password, "diferente-123", Password, Password);

        await RunAsync(["create"], terminal);

        Assert.DoesNotContain(terminal.Output.Concat(terminal.Errors), message => message.Contains(Password, StringComparison.Ordinal));
        Assert.DoesNotContain(terminal.Output.Concat(terminal.Errors), message => message.Contains("curta", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateReadsThePasswordWithoutEcho()
    {
        var terminal = new FakeTerminal("maria", Password, Password);

        await RunAsync(["create"], terminal);

        Assert.Equal(["Senha: ", "Confirme a senha: "], terminal.PasswordPrompts);
        Assert.Equal(["Nome de usuário: "], terminal.LinePrompts);
    }

    [Fact]
    public async Task CreateRefusesWhenTheUserAlreadyExistsAndPointsToResetPassword()
    {
        await RunAsync(["create"], new FakeTerminal("maria", Password, Password));
        var terminal = new FakeTerminal("joao", Password, Password);

        var exitCode = await RunAsync(["create"], terminal);

        Assert.Equal(UserCommands.UserAlreadyExists, exitCode);
        Assert.Contains(terminal.Errors, message => message.Contains("user reset-password", StringComparison.Ordinal));
        Assert.Equal("1", await ScalarAsync("SELECT COUNT(*) FROM identity.users"));
        Assert.Empty(terminal.LinePrompts);
    }

    [Fact]
    public async Task CreateAsksAgainWhenThePasswordIsShorterThanEightCharacters()
    {
        var terminal = new FakeTerminal("maria", "1234567", Password, Password);

        var exitCode = await RunAsync(["create"], terminal);

        Assert.Equal(UserCommands.Success, exitCode);
        Assert.Contains(terminal.Errors, message => message.Contains("mínimo 8", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateAsksAgainWhenTheConfirmationDiffers()
    {
        var terminal = new FakeTerminal("maria", Password, "outra-senha-123", Password, Password);

        var exitCode = await RunAsync(["create"], terminal);

        Assert.Equal(UserCommands.Success, exitCode);
        Assert.Contains(terminal.Errors, message => message.Contains("não conferem", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateAsksAgainWhenTheUserNameIsEmpty()
    {
        var terminal = new FakeTerminal("  ", "maria", Password, Password);

        var exitCode = await RunAsync(["create"], terminal);

        Assert.Equal(UserCommands.Success, exitCode);
        Assert.Equal("maria", await ScalarAsync("SELECT \"UserName\" FROM identity.users"));
    }

    [Fact]
    public async Task CreateStopsWithoutUserWhenTheInputEnds()
    {
        var terminal = new FakeTerminal("maria", Password);

        var exitCode = await RunAsync(["create"], terminal);

        Assert.Equal(UserCommands.Failed, exitCode);
        Assert.Equal("0", await ScalarAsync("SELECT COUNT(*) FROM identity.users"));
    }

    [Fact]
    public async Task ResetPasswordChangesTheHashAndRenewsTheSecurityStamp()
    {
        await RunAsync(["create"], new FakeTerminal("maria", Password, Password));
        var hashBefore = await ScalarAsync("SELECT \"PasswordHash\" FROM identity.users");
        var stampBefore = await ScalarAsync("SELECT \"SecurityStamp\" FROM identity.users");
        var terminal = new FakeTerminal("nova-senha-456", "nova-senha-456");

        var exitCode = await RunAsync(["reset-password"], terminal);

        Assert.Equal(UserCommands.Success, exitCode);
        Assert.NotEqual(hashBefore, await ScalarAsync("SELECT \"PasswordHash\" FROM identity.users"));
        Assert.NotEqual(stampBefore, await ScalarAsync("SELECT \"SecurityStamp\" FROM identity.users"));
        Assert.DoesNotContain(terminal.Output.Concat(terminal.Errors), message => message.Contains("nova-senha-456", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResetPasswordAsksAgainWhenThePasswordIsShortOrTheConfirmationDiffers()
    {
        await RunAsync(["create"], new FakeTerminal("maria", Password, Password));
        var terminal = new FakeTerminal("curta", "nova-senha-456", "diferente-789", "nova-senha-456", "nova-senha-456");

        var exitCode = await RunAsync(["reset-password"], terminal);

        Assert.Equal(UserCommands.Success, exitCode);
        Assert.Equal(2, terminal.Errors.Count);
    }

    [Fact]
    public async Task ResetPasswordWithoutUserPointsToCreate()
    {
        var terminal = new FakeTerminal();

        var exitCode = await RunAsync(["reset-password"], terminal);

        Assert.Equal(UserCommands.NoUser, exitCode);
        Assert.Contains(terminal.Errors, message => message.Contains("user create", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("delete")]
    [InlineData("create extra")]
    public async Task UnknownUsageShowsHelpAndTouchesNothing(string commandLine)
    {
        var terminal = new FakeTerminal();

        var exitCode = await RunAsync(commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries), terminal);

        Assert.Equal(UserCommands.Usage, exitCode);
        Assert.Contains(terminal.Errors, message => message.StartsWith("Uso:", StringComparison.Ordinal));
        Assert.Empty(await Database.QueryAsync("SELECT schema_name FROM information_schema.schemata WHERE schema_name = 'identity'"));
    }

    private Task<int> RunAsync(string[] arguments, FakeTerminal terminal)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Finance"] = Database.ConnectionString })
            .Build();
        return UserCommandRunner.RunAsync(arguments, configuration, terminal);
    }

    private Task<string> ScalarAsync(string sql)
    {
        return Database.ScalarAsync(sql);
    }

    /// <summary>Terminal com as respostas combinadas de antemão, na ordem em que o comando pergunta.</summary>
    private sealed class FakeTerminal : ITerminal
    {
        private readonly Queue<string> _answers;

        public FakeTerminal(params string[] answers)
        {
            _answers = new Queue<string>(answers);
        }

        public List<string> LinePrompts { get; } = [];

        public List<string> PasswordPrompts { get; } = [];

        public List<string> Output { get; } = [];

        public List<string> Errors { get; } = [];

        public string? ReadLine(string prompt)
        {
            LinePrompts.Add(prompt);
            return _answers.TryDequeue(out var answer) ? answer : null;
        }

        public string? ReadPassword(string prompt)
        {
            PasswordPrompts.Add(prompt);
            return _answers.TryDequeue(out var answer) ? answer : null;
        }

        public void WriteLine(string message)
        {
            Output.Add(message);
        }

        public void WriteError(string message)
        {
            Errors.Add(message);
        }
    }
}
