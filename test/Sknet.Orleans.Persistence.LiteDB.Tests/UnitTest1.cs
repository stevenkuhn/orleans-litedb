using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Orleans;
using Orleans.Configuration;
using Orleans.TestingHost;
using System.Runtime.ExceptionServices;
using Xunit.Abstractions;

namespace Sknet.Orleans.Persistence.LiteDB.Tests;

public class GrainTests
{
    private readonly ITestOutputHelper output;
    private readonly string grainNamespace;
    private readonly BaseTestClusterFixture fixture;
    protected readonly ILogger logger;
    protected TestCluster HostedCluster { get; private set; }

    public GrainTests(ITestOutputHelper output, BaseTestClusterFixture fixture, string grainNamespace = "UnitTests.Grains")
    {
        this.output = output;
        this.fixture = fixture;
        this.grainNamespace = grainNamespace;
        this.logger = fixture.Logger;
        HostedCluster = fixture.HostedCluster;
        GrainFactory = fixture.GrainFactory;
    }

    public IGrainFactory GrainFactory { get; }

    [Fact]
    public async void Test1()
    {
        // Arrange
        var id = Guid.NewGuid();
        var grain = this.GrainFactory.GetGrain<IGrainStorageTestGrain>(id, this.grainNamespace);

        // Act
        var result = await grain.GetValue();

        // Assert
        Assert.Equal(0, result);
    }
}

public abstract class BaseTestClusterFixture : Xunit.IAsyncLifetime
{
    private readonly ExceptionDispatchInfo preconditionsException;

    static BaseTestClusterFixture()
    {
        TestDefaultConfiguration.InitializeDefaults();
    }

    protected BaseTestClusterFixture()
    {
        try
        {
            CheckPreconditionsOrThrow();
        }
        catch (Exception ex)
        {
            this.preconditionsException = ExceptionDispatchInfo.Capture(ex);
            return;
        }
    }

    public void EnsurePreconditionsMet()
    {
        this.preconditionsException?.Throw();
    }

    protected virtual void CheckPreconditionsOrThrow() { }

    protected virtual void ConfigureTestCluster(TestClusterBuilder builder)
    {
    }

    public TestCluster HostedCluster { get; private set; }

    public IGrainFactory GrainFactory => this.HostedCluster?.GrainFactory;

    public IClusterClient Client => this.HostedCluster?.Client;

    public ILogger Logger { get; private set; }

    public string GetClientServiceId() => Client.ServiceProvider.GetRequiredService<IOptions<ClusterOptions>>().Value.ServiceId;

    public virtual async Task InitializeAsync()
    {
        this.EnsurePreconditionsMet();
        var builder = new TestClusterBuilder();
        TestDefaultConfiguration.ConfigureTestCluster(builder);
        this.ConfigureTestCluster(builder);

        var testCluster = builder.Build();
        if (testCluster.Primary == null)
        {
            await testCluster.DeployAsync().ConfigureAwait(false);
        }

        this.HostedCluster = testCluster;
        this.Logger = this.Client.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Application");
    }

    public virtual async Task DisposeAsync()
    {
        var cluster = this.HostedCluster;
        if (cluster is null) return;

        try
        {
            await cluster.StopAllSilosAsync().ConfigureAwait(false);
        }
        finally
        {
            await cluster.DisposeAsync().ConfigureAwait(false);
        }
    }
}

public class TestDefaultConfiguration
{
    private static readonly object LockObject = new object();
    private static IConfiguration defaultConfiguration;

    static TestDefaultConfiguration()
    {
        InitializeDefaults();
    }

    public static void InitializeDefaults()
    {
        lock (LockObject)
        {
            defaultConfiguration = BuildDefaultConfiguration();
        }
    }

    public static bool GetValue(string key, out string value)
    {
        value = defaultConfiguration.GetValue(key, default(string));

        return value != null;
    }

    private static IConfiguration BuildDefaultConfiguration()
    {
        var builder = new ConfigurationBuilder();
        ConfigureHostConfiguration(builder);

        var config = builder.Build();
        return config;
    }

    public static void ConfigureHostConfiguration(IConfigurationBuilder builder)
    {
    }

    public static void ConfigureTestCluster(TestClusterBuilder builder)
    {
        builder.ConfigureHostConfiguration(ConfigureHostConfiguration);
    }
}

public interface IGrainStorageTestGrain : IGrainWithGuidKey
{
    Task<int> GetValue();
    Task DoWrite(int val);
    Task<int> DoRead();
    Task DoDelete();
}