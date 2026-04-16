using System.Collections.Concurrent;

namespace FiskalyMock.Models;

/// <summary>
/// In-memory transaction store.
/// I contatori documentNumber e adeNumber vengono persistiti su file
/// cosi restano univoci anche dopo un riavvio del mock.
/// </summary>
public class TransactionStore
{
    private const string CounterFilePath = "fiskaly_mock_counter.txt";

    private int _counter;
    private readonly ConcurrentDictionary<string, StoredTransaction> _byDocumentNumber = new();
    private readonly ConcurrentDictionary<string, StoredTransaction> _byReceiptRecordId = new();

    public TransactionStore()
    {
        LoadCounter();
    }

    public string NextNumber()
    {
        var n = Interlocked.Increment(ref _counter);
        SaveCounter();
        return n.ToString();
    }

    public int PeekNextDocumentNumber() => _counter + 1;

    public string NextAdeProgressiveNumber()
    {
        // Usa lo stesso contatore, formattato come ADE
        return $"DCM{_counter:D4}/{_counter:D4}-{_counter:D4}";
    }

    public void Save(StoredTransaction tx)
    {
        _byDocumentNumber[tx.DocumentNumber] = tx;
        if (tx.ReceiptRecordId != null)
            _byReceiptRecordId[tx.ReceiptRecordId] = tx;
    }

    public StoredTransaction? FindByDocumentNumber(string docNumber)
    {
        _byDocumentNumber.TryGetValue(docNumber, out var tx);
        return tx;
    }

    public StoredTransaction? FindByReceiptRecordId(string receiptRecordId)
    {
        _byReceiptRecordId.TryGetValue(receiptRecordId, out var tx);
        return tx;
    }

    public List<StoredTransaction> GetAll() => _byDocumentNumber.Values
        .OrderByDescending(t => t.CreatedAt)
        .ToList();

    public int Count => _byDocumentNumber.Count;

    private void LoadCounter()
    {
        var path = GetCounterFilePath();
        if (!File.Exists(path))
        {
            _counter = 0;
            return;
        }

        try
        {
            var text = File.ReadAllText(path).Trim();
            _counter = int.TryParse(text, out var val) ? val : 0;
        }
        catch
        {
            _counter = 0;
        }
    }

    private void SaveCounter()
    {
        try
        {
            File.WriteAllText(GetCounterFilePath(), _counter.ToString());
        }
        catch
        {
        }
    }

    private static string GetCounterFilePath()
    {
        // Salva accanto all'exe
        var dir = AppContext.BaseDirectory;
        return Path.Combine(dir, CounterFilePath);
    }
}

public class StoredTransaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = "RECEIPT"; // RECEIPT, CANCELLATION, CORRECTION
    public string Status { get; set; } = "REGISTERED";
    public string DocumentNumber { get; set; } = "";
    public string? ReceiptRecordId { get; set; }
    public string? IntentionRecordId { get; set; }
    public string? AdeProgressiveNumber { get; set; }
    public string? OperatorId { get; set; }
    public double TotalAmount { get; set; }
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long? CompletedAt { get; set; }
    public string? OriginalReceiptRecordId { get; set; }

    public bool IsTraining { get; set; }
}
