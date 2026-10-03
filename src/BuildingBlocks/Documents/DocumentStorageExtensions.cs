using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Atlas.Documents;

public static class DocumentStorageExtensions
{
    /// <summary>The connection string name the AppHost provides for document storage.</summary>
    private const string ConnectionName = "documents";

    public static IHostApplicationBuilder AddDocumentStorage(this IHostApplicationBuilder builder)
    {
        builder.AddAzureBlobServiceClient(ConnectionName);
        builder.Services.AddSingleton<DocumentStorage>();

        return builder;
    }
    
    public static Task CreateDocumentContainerAsync(this IHost host, CancellationToken cancellationToken = default) =>
        host.Services.GetRequiredService<DocumentStorage>().CreateContainerAsync(cancellationToken);
}
