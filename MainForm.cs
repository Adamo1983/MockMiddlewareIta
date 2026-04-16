using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FiskalyMock.Models;

namespace FiskalyMock;

public class MainForm : Form
{
    private readonly RichTextBox _logBox;
    private readonly ListView _txListView;
    private readonly Button _startButton;
    private readonly Button _stopButton;
    private readonly Button _clearButton;
    private readonly Label _statusLabel;
    private readonly Label _counterLabel;
    private readonly CheckBox _sendMessageCheckBox;
    private readonly SplitContainer _splitContainer;

    private WebApplication? _webApp;
    private CancellationTokenSource? _cts;
    private TransactionStore _store = new();

    public MainForm()
    {
        Text = "Fiskaly Mock Middleware";
        Size = new Size(900, 600);
        MinimumSize = new Size(700, 450);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9);

        // Top panel with controls
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            Padding = new Padding(8, 8, 8, 4)
        };

        _startButton = new Button
        {
            Text = "Start",
            Size = new Size(80, 32),
            Location = new Point(8, 9),
            BackColor = Color.FromArgb(46, 139, 87),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _startButton.Click += OnStartClick;

        _stopButton = new Button
        {
            Text = "Stop",
            Size = new Size(80, 32),
            Location = new Point(96, 9),
            BackColor = Color.FromArgb(178, 34, 34),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Enabled = false,
            Cursor = Cursors.Hand
        };
        _stopButton.Click += OnStopClick;

        _clearButton = new Button
        {
            Text = "Clear Log",
            Size = new Size(80, 32),
            Location = new Point(184, 9),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _clearButton.Click += (_, _) => _logBox?.Clear();

        _statusLabel = new Label
        {
            Text = "  Stopped",
            ForeColor = Color.Gray,
            AutoSize = true,
            Location = new Point(280, 16),
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        _sendMessageCheckBox = new CheckBox
        {
            Text = "Invia messaggio nella risposta",
            Checked = true,
            AutoSize = true,
            Location = new Point(420, 14),
            ForeColor = Color.FromArgb(80, 80, 80),
            Font = new Font("Segoe UI", 8.5f)
        };

        _counterLabel = new Label
        {
            Text = "",
            ForeColor = Color.DimGray,
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(650, 16),
            Font = new Font("Segoe UI", 9)
        };

        topPanel.Controls.AddRange(new Control[] { _startButton, _stopButton, _clearButton, _statusLabel, _sendMessageCheckBox, _counterLabel });

        // Split container: top = transaction list, bottom = log
        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 180,
            Panel1MinSize = 80,
            Panel2MinSize = 150
        };

        // Transaction list (top panel)
        _txListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Segoe UI", 8.5f),
            BackColor = Color.FromArgb(245, 245, 250)
        };
        _txListView.Columns.Add("DocNum", 65, HorizontalAlignment.Center);
        _txListView.Columns.Add("Tipo", 100, HorizontalAlignment.Left);
        _txListView.Columns.Add("Totale", 80, HorizontalAlignment.Right);
        _txListView.Columns.Add("ADE", 160, HorizontalAlignment.Left);
        _txListView.Columns.Add("ReceiptRecordId", 280, HorizontalAlignment.Left);
        _txListView.Columns.Add("Ora", 70, HorizontalAlignment.Center);
        _txListView.Columns.Add("Ref. Originale", 80, HorizontalAlignment.Center);

        var txLabel = new Label
        {
            Text = " Transazioni in memoria:",
            Dock = DockStyle.Top,
            Height = 20,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.DimGray,
            BackColor = Color.FromArgb(235, 235, 240),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _splitContainer.Panel1.Controls.Add(_txListView);
        _splitContainer.Panel1.Controls.Add(txLabel);

        // Log area (bottom panel)
        _logBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = false, // false per permettere selezione e copia
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Cascadia Code", 9.5f, FontStyle.Regular, GraphicsUnit.Point, 0, false),
            BorderStyle = BorderStyle.None,
            WordWrap = false,
            ShortcutsEnabled = true // Ctrl+C funziona
        };
        if (!_logBox.Font.Name.Equals("Cascadia Code", StringComparison.OrdinalIgnoreCase))
            _logBox.Font = new Font("Consolas", 9.5f);
        // Blocca la digitazione ma permette selezione + copia
        _logBox.KeyPress += (_, e) => e.Handled = true;
        _logBox.KeyDown += (_, e) =>
        {
            // Permetti solo Ctrl+C e Ctrl+A
            if (e.Control && (e.KeyCode == Keys.C || e.KeyCode == Keys.A))
                return;
            e.SuppressKeyPress = true;
        };

        _splitContainer.Panel2.Controls.Add(_logBox);

        Controls.Add(_splitContainer);
        Controls.Add(topPanel);

        FormClosing += OnFormClosing;
        Load += (_, _) => OnStartClick(this, EventArgs.Empty);
    }

    private void OnStartClick(object? sender, EventArgs e)
    {
        _startButton.Enabled = false;
        _store = new TransactionStore();
        _txListView.Items.Clear();

        try
        {
            _cts = new CancellationTokenSource();
            _webApp = BuildWebApp();
            _ = _webApp.StartAsync(_cts.Token); // avvia il server in background senza bloccare

            _stopButton.Enabled = true;
            _statusLabel.Text = "  Listening on :8180";
            _statusLabel.ForeColor = Color.FromArgb(46, 139, 87);
            Log("SERVER AVVIATO sulla porta 8180", Color.FromArgb(80, 200, 120));
            Log($"Contatori caricati da file: prossimo docNum={_store.PeekNextDocumentNumber()}", Color.FromArgb(180, 180, 100));
            _counterLabel.Text = $"Transazioni: 0 | Prossimo docNum: {_store.PeekNextDocumentNumber()}";
            Log("In attesa di richieste da GianoITA...\n", Color.Gray);
        }
        catch (Exception ex)
        {
            _startButton.Enabled = true;
            Log($"ERRORE AVVIO: {ex.Message}", Color.Red);
        }
    }

    private async void OnStopClick(object? sender, EventArgs e)
    {
        _stopButton.Enabled = false;
        await StopServer();
        _startButton.Enabled = true;
        _statusLabel.Text = "  Stopped";
        _statusLabel.ForeColor = Color.Gray;
        _counterLabel.Text = "";
        Log("SERVER FERMATO\n", Color.FromArgb(200, 80, 80));
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        e.Cancel = true;
        await StopServer();
        Environment.Exit(0);
    }

    private async Task StopServer()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
            _cts = null;
        }
        if (_webApp != null)
        {
            try { await _webApp.StopAsync(); } catch { }
            try { await _webApp.DisposeAsync(); } catch { }
            _webApp = null;
        }
    }

    private void Log(string message, Color color)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => Log(message, color));
            return;
        }

        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionColor = Color.DimGray;
        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] ");
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionColor = color;
        _logBox.AppendText(message + "\n");
        _logBox.ScrollToCaret();
    }

    private void AddTransactionToList(StoredTransaction tx)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AddTransactionToList(tx));
            return;
        }

        var item = new ListViewItem(tx.DocumentNumber);

        var typeColor = tx.Type switch
        {
            "RECEIPT" => Color.DarkGreen,
            "CANCELLATION" => Color.DarkMagenta,
            "CORRECTION" => Color.DarkBlue,
            _ => Color.Black
        };
        item.ForeColor = typeColor;

        item.SubItems.Add(tx.Type);
        item.SubItems.Add($"{tx.TotalAmount:F2}");
        item.SubItems.Add(tx.AdeProgressiveNumber ?? "");
        item.SubItems.Add(tx.ReceiptRecordId ?? "");
        item.SubItems.Add(DateTime.Now.ToString("HH:mm:ss"));
        item.SubItems.Add(tx.OriginalReceiptRecordId != null ? "doc " + (FindOriginalDocNumber(tx.OriginalReceiptRecordId) ?? "?") : "");

        _txListView.Items.Insert(0, item); // newest on top
        _counterLabel.Text = $"Transazioni: {_store.Count} | Prossimo docNum: {_store.PeekNextDocumentNumber()}";
    }

    private string? FindOriginalDocNumber(string receiptRecordId)
    {
        var original = _store.FindByReceiptRecordId(receiptRecordId);
        return original?.DocumentNumber;
    }

    // ================================================================
    // Web server setup
    // ================================================================
    private WebApplication BuildWebApp()
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.WebHost.UseUrls("http://0.0.0.0:8180");
        builder.WebHost.SuppressStatusMessages(true);
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton(_store);
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        var app = builder.Build();
        var store = _store;
        // Cattura il riferimento al checkbox per accesso thread-safe dal web server
        var sendMessageCheckBox = _sendMessageCheckBox;

        // Helper per leggere il checkbox dal thread UI
        string GetMessage(string defaultMsg) =>
            sendMessageCheckBox.Checked ? defaultMsg : "";

        // Request logging middleware - esclude le chiamate del frontend
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? "";
            // Non loggare le richieste della dashboard HTML (auto-refresh ogni 5s)
            if (path != "/")
                Log($"{context.Request.Method} {path}", Color.FromArgb(100, 180, 255));
            await next();
        });

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
                <title>Fiskaly Mock</title>
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
                  .receipt { color:#4ecca3; } .cancellation { color:#e040a0; } .correction { color:#40a0e0; }
                  .empty { text-align:center; padding:30px; color:#555; }
                </style>
                </head><body>
                <h1>Fiskaly Mock Middleware</h1>
                <p class="sub">Simulatore per test GianoITA &mdash; nessuna connessione a Fiskaly</p>
                <span class="status">ONLINE - porta 8180</span>
                <div class="info">
                  <div><div class="label">Transazioni</div><div class="value">{{store.Count}}</div></div>
                  <div><div class="label">Prossimo docNum</div><div class="value">{{store.PeekNextDocumentNumber()}}</div></div>
                  <div><div class="label">Modalita</div><div class="value">SIMULATO</div></div>
                </div>
                <table>
                  <tr><th>DocNum</th><th>Tipo</th><th>Totale</th><th>ADE</th><th>ReceiptRecordId</th></tr>
                  {{(txRows.Length > 0 ? txRows : "<tr><td colspan='5' class='empty'>Nessuna transazione</td></tr>")}}
                </table>
                <p style="margin-top:16px;color:#555;font-size:11px;">Auto-refresh ogni 5 secondi</p>
                </body></html>
                """;
            return Results.Content(html, "text/html");
        });

        // GET /state - EFR compatibility (XML)
        // DEVE contenere tutti i nodi che EfrStateData.InternalParse() si aspetta,
        // altrimenti Giano crasha (NullRef su RegisteredClients e timeString)
        app.MapGet("/state", () =>
        {
            var now = DateTime.Now;
            var formattedDate = now.ToString("yyyy-MM-dd'T'HH:mm:ss");
            var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            var uptime = (long)(DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;

            var xml = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <state>
                  <name>Fiskaly Mock Middleware</name>
                  <version>1.0.0</version>
                  <manifest>Fiskaly Mock for Italy</manifest>
                  <Country>IT</Country>
                  <PID>{pid}</PID>
                  <uptime>{uptime}</uptime>
                  <Online>true</Online>
                  <Recorder>online</Recorder>
                  <Company></Company>
                  <EFR></EFR>
                  <RN>01</RN>
                  <RecSent>0</RecSent>
                  <RecQueued>0</RecQueued>
                  <RetryQueued>0</RetryQueued>
                  <TimeOffset>0</TimeOffset>
                  <D>{formattedDate}</D>
                  <DiskUsage>0.0</DiskUsage>
                  <DiskQuota>1000000000</DiskQuota>
                </state>
                """;
            return Results.Content(xml, "application/xml");
        });

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
            // Training/demo: docNumber sempre "0", Live: incrementa contatore
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
            AddTransactionToList(tx);

            var env = isTraining ? "DEMO" : "LIVE";
            var docSource = request.DocumentNumber != null ? "da Giano" : (isTraining ? "fisso 0" : "generato");
            Log($"  RECEIPT [{env}] docNum={docNumber} ({docSource}) | {totalAmount:F2} | ADE: {adeNumber}", Color.FromArgb(80, 200, 120));
            Log($"         receiptRecordId={receiptRecordId}", Color.FromArgb(120, 170, 120));

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
            // Log what Giano sent
            Log($"  Cancel request: docNum={request.DocumentNumber ?? "null"}, receiptId={request.OriginalReceiptRecordId ?? "null"}", Color.FromArgb(200, 180, 220));

            // Find original transaction
            StoredTransaction? original = null;
            if (!string.IsNullOrEmpty(request.OriginalReceiptRecordId))
                original = store.FindByReceiptRecordId(request.OriginalReceiptRecordId);
            if (original == null && !string.IsNullOrEmpty(request.DocumentNumber))
                original = store.FindByDocumentNumber(request.DocumentNumber);

            if (original != null)
            {
                Log($"  Originale TROVATO: docNum={original.DocumentNumber}, tipo={original.Type}, totale={original.TotalAmount:F2}", Color.FromArgb(80, 200, 120));
            }
            else
            {
                Log($"  ATTENZIONE: originale NON trovato in memoria!", Color.FromArgb(255, 165, 0));
                Log($"  Transazioni in memoria: {string.Join(", ", store.GetAll().Select(t => $"#{t.DocumentNumber}({t.Type})"))}", Color.FromArgb(255, 165, 0));
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
            AddTransactionToList(tx);

            Log($"  CANCEL OK -> nuovo docNum={cancelDocNumber} | ADE: {adeNumber}", Color.FromArgb(220, 120, 220));

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
            Log($"  Refund request: docNum={request.DocumentNumber ?? "null"}, receiptId={request.OriginalReceiptRecordId ?? "null"}", Color.FromArgb(150, 180, 220));

            StoredTransaction? original = null;
            if (!string.IsNullOrEmpty(request.OriginalReceiptRecordId))
                original = store.FindByReceiptRecordId(request.OriginalReceiptRecordId);
            if (original == null && !string.IsNullOrEmpty(request.DocumentNumber))
                original = store.FindByDocumentNumber(request.DocumentNumber);

            if (original != null)
                Log($"  Originale TROVATO: docNum={original.DocumentNumber}", Color.FromArgb(80, 200, 120));
            else
                Log($"  ATTENZIONE: originale NON trovato in memoria!", Color.FromArgb(255, 165, 0));

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
            AddTransactionToList(tx);

            Log($"  REFUND OK -> docNum={refundDocNumber} | {totalAmount:F2} | ADE: {adeNumber}", Color.FromArgb(100, 150, 255));

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

        return app;
    }
}
