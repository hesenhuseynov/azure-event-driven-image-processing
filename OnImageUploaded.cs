// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}

using System;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Security.Policy;
using System.Text.Json;
using Azure.Identity;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Azure.Cosmos;
namespace Company.Function;

public class OnImageUploaded
{
    private readonly ILogger<OnImageUploaded> _logger;

    public OnImageUploaded(ILogger<OnImageUploaded> logger)
    {
        _logger = logger;
    }

    [Function(nameof(OnImageUploaded))]
    public async Task Run([EventGridTrigger] EventGridEvent ev)
    {
        var subject = ev.Subject ?? "";

         

        if (!subject.Contains("/containers/images/blobs/", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Skip non-images container. Subject={subject}", subject);
            return;
        }

        using var doc = JsonDocument.Parse(ev.Data.ToString());

        if (!doc.RootElement.TryGetProperty("url", out var urlElement))
        {
            _logger.LogError("Missing 'url' in EventGrid data. EventType: {eventType}", ev.EventType);
            return;
        }

        // var url = doc.RootElement.GetProperty("url").GetString();

        var url = urlElement.GetString();
 
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogError("URL not found in event data.");
            return;
        }

        var credential = new DefaultAzureCredential(); 

        _logger.LogInformation("Source Blob URL: {url}", url);

        var sourceBlob = new BlobClient(new Uri(url), credential);

        var props = await sourceBlob.GetPropertiesAsync();

        _logger.LogInformation("Source blob Size:{size} bytes, ContentType:{contentType}",
           props.Value.ContentLength, props.Value.ContentType);

        var accountBaseUri = new Uri($"{sourceBlob.Uri.Scheme}://{sourceBlob.Uri.Host}");

        var serviceClient = new BlobServiceClient(accountBaseUri, credential);
        var destContainer = serviceClient.GetBlobContainerClient("processed-images");

        await destContainer.CreateIfNotExistsAsync();

        var fileName = sourceBlob.Name;

        var destBlob = destContainer.GetBlobClient(fileName); 

        using var sourceStream = await sourceBlob.OpenReadAsync();

        await destBlob.UploadAsync(sourceStream, overwrite: true);

        _logger.LogInformation(" Uploaded processed-images: {destUrl}", destBlob.Uri);

        var cosmosEndpoint = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT");
        var dbName = Environment.GetEnvironmentVariable("COSMOS_DB");
        var containerName = Environment.GetEnvironmentVariable("COSMOS_CONTAINER");

        var cosmosClient = new CosmosClient(cosmosEndpoint, new DefaultAzureCredential());

        var container = cosmosClient.GetContainer(dbName, containerName);

        var imageId = fileName;


        var docc = new
        {
            id = imageId,
            imageId = imageId,
            sourceUrl = sourceBlob.Uri.ToString(),
            processedUrl = destBlob.Uri.ToString(),
            contentType = props.Value.ContentType,
            size = props.Value.ContentLength,
            status = "processed",
            processedAtUtc = DateTime.UtcNow
        };



        await container.UpsertItemAsync(docc, new PartitionKey(imageId));
        _logger.LogInformation(" Cosmos Upsert ok. id={id}", imageId);


    }
}