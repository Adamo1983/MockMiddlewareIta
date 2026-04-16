using System.Globalization;
using System.Text;

namespace FiskalyMock.Models;

/// <summary>
/// Risposta al Transaction Start (Germany step 1).
/// XML generato: <TraSCompleted SQ="1"><Result RC="OK"/><Fis TID="1" StartD="..."/></TraSCompleted>
/// Parsato da: EfrTransactionStartCompletionData.Parse() in Giano
///
/// Campi obbligatori per Giano (crash se mancanti):
///   - SQ (int.Parse)
///   - Fis/@TID (int.Parse)
///   - Fis/@StartD (DateTime, 19 char format yyyy-MM-ddTHH:mm:ss)
/// </summary>
public class EfrTransactionStartResponse
{
    public int SequenceNumber { get; set; }
    public string ResultCode { get; set; } = "OK"; // "OK", "NO", "BAD"
    public int TransactionId { get; set; }
    public DateTime StartTime { get; set; } = DateTime.Now;
    public string? ErrorCode { get; set; }
    public string? UserMessage { get; set; }

    public string ToXml()
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.Append($"<TraSCompleted SQ=\"{SequenceNumber}\">");

        // Result
        sb.Append($"<Result RC=\"{ResultCode}\">");
        if (!string.IsNullOrEmpty(ErrorCode))
            sb.Append($"<ErrorCode>{Escape(ErrorCode)}</ErrorCode>");
        if (!string.IsNullOrEmpty(UserMessage))
            sb.Append($"<UserMessage>{Escape(UserMessage)}</UserMessage>");
        sb.Append("</Result>");

        // Fis - TID e StartD obbligatori (Giano fa int.Parse su TID)
        sb.Append($"<Fis TID=\"{TransactionId}\" StartD=\"{FormatDateTime(StartTime)}\"/>");

        sb.Append("</TraSCompleted>");
        return sb.ToString();
    }

    private static string FormatDateTime(DateTime dt) => $"{dt:yyyy-MM-dd'T'HH:mm:ss}";
    private static string Escape(string s) => System.Security.SecurityElement.Escape(s) ?? s;
}

/// <summary>
/// Risposta a una transazione completa o void (Germany step 2 / void).
/// XML generato: <TraCompleted SQ="1"><Result RC="OK"/><ESR D="..." T="..." TN="..."/><Fis TID="..."><Code>...</Code><Tag .../></Fis></TraCompleted>
/// Parsato da: EfrTransactionCompletionData.Parse() in Giano
///
/// Campi obbligatori per Giano (crash se mancanti):
///   - SQ (int.Parse)
///   - Result/@RC ("OK"/"NO"/"BAD")
///   - Fis (nodo deve esistere - Giano itera ChildNodes senza null-check)
///   - Fis/Tag/@Value (Giano fa .Replace() senza null-check)
///
/// Campi da NON includere:
///   - Fis/Link: se vuoto o < 10 char, Giano crasha su FiscalLink?.Substring(10, Length-10)
/// </summary>
public class EfrTransactionResponse
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public int SequenceNumber { get; set; }
    public string ResultCode { get; set; } = "OK"; // "OK", "NO", "BAD"
    public string? ErrorCode { get; set; }
    public string? UserMessage { get; set; }
    public double? Total { get; set; }
    public DateTime? Time { get; set; }
    public int? TransactionNumber { get; set; }
    public int? TransactionId { get; set; }
    public string FiscalCode { get; set; } = "MOCK-FISCAL-CODE";
    public List<EfrFiscalTag> FiscalTags { get; set; } = new();

    public string ToXml()
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.Append($"<TraCompleted SQ=\"{SequenceNumber}\">");

        // Result
        sb.Append($"<Result RC=\"{ResultCode}\">");
        if (!string.IsNullOrEmpty(ErrorCode))
            sb.Append($"<ErrorCode>{Escape(ErrorCode)}</ErrorCode>");
        if (!string.IsNullOrEmpty(UserMessage))
            sb.Append($"<UserMessage>{Escape(UserMessage)}</UserMessage>");
        sb.Append("</Result>");

        // ESR - attributi condizionali
        sb.Append("<ESR");
        if (Time != null)
            sb.Append($" D=\"{FormatDateTime(Time.Value)}\"");
        if (Total != null)
            sb.Append($" T=\"{Total.Value.ToString("F2", Inv)}\"");
        if (TransactionNumber != null)
            sb.Append($" TN=\"{TransactionNumber}\"");
        sb.Append("/>");

        // Fis - DEVE esistere (Giano itera ChildNodes senza null-check)
        sb.Append("<Fis");
        if (TransactionId != null)
            sb.Append($" TID=\"{TransactionId}\"");
        sb.Append(">");

        // Code
        sb.Append($"<Code>{Escape(FiscalCode)}</Code>");

        // NON includere <Link> - se vuoto/corto Giano crasha su Substring(10, ...)

        // Tag - Value DEVE essere non-null (Giano fa .Replace() senza null-check)
        foreach (var tag in FiscalTags)
        {
            sb.Append($"<Tag Name=\"{Escape(tag.Name)}\" Value=\"{Escape(tag.Value)}\" Label=\"{Escape(tag.Label)}\"/>");
        }

        sb.Append("</Fis>");
        sb.Append("</TraCompleted>");
        return sb.ToString();
    }

    /// <summary>
    /// Crea una risposta di successo con i valori tipici per il mock.
    /// </summary>
    public static EfrTransactionResponse Ok(int sq, double? total, DateTime? time, int? transactionNumber, int? transactionId)
    {
        return new EfrTransactionResponse
        {
            SequenceNumber = sq,
            ResultCode = "OK",
            Total = total,
            Time = time,
            TransactionNumber = transactionNumber,
            TransactionId = transactionId,
            FiscalTags = new List<EfrFiscalTag>
            {
                new() { Name = "MockSequence", Value = sq.ToString(), Label = "Sequence" }
            }
        };
    }

    /// <summary>
    /// Crea una risposta di errore.
    /// </summary>
    public static EfrTransactionResponse Error(int sq, string errorMessage)
    {
        return new EfrTransactionResponse
        {
            SequenceNumber = sq,
            ResultCode = "BAD",
            ErrorCode = "MOCK_ERROR",
            UserMessage = errorMessage,
            FiscalTags = new List<EfrFiscalTag>
            {
                new() { Name = "MockSequence", Value = sq.ToString(), Label = "Sequence" }
            }
        };
    }

    private static string FormatDateTime(DateTime dt) => $"{dt:yyyy-MM-dd'T'HH:mm:ss}";
    private static string Escape(string s) => System.Security.SecurityElement.Escape(s) ?? s;
}

/// <summary>
/// Singolo tag fiscale nella risposta EFR.
/// ATTENZIONE: Value DEVE essere non-null - Giano fa fiscalTag.Value.Replace() senza null-check.
/// </summary>
public class EfrFiscalTag
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = ""; // MAI null - Giano crasha
    public string Label { get; set; } = "";
}
