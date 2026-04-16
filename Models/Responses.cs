using System.Text.Json.Serialization;

namespace FiskalyMock.Models;

public class ReceiptResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("transactionId")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("documentNumber")]
    public string? DocumentNumber { get; set; }

    [JsonPropertyName("intentionRecordId")]
    public string? IntentionRecordId { get; set; }

    [JsonPropertyName("receiptRecordId")]
    public string? ReceiptRecordId { get; set; }

    [JsonPropertyName("adeProgressiveNumber")]
    public string? AdeProgressiveNumber { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("errorDetails")]
    public string? ErrorDetails { get; set; }
}

public class CancelResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("receiptRecordId")]
    public string? ReceiptRecordId { get; set; }

    [JsonPropertyName("originalReceiptRecordId")]
    public string? OriginalReceiptRecordId { get; set; }

    [JsonPropertyName("adeProgressiveNumber")]
    public string? AdeProgressiveNumber { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("errorDetails")]
    public string? ErrorDetails { get; set; }
}

public class StatusResponse
{
    [JsonPropertyName("configured")]
    public bool Configured { get; set; }

    [JsonPropertyName("environment")]
    public string Environment { get; set; } = "test";

    [JsonPropertyName("entityId")]
    public string? EntityId { get; set; }

    [JsonPropertyName("systemId")]
    public string? SystemId { get; set; }

    [JsonPropertyName("systemState")]
    public string? SystemState { get; set; }

    [JsonPropertyName("lastTransactionAt")]
    public long? LastTransactionAt { get; set; }

    [JsonPropertyName("fisconlineCredentialsUpdatedAt")]
    public long? FisconlineCredentialsUpdatedAt { get; set; }

    [JsonPropertyName("fisconlineDaysRemaining")]
    public int? FisconlineDaysRemaining { get; set; }

    [JsonPropertyName("fisconlineExpired")]
    public bool FisconlineExpired { get; set; }

    [JsonPropertyName("fisconlineWarning")]
    public bool FisconlineWarning { get; set; }

    [JsonPropertyName("testSetupCompleted")]
    public bool TestSetupCompleted { get; set; }

    [JsonPropertyName("liveSetupCompleted")]
    public bool LiveSetupCompleted { get; set; }
}

public class HealthResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "ok";

    [JsonPropertyName("service")]
    public string Service { get; set; } = "fiskaly-mock";

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}

public class TransactionSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("documentNumber")]
    public string DocumentNumber { get; set; } = "";

    [JsonPropertyName("totalAmount")]
    public double TotalAmount { get; set; }

    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("adeProgressiveNumber")]
    public string? AdeProgressiveNumber { get; set; }
}

public class TransactionDetails : TransactionSummary
{
    [JsonPropertyName("operatorId")]
    public string? OperatorId { get; set; }

    [JsonPropertyName("transactionRecordId")]
    public string? TransactionRecordId { get; set; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("completedAt")]
    public long? CompletedAt { get; set; }
}

public class TransactionListResponse
{
    [JsonPropertyName("transactions")]
    public List<TransactionSummary> Transactions { get; set; } = new();

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}
