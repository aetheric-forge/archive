using Archive.Web.Plugin;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;
using AethericForge.Runtime.Models.Archive.Primitives;
using AethericForge.Runtime.Models.Archive.Serialization;
using AethericForge.Runtime.Models.Authorities;
using AethericForge.Runtime.Providers.Archive.InMemory;
using AethericForge.Runtime.Services.Archive;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

public sealed class ArchiveBoundaryTests
{
    [Fact]
    public async Task Existing_archive_keys_are_read_and_updated_through_the_extracted_module()
    {
        var storage = new InMemoryArchiveProvider("MinIO");
        var legacy = new Archivist(new ArchiveService([storage]), new Team<IArchiveClerk>([]), [new JsonArchiveSerializer()]);
        await legacy.PutAsync("MinIO", "existing/catalog-v1", "Before extraction");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Archive:S3:ServiceUrl"]="https://unused.example", ["Archive:S3:BucketName"]="existing-bucket",
            ["Archive:S3:AccessKey"]="archive", ["Archive:S3:SecretKey"]="test-only"
        }).Build();
        var services = new ServiceCollection();
        new ArchiveWebFactory().ConfigureWebServices(services, configuration, _ => null);
        services.RemoveAllKeyed<IArchiveProvider>("Archive");
        services.AddKeyedSingleton<IArchiveProvider>("Archive", storage);
        using var provider = services.BuildServiceProvider();
        var module = provider.GetRequiredKeyedService<IArchivist>("Archive");
        var reference = new ArchiveReference("MinIO", "existing/catalog-v1");
        Assert.Equal("Before extraction", await module.GetAsync<string>(reference));
        await module.PutAsync("MinIO", reference.Key, "After extraction");
        Assert.Equal("After extraction", await legacy.GetAsync<string>(reference));
    }

    [Fact]
    public void Archive_never_inherits_platform_credentials()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["S3:AccessKey"]="platform", ["S3:SecretKey"]="platform" }).Build();
        Assert.Throws<InvalidOperationException>(() => new ArchiveWebFactory().ConfigureWebServices(new ServiceCollection(), configuration, _ => null));
    }
}
