using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FiskalyMock.Endpoints;
using FiskalyMock.Models;

namespace FiskalyMock;

/// <summary>
/// Costruisce il WebApplication con endpoint condivisi e registra
/// gli endpoint specifici per lingua (IT o DE).
/// </summary>
public static class MockServerBuilder
{
    public static WebApplication Build(int port, MockLanguage language, TransactionStore store, IMockCallbacks cb)
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        builder.WebHost.SuppressStatusMessages(true);
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton(store);
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        var app = builder.Build();

        // Request logging middleware (esclude dashboard auto-refresh)
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? "";
            if (path != "/")
                cb.Log($"{context.Request.Method} {path}", Color.FromArgb(100, 180, 255));
            await next();
        });

        RegisterSharedEndpoints(app, port, language, store);

        // Austria usa lo stesso protocollo XML EFR della Germania (endpoint /register,
        // /register/void, /retrieve...): cambiano solo il payload che manda Giano e il
        // <Country> di /state, non gli endpoint.
        if (language == MockLanguage.Italian)
            ItalianEndpoints.Register(app, store, cb);
        else
            GermanEndpoints.Register(app, store, cb);

        return app;
    }

    private static void RegisterSharedEndpoints(WebApplication app, int port, MockLanguage language, TransactionStore store)
    {
        var langName = language switch
        {
            MockLanguage.Italian => "Fiskaly (IT)",
            MockLanguage.German => "Efsta/EFR (DE)",
            _ => "Efsta/EFR (AT)"
        };

        // GET / - Dashboard HTML
        app.MapGet("/", () =>
        {
            var txRows = string.Join("", store.GetAll().Select(t =>
                $"<tr><td>{t.DocumentNumber}</td><td><span class='{t.Type.ToLower()}'>{t.Type}</span></td>"
                + $"<td>{t.TotalAmount:F2}</td><td>{t.AdeProgressiveNumber}</td>"
                + $"<td class='small'>{t.ReceiptRecordId}</td></tr>"));

            var html = $$"""
                <!DOCTYPE html>
                <html><head>
                <meta charset="utf-8">
                <title>{{langName}} Mock</title>
                <meta http-equiv="refresh" content="5">
                <style>
                  * { margin:0; padding:0; box-sizing:border-box; }
                  body { font-family:'Segoe UI',sans-serif; background:#1a1a2e; color:#e0e0e0; padding:30px; }
                  h1 { color:#4ecca3; font-size:28px; margin-bottom:4px; }
                  .sub { color:#888; margin-bottom:24px; }
                  .status { display:inline-block; background:#4ecca3; color:#1a1a2e; padding:4px 14px;
                            border-radius:12px; font-weight:bold; font-size:13px; margin-bottom:20px; }
                  .info { background:#16213e; border-radius:8px; padding:16px 20px; margin-bottom:20px;
                          display:grid; grid-template-columns:repeat(3,1fr); gap:12px; }
                  .info div { text-align:center; }
                  .info .label { color:#888; font-size:12px; }
                  .info .value { color:#4ecca3; font-size:22px; font-weight:bold; }
                  table { width:100%; border-collapse:collapse; background:#16213e; border-radius:8px; overflow:hidden; }
                  th { background:#0f3460; padding:10px 12px; text-align:left; font-size:12px; color:#aaa; text-transform:uppercase; }
                  td { padding:8px 12px; border-top:1px solid #1a1a2e; font-size:13px; }
                  .small { font-size:11px; color:#888; max-width:250px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
                  .receipt { color:#4ecca3; } .cancellation { color:#e040a0; } .correction { color:#40a0e0; } .void { color:#e040a0; }
                  .empty { text-align:center; padding:30px; color:#555; }
                </style>
                </head><body>
                <h1>{{langName}} Mock Middleware</h1>
                <p class="sub">Simulatore per test GianoITA &mdash; modalita {{langName}}</p>
                <span class="status">ONLINE - porta {{port}}</span>
                <div class="info">
                  <div><div class="label">Transazioni</div><div class="value">{{store.Count}}</div></div>
                  <div><div class="label">Prossimo docNum</div><div class="value">{{store.PeekNextDocumentNumber()}}</div></div>
                  <div><div class="label">Modalita</div><div class="value">{{langName}}</div></div>
                </div>
                <table>
                  <tr><th>DocNum</th><th>Tipo</th><th>Totale</th><th>ADE/SQ</th><th>ReceiptRecordId</th></tr>
                  {{(txRows.Length > 0 ? txRows : "<tr><td colspan='5' class='empty'>Nessuna transazione</td></tr>")}}
                </table>
                <p style="margin-top:16px;color:#555;font-size:11px;">Auto-refresh ogni 5 secondi</p>
                </body></html>
                """;
            return Results.Content(html, "text/html");
        });

        // GET /state - EFR XML state (con Country corretto per la lingua)
        app.MapGet("/state", () =>
        {
            var now = DateTime.Now;
            var formattedDate = now.ToString("yyyy-MM-dd'T'HH:mm:ss");
            var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            var uptime = (long)(DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;
            var countryCode = language switch
            {
                MockLanguage.Italian => "IT",
                MockLanguage.German => "DE",
                _ => "AT"
            };

            var mockName = language == MockLanguage.Italian ? "Fiskaly Mock Middleware" : "Efsta EFR Mock";

            var manifestText = language switch
            {
                MockLanguage.Italian => "Fiskaly Mock for Italy",
                MockLanguage.German => "EFR Mock for Germany",
                _ => "EFR Mock for Austria"
            };

            // In Austria Giano pretende una smart card installata OPPURE la company di test
            // efsta ATU57780814 (EfrClient.TestCloudCompanyId), altrimenti il test EFR
            // all'avvio fallisce e la finalizzazione tenta un riavvio del servizio.
            // Il formato di ogni voce <SC> e' "TaxId:Identifier:Serial": esattamente 3 campi,
            // altrimenti Giano mette la lista a null e va in NullReference in finalizzazione.
            var company = language == MockLanguage.Austrian ? "ATU57780814" : "";
            var smartCards = language == MockLanguage.Austrian ? "ATU57780814:AT0:MOCK0000000001" : "";

            var xml = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <state>
                  <name>{mockName}</name>
                  <version>1.0.0</version>
                  <manifest>{manifestText}</manifest>
                  <Country>{countryCode}</Country>
                  <PID>{pid}</PID>
                  <uptime>{uptime}</uptime>
                  <Online>true</Online>
                  <Recorder>online</Recorder>
                  <Company>{company}</Company>
                  <EFR></EFR>
                  <RN>01</RN>
                  <RecSent>0</RecSent>
                  <RecQueued>0</RecQueued>
                  <RetryQueued>0</RetryQueued>
                  <TimeOffset>0</TimeOffset>
                  <D>{formattedDate}</D>
                  <SC>{smartCards}</SC>
                  <DiskUsage>0.0</DiskUsage>
                  <DiskQuota>1000000000</DiskQuota>
                </state>
                """;
            return Results.Content(xml, "application/xml");
        });

        // GET /api/transactions
        app.MapGet("/api/transactions", (int? page, int? pageSize) =>
        {
            var p = page ?? 1;
            var ps = pageSize ?? 50;
            var all = store.GetAll();
            var paged = all.Skip((p - 1) * ps).Take(ps).ToList();

            return Results.Ok(new TransactionListResponse
            {
                Transactions = paged.Select(t => new TransactionSummary
                {
                    Id = t.Id,
                    Type = t.Type,
                    Status = t.Status,
                    DocumentNumber = t.DocumentNumber,
                    TotalAmount = t.TotalAmount,
                    CreatedAt = t.CreatedAt,
                    AdeProgressiveNumber = t.AdeProgressiveNumber
                }).ToList(),
                Page = p,
                PageSize = ps,
                TotalCount = all.Count
            });
        });

        // GET /api/transactions/{documentNumber}
        app.MapGet("/api/transactions/{documentNumber}", (string documentNumber) =>
        {
            var tx = store.FindByDocumentNumber(documentNumber);
            if (tx == null)
                return Results.NotFound(new { message = $"Transaction {documentNumber} not found" });

            return Results.Ok(new TransactionDetails
            {
                Id = tx.Id,
                Type = tx.Type,
                Status = tx.Status,
                DocumentNumber = tx.DocumentNumber,
                TotalAmount = tx.TotalAmount,
                CreatedAt = tx.CreatedAt,
                AdeProgressiveNumber = tx.AdeProgressiveNumber,
                OperatorId = tx.OperatorId,
                TransactionRecordId = tx.ReceiptRecordId,
                CompletedAt = tx.CompletedAt
            });
        });
    }
}
