using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using FiskalyMock.Models;

namespace FiskalyMock.Endpoints;

/// <summary>
/// Endpoint Fiskaly (Italia) - protocollo JSON REST.
/// </summary>
public static class ItalianEndpoints
{
    public static void Register(WebApplication app, TransactionStore store, IMockCallbacks cb)
    {
        string GetMessage(string defaultMsg) => cb.SendMessageEnabled ? defaultMsg : "";

        // GET /api/status
        app.MapGet("/api/status", () =>
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return Results.Ok(new StatusResponse
            {
                Configured = true,
                Environment = "test",
                EntityId = "mock-entity-001",
                SystemId = "mock-system-001",
                SystemState = "ACTIVE",
                LastTransactionAt = store.Count > 0 ? now : null,
                FisconlineCredentialsUpdatedAt = now,
                FisconlineDaysRemaining = 55,
                FisconlineExpired = false,
                FisconlineWarning = false,
                TestSetupCompleted = true,
                LiveSetupCompleted = true
            });
        });

        // GET /api/health
        app.MapGet("/api/health", () => Results.Ok(new HealthResponse
        {
            Status = "ok",
            Service = "fiskaly-mock",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        }));

        // POST /api/receipt
        app.MapPost("/api/receipt", (SimpleReceiptRequest request) =>
        {
            var isTraining = request.Training;
            string docNumber;
            if (isTraining)
                docNumber = request.DocumentNumber ?? "0";
            else
                docNumber = request.DocumentNumber ?? store.NextNumber();

            var receiptRecordId = Guid.NewGuid().ToString();
            var intentionRecordId = Guid.NewGuid().ToString();
            var adeNumber = store.NextAdeProgressiveNumber();
            var totalAmount = request.Items.Sum(i => i.Quantity * i.UnitPrice);

            var tx = new StoredTransaction
            {
                Type = "RECEIPT",
                Status = "REGISTERED",
                DocumentNumber = docNumber,
                ReceiptRecordId = receiptRecordId,
                IntentionRecordId = intentionRecordId,
                AdeProgressiveNumber = adeNumber,
                OperatorId = request.OperatorId,
                TotalAmount = totalAmount,
                IsTraining = isTraining,
                CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            store.Save(tx);
            cb.AddTransactionToList(tx);

            var env = isTraining ? "DEMO" : "LIVE";
            var docSource = request.DocumentNumber != null ? "da Giano" : (isTraining ? "fisso 0" : "generato");
            cb.Log($"  RECEIPT [{env}] docNum={docNumber} ({docSource}) | {totalAmount:F2} | ADE: {adeNumber}", Color.FromArgb(80, 200, 120));
            cb.Log($"         receiptRecordId={receiptRecordId}", Color.FromArgb(120, 170, 120));

            return Results.Created($"/api/transactions/{docNumber}", new ReceiptResponse
            {
                Success = true,
                Message = GetMessage("Receipt registered successfully (MOCK)"),
                TransactionId = tx.Id,
                DocumentNumber = docNumber,
                IntentionRecordId = intentionRecordId,
                ReceiptRecordId = receiptRecordId,
                AdeProgressiveNumber = adeNumber,
                Status = "REGISTERED"
            });
        });

        // POST /api/receipt/cancel
        app.MapPost("/api/receipt/cancel", (CancellationApiRequest request) =>
        {
            cb.Log($"  Cancel request: docNum={request.DocumentNumber ?? "null"}, receiptId={request.OriginalReceiptRecordId ?? "null"}", Color.FromArgb(200, 180, 220));

            StoredTransaction? original = null;
            if (!string.IsNullOrEmpty(request.OriginalReceiptRecordId))
                original = store.FindByReceiptRecordId(request.OriginalReceiptRecordId);
            if (original == null && !string.IsNullOrEmpty(request.DocumentNumber))
                original = store.FindByDocumentNumber(request.DocumentNumber);

            if (original != null)
            {
                cb.Log($"  Originale TROVATO: docNum={original.DocumentNumber}, tipo={original.Type}, totale={original.TotalAmount:F2}", Color.FromArgb(80, 200, 120));
            }
            else
            {
                cb.Log($"  ATTENZIONE: originale NON trovato in memoria!", Color.FromArgb(255, 165, 0));
                cb.Log($"  Transazioni in memoria: {string.Join(", ", store.GetAll().Select(t => $"#{t.DocumentNumber}({t.Type})"))}", Color.FromArgb(255, 165, 0));
            }

            var cancelReceiptRecordId = Guid.NewGuid().ToString();
            var adeNumber = store.NextAdeProgressiveNumber();
            var cancelDocNumber = store.NextNumber();

            var tx = new StoredTransaction
            {
                Type = "CANCELLATION",
                Status = "REGISTERED",
                DocumentNumber = cancelDocNumber,
                ReceiptRecordId = cancelReceiptRecordId,
                AdeProgressiveNumber = adeNumber,
                OperatorId = request.OperatorId,
                TotalAmount = original?.TotalAmount ?? 0,
                CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                OriginalReceiptRecordId = request.OriginalReceiptRecordId ?? original?.ReceiptRecordId
            };
            store.Save(tx);
            cb.AddTransactionToList(tx);

            cb.Log($"  CANCEL OK -> nuovo docNum={cancelDocNumber} | ADE: {adeNumber}", Color.FromArgb(220, 120, 220));

            return Results.Ok(new CancelResponse
            {
                Success = true,
                Message = GetMessage("Receipt cancelled successfully (MOCK)"),
                ReceiptRecordId = cancelReceiptRecordId,
                OriginalReceiptRecordId = request.OriginalReceiptRecordId ?? original?.ReceiptRecordId,
                AdeProgressiveNumber = adeNumber,
                Status = "REGISTERED"
            });
        });

        // POST /api/receipt/refund
        app.MapPost("/api/receipt/refund", (CorrectionApiRequest request) =>
        {
            cb.Log($"  Refund request: docNum={request.DocumentNumber ?? "null"}, receiptId={request.OriginalReceiptRecordId ?? "null"}", Color.FromArgb(150, 180, 220));

            StoredTransaction? original = null;
            if (!string.IsNullOrEmpty(request.OriginalReceiptRecordId))
                original = store.FindByReceiptRecordId(request.OriginalReceiptRecordId);
            if (original == null && !string.IsNullOrEmpty(request.DocumentNumber))
                original = store.FindByDocumentNumber(request.DocumentNumber);

            if (original != null)
                cb.Log($"  Originale TROVATO: docNum={original.DocumentNumber}", Color.FromArgb(80, 200, 120));
            else
                cb.Log($"  ATTENZIONE: originale NON trovato in memoria!", Color.FromArgb(255, 165, 0));

            var refundReceiptRecordId = Guid.NewGuid().ToString();
            var adeNumber = store.NextAdeProgressiveNumber();
            var refundDocNumber = store.NextNumber();
            var totalAmount = request.Items.Sum(i => i.Quantity * i.UnitPrice);

            var tx = new StoredTransaction
            {
                Type = "CORRECTION",
                Status = "REGISTERED",
                DocumentNumber = refundDocNumber,
                ReceiptRecordId = refundReceiptRecordId,
                AdeProgressiveNumber = adeNumber,
                OperatorId = request.OperatorId,
                TotalAmount = totalAmount,
                CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                OriginalReceiptRecordId = request.OriginalReceiptRecordId ?? original?.ReceiptRecordId
            };
            store.Save(tx);
            cb.AddTransactionToList(tx);

            cb.Log($"  REFUND OK -> docNum={refundDocNumber} | {totalAmount:F2} | ADE: {adeNumber}", Color.FromArgb(100, 150, 255));

            return Results.Ok(new ReceiptResponse
            {
                Success = true,
                Message = GetMessage("Refund registered successfully (MOCK)"),
                TransactionId = tx.Id,
                DocumentNumber = refundDocNumber,
                ReceiptRecordId = refundReceiptRecordId,
                AdeProgressiveNumber = adeNumber,
                Status = "REGISTERED"
            });
        });
    }
}
