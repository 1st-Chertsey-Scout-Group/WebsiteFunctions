using System;
using Azure;
using Azure.Data.Tables;

namespace ServerlessFunctions.Models.Entities;

public class RecipientEntity : ITableEntity
{
    internal string Topic
    {
        get => PartitionKey;
        set => PartitionKey = value;
    }

    internal string Emails
    {
        get => RowKey;
        set => RowKey = value;
    }

    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}