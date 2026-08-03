using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using FiskalyMock.Models;

namespace FiskalyMock.Endpoints;

/// <summary>
/// Endpoint Efsta/EFR (Germania) - protocollo XML.
/// </summary>
public static class GermanEndpoints
{
    private const string EfrContentType = "application/vnd.efsta.efr.v1+xml";

    public static void Register(WebApplication app, TransactionStore store, IMockCallbacks cb)
    {
        // POST /register - handles both TransactionStart (<TraS>) and full Transaction (<Tra>)
        app.MapPost("/register", async (HttpContext ctx) =>
        {
            var body = await new StreamReader(ctx.Request.Body, Encoding.UTF8).ReadToEndAsync();
            cb.Log($"  EFR /register body: {(body.Length > 200 ? body[..200] + "..." : body)}", Color.FromArgb(180, 180, 220));

            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(body);
                var root = doc.DocumentElement!;

                if (root.Name == "TraS")
                    return HandleTransactionStart(root, cb);
                else if (root.Name == "Tra")
                    return HandleTransaction(root, store, cb);
                else
                {
                    cb.Log($"  EFR: Unknown root element: {root.Name}", Color.FromArgb(255, 165, 0));
                    return Results.BadRequest("Unknown XML root element");
                }
            }
            catch (Exception ex)
            {
                cb.Log($"  EFR ERROR: {ex.Message}", Color.Red);
                var response = EfrTransactionResponse.Error(cb.NextSequenceNumber(), ex.Message);
                return Results.Content(response.ToXml(), EfrContentType);
            }
        });

        // POST /register/void - Void/cancel transaction
        app.MapPost("/register/void", async (HttpContext ctx) =>
        {
            var body = await new StreamReader(ctx.Request.Body, Encoding.UTF8).ReadToEndAsync();
            cb.Log($"  EFR /register/void body: {(body.Length > 200 ? body[..200] + "..." : body)}", Color.FromArgb(220, 150, 220));

            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(body);
                var root = doc.DocumentElement!;
                var request = EfrTransactionRequest.Parse(root);

                cb.Log($"  VOID request: RFN={request.ReferenceNumber ?? "null"}, TID={request.TransactionId?.ToString() ?? "null"}", Color.FromArgb(220, 120, 220));

                // Find original transaction
                StoredTransaction? original = null;
                if (!string.IsNullOrEmpty(request.ReferenceNumber))
                {
                    original = store.FindByReceiptRecordId(request.ReferenceNumber);
                    if (original == null)
                        original = store.FindByDocumentNumber(request.ReferenceNumber);
                }

                if (original != null)
                    cb.Log($"  Originale TROVATO: docNum={original.DocumentNumber}", Color.FromArgb(80, 200, 120));
                else
                    cb.Log($"  Originale NON trovato (RFN={request.ReferenceNumber}), procedo comunque", Color.FromArgb(255, 165, 0));

                var sq = cb.NextSequenceNumber();
                var docNumber = store.NextNumber();
                var now = DateTime.Now;

                var tx = new StoredTransaction
                {
                    Type = "VOID",
                    Status = "REGISTERED",
                    DocumentNumber = docNumber,
                    ReceiptRecordId = sq.ToString(),
                    AdeProgressiveNumber = $"SQ={sq}",
                    OperatorId = request.OperatorId,
                    TotalAmount = original?.TotalAmount ?? request.Total,
                    CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    OriginalReceiptRecordId = request.ReferenceNumber
                };
                store.Save(tx);
                cb.AddTransactionToList(tx);

                cb.Log($"  VOID OK -> SQ={sq}, docNum={docNumber}", Color.FromArgb(220, 120, 220));

                var response = EfrTransactionResponse.Ok(sq, request.Total, now, int.Parse(docNumber), request.TransactionId ?? sq);
                return Results.Content(response.ToXml(), EfrContentType);
            }
            catch (Exception ex)
            {
                cb.Log($"  EFR VOID ERROR: {ex.Message}", Color.Red);
                var response = EfrTransactionResponse.Error(cb.NextSequenceNumber(), ex.Message);
                return Results.Content(response.ToXml(), EfrContentType);
            }
        });

        // GET /retrieve - Journal
        app.MapGet("/retrieve", (int? last, int? endSQ) =>
        {
            cb.Log($"  EFR /retrieve last={last}, endSQ={endSQ}", Color.FromArgb(150, 180, 220));
            return Results.Content("""<?xml version="1.0" encoding="UTF-8"?><Journal></Journal>""", "application/xml");
        });

        // GET /last - Last journal entry
        app.MapGet("/last", () =>
        {
            cb.Log($"  EFR /last", Color.FromArgb(150, 180, 220));
            return Results.Content("""<?xml version="1.0" encoding="UTF-8"?><Journal></Journal>""", "application/xml");
        });

        // GET /control/tse - TSE status
        app.MapGet("/control/tse", () =>
        {
            cb.Log($"  EFR /control/tse", Color.FromArgb(150, 180, 220));
            return Results.Content("""<?xml version="1.0" encoding="UTF-8"?><TSE><Status>OK</Status><Initialized>true</Initialized></TSE>""", "application/xml");
        });

        // GET /control/export* - empty stubs
        app.MapGet("/control/export", () => Results.Content("", "application/octet-stream"));
        app.MapGet("/control/exportGoBD", () => Results.Content("", "application/octet-stream"));
        app.MapGet("/control/exportDSFinVK", () => Results.Content("", "application/octet-stream"));
        app.MapGet("/control/exportFull", () => Results.Content("", "application/octet-stream"));
        app.MapGet("/control/exportTse", () => Results.Content("", "application/octet-stream"));
        app.MapGet("/control/exportBackup", () => Results.Content("", "application/octet-stream"));
    }

    private static IResult HandleTransactionStart(XmlElement root, IMockCallbacks cb)
    {
        var request = EfrTransactionStartRequest.Parse(root);
        var sq = cb.NextSequenceNumber();
        var tid = sq;

        cb.Log($"  TraS (start) -> SQ={sq}, TID={tid}, TL={request.LocationId}, TT={request.TerminalId}", Color.FromArgb(180, 220, 180));

        var response = new EfrTransactionStartResponse
        {
            SequenceNumber = sq,
            ResultCode = "OK",
            TransactionId = tid,
            StartTime = DateTime.Now
        };

        return Results.Content(response.ToXml(), EfrContentType);
    }

    private static IResult HandleTransaction(XmlElement root, TransactionStore store, IMockCallbacks cb)
    {
        var request = EfrTransactionRequest.Parse(root);

        var sq = cb.NextSequenceNumber();
        var docNumber = request.IsTraining ? "0" : store.NextNumber();
        var now = DateTime.Now;

        var tx = new StoredTransaction
        {
            // Austria: lo storno arriva come Tra con AT_Storno="1" (niente TraS, niente TID)
            Type = request.IsAustrianCancellation ? "VOID" : "RECEIPT",
            Status = "REGISTERED",
            DocumentNumber = docNumber,
            ReceiptRecordId = sq.ToString(),
            AdeProgressiveNumber = $"SQ={sq}",
            OperatorId = request.OperatorId,
            TotalAmount = request.Total,
            IsTraining = request.IsTraining,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        store.Save(tx);
        cb.AddTransactionToList(tx);

        var env = request.IsTraining ? "TRAINING" : "LIVE";
        var kind = request.IsAustrianCancellation ? "AT_STORNO" : "RECEIPT";
        var idInfo = request.TransactionId != null ? $"TID={request.TransactionId}" : $"TN={request.TransactionNumber?.ToString() ?? "null"}";
        cb.Log($"  {kind} [{env}] SQ={sq}, docNum={docNumber}, total={request.Total:F2}, {idInfo}",
            request.IsAustrianCancellation ? Color.FromArgb(220, 120, 220) : Color.FromArgb(80, 200, 120));

        if (request.Positions.Count > 0)
            cb.Log($"         {request.Positions.Count} posizioni, {request.Payments.Count} pagamenti, {request.Taxes.Count} tasse", Color.FromArgb(120, 170, 120));

        var response = EfrTransactionResponse.Ok(sq, request.Total, now, int.Parse(docNumber), request.TransactionId);
        return Results.Content(response.ToXml(), EfrContentType);
    }
}
