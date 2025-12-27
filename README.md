# Event-Driven Image Processing Pipeline

This project demonstrates an Azure event-driven architecture:
Blob upload triggers Event Grid, which invokes an Azure Function that processes data
and writes metadata to Cosmos DB using Managed Identity (RBAC).

## Flow
1. Upload a file to the source blob container
2. Event Grid emits a BlobCreated event
3. Azure Function is triggered
4. Function reads blob, writes to another container
5. Metadata is upserted into Cosmos DB

## Azure Services Used
- Azure Blob Storage
- Event Grid
- Azure Functions (.NET isolated)
- Azure Cosmos DB (NoSQL)
- Managed Identity + RBAC

## Security
- No connection strings or keys
- Access via DefaultAzureCredential
- Data-plane RBAC for Cosmos DB

## Purpose
This repository is created as a hands-on Azure learning project
with patterns commonly tested in AZ-204.
