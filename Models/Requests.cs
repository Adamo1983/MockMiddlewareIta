using System.Text.Json.Serialization;

namespace FiskalyMock.Models;

public class SimpleReceiptRequest
{
    [JsonPropertyName("items")]
    public List<ReceiptItem> Items { get; set; } = new();

    [JsonPropertyName("payments")]
    public List<SimplePayment> Payments { get; set; } = new();

    [JsonPropertyName("operatorId")]
    public string? OperatorId { get; set; }

    [JsonPropertyName("operatorName")]
    public string? OperatorName { get; set; }

    [JsonPropertyName("training")]
    public bool Training { get; set; }

    [JsonPropertyName("environment")]
    public string? Environment { get; set; }

    [JsonPropertyName("documentNumber")]
    public string? DocumentNumber { get; set; }

    [JsonPropertyName("customerLotteryCode")]
    public string? CustomerLotteryCode { get; set; }
}

public class ReceiptItem
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("quantity")]
    public double Quantity { get; set; }

    [JsonPropertyName("unitPrice")]
    public double UnitPrice { get; set; }

    [JsonPropertyName("vatCode")]
    public string VatCode { get; set; } = "STANDARD";

    [JsonPropertyName("concept")]
    public string Concept { get; set; } = "GOOD";

    [JsonPropertyName("entryType")]
    public string EntryType { get; set; } = "SALE";

    [JsonPropertyName("discount")]
    public double? Discount { get; set; }
}

public class SimplePayment
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "CASH";

    [JsonPropertyName("amount")]
    public double? Amount { get; set; }

    [JsonPropertyName("cardLastDigits")]
    public string? CardLastDigits { get; set; }

    [JsonPropertyName("cardKind")]
    public string? CardKind { get; set; }
}

public class CancellationApiRequest
{
    [JsonPropertyName("originalReceiptRecordId")]
    public string? OriginalReceiptRecordId { get; set; }

    [JsonPropertyName("documentNumber")]
    public string? DocumentNumber { get; set; }

    [JsonPropertyName("operatorId")]
    public string? OperatorId { get; set; }

    [JsonPropertyName("training")]
    public bool Training { get; set; }
}

public class CorrectionApiRequest
{
    [JsonPropertyName("originalReceiptRecordId")]
    public string? OriginalReceiptRecordId { get; set; }

    [JsonPropertyName("documentNumber")]
    public string? DocumentNumber { get; set; }

    [JsonPropertyName("items")]
    public List<ReceiptItem> Items { get; set; } = new();

    [JsonPropertyName("payments")]
    public List<SimplePayment> Payments { get; set; } = new();

    [JsonPropertyName("operatorId")]
    public string? OperatorId { get; set; }

    [JsonPropertyName("training")]
    public bool Training { get; set; }
}
