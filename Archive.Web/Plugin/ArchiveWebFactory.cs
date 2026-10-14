using AethericForge.Runtime.Institutions.Abstractions.Models;
using System.Runtime.Loader;
using Amazon.S3;
using Amazon.Runtime;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions.Plugins;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Serialization;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;
using AethericForge.Runtime.Institutions.Abstractions.Builders;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;
using AethericForge.Runtime.Institutions.Archive;
using AethericForge.Runtime.Models.Archive.Serialization;
using AethericForge.Runtime.Models.Authorities;
using AethericForge.Runtime.Providers.Archive.S3;
using AethericForge.Runtime.Services.Archive;
using Archive.Web.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: InstitutionPluginPackage(typeof(Archive.Web.Plugin.ArchiveWebPackage))]
namespace Archive.Web.Plugin;

public sealed class ArchiveWebPackage : IInstitutionPluginPackage
{
    public IReadOnlyCollection<IInstitutionFactory> GetFactories() => [new ArchiveWebFactory()];
}

public sealed class ArchiveWebFactory : IInstitutionFactory
{
    private readonly AssemblyLoadContext _context = AssemblyLoadContext.GetLoadContext(typeof(ArchiveWebFactory).Assembly)!;
    public Type ContractType => typeof(IArchive);
    public IInstitutionTemplate Template { get; } = InstitutionTemplateBuilder.Create()
        .WithDescriptor("Archive", new Version(1, 0, 0), "Durable archival storage and typed retrieval.")
        .AddResource(new ResourceDefinition("Archive storage", "archive", "Durable archival storage and typed retrieval.")).Build();
    public IInstitutionManifest Manifest => Template.Descriptor;

    public void ConfigureWebServices(IServiceCollection services, IConfiguration configuration,
        Func<IServiceProvider, IInstitution?> parent)
    {
        var settings = InstitutionServiceConfiguration.Resolve(configuration, "Archive", "S3");
        if (string.IsNullOrWhiteSpace(settings["S3:ServiceUrl"]))
            throw new InvalidOperationException("Archive S3 ServiceUrl is required.");
        services.AddKeyedSingleton<IAmazonS3>("Archive", (_, _) => CreateClient(settings));
        services.AddKeyedSingleton<IArchiveProvider>("Archive", (sp, _) => new S3ArchiveProvider(sp.GetRequiredKeyedService<IAmazonS3>("Archive"),
            "MinIO", settings.GetValue("S3:BucketName", "forge-campus-archive")!));
        services.AddKeyedSingleton<IArchiveService>("Archive", (sp, _) => new ArchiveService(sp.GetKeyedServices<IArchiveProvider>("Archive")));
        services.AddKeyedSingleton<IArchiveVault>("Archive", (sp, _) => new ArchiveVault(sp.GetKeyedServices<IArchiveProvider>("Archive").First()));
        services.AddKeyedSingleton<IArchivist>("Archive", (sp, _) => new Archivist(sp.GetRequiredKeyedService<IArchiveService>("Archive"),
            new Team<IArchiveClerk>([]), [new JsonArchiveSerializer()]));
        services.AddKeyedSingleton<IArchive>("Archive", (sp, _) => parent(sp) is { } owner
            ? owner.Resolve<IArchive>() : CreateArchive(sp, null));
    }

    public static IAmazonS3 CreateClient(IConfiguration settings) => new AmazonS3Client(
        new BasicAWSCredentials(settings["S3:AccessKey"], settings["S3:SecretKey"]), new AmazonS3Config
        {
            ServiceURL = settings["S3:ServiceUrl"] ?? throw new InvalidOperationException("S3:ServiceUrl is required."),
            ForcePathStyle = settings.GetValue("S3:ForcePathStyle", true),
            AuthenticationRegion = settings.GetValue("S3:AuthenticationRegion", "us-east-1")
        });
    public IInstitution Create(IInstitution parent, IServiceProvider services) => CreateArchive(services, parent);
    private IArchive CreateArchive(IServiceProvider services, IInstitution? parent) =>
        new AethericForge.Runtime.Institutions.Archive.Archive(new ArchiveContext(Template, services, parent),
            services.GetRequiredKeyedService<IArchiveVault>("Archive"), services.GetRequiredKeyedService<IArchivist>("Archive"));
}
