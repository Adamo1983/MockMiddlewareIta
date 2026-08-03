using FiskalyMock.Models;
using Microsoft.AspNetCore.Builder;

namespace FiskalyMock;

public enum MockLanguage { Italian, German, Austrian }

public class MainForm : Form, IMockCallbacks
{
    // UI controls
    private readonly RichTextBox _logBox;
    private readonly ListView _txListView;
    private readonly Button _startButton;
    private readonly Button _stopButton;
    private readonly Button _clearButton;
    private readonly Label _statusLabel;
    private readonly Label _counterLabel;
    private readonly CheckBox _sendMessageCheckBox;
    private readonly SplitContainer _splitContainer;
    private readonly RadioButton _radioIT;
    private readonly RadioButton _radioDE;
    private readonly RadioButton _radioAT;
    private readonly TextBox _portTextBox;

    // Server state
    private WebApplication? _webApp;
    private CancellationTokenSource? _cts;
    private TransactionStore _store = new();
    private int _efrSequenceNumber;

    // IMockCallbacks
    public bool SendMessageEnabled => _sendMessageCheckBox.Checked;
    public int NextSequenceNumber() => Interlocked.Increment(ref _efrSequenceNumber);

    private MockLanguage SelectedLanguage =>
        _radioDE.Checked ? MockLanguage.German
        : _radioAT.Checked ? MockLanguage.Austrian
        : MockLanguage.Italian;

    private static string LanguageDescription(MockLanguage language) => language switch
    {
        MockLanguage.Italian => "IT (Fiskaly)",
        MockLanguage.German => "DE (Efsta/EFR)",
        _ => "AT (Efsta/EFR)"
    };

    public MainForm()
    {
        Text = "Fiskaly / Efsta Mock Middleware";
        Size = new Size(950, 620);
        MinimumSize = new Size(750, 480);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9);

        // === Top panel ===
        var topPanel = new Panel { Dock = DockStyle.Top, Height = 90, Padding = new Padding(8, 8, 8, 4) };

        // Row 1: Language + Port + Checkbox
        var langLabel = new Label
        {
            Text = "Lingua:", AutoSize = true, Location = new Point(8, 12),
            Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(60, 60, 60)
        };

        var flagSize = new Size(18, 18);

        var itFlag = new PictureBox
        {
            Image = new Bitmap(Image.FromFile(Path.Combine(AppContext.BaseDirectory, "Images", "ItalianFlag.png")), flagSize),
            Size = flagSize, Location = new Point(68, 12), SizeMode = PictureBoxSizeMode.StretchImage
        };

        _radioIT = new RadioButton
        {
            Text = "IT", AutoSize = true, Location = new Point(88, 10),
            Checked = true, Font = new Font("Segoe UI", 9.5f)
        };

        var deFlag = new PictureBox
        {
            Image = new Bitmap(Image.FromFile(Path.Combine(AppContext.BaseDirectory, "Images", "GermanFlag.png")), flagSize),
            Size = flagSize, Location = new Point(148, 12), SizeMode = PictureBoxSizeMode.StretchImage
        };

        _radioDE = new RadioButton
        {
            Text = "DE", AutoSize = true, Location = new Point(168, 10),
            Font = new Font("Segoe UI", 9.5f)
        };

        var atFlag = new PictureBox
        {
            Image = new Bitmap(Image.FromFile(Path.Combine(AppContext.BaseDirectory, "Images", "AustrianFlag.png")), flagSize),
            Size = flagSize, Location = new Point(228, 12), SizeMode = PictureBoxSizeMode.StretchImage
        };

        _radioAT = new RadioButton
        {
            Text = "AT", AutoSize = true, Location = new Point(248, 10),
            Font = new Font("Segoe UI", 9.5f)
        };

        var portLabel = new Label
        {
            Text = "Porta:", AutoSize = true, Location = new Point(320, 12),
            Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(60, 60, 60)
        };

        _portTextBox = new TextBox
        {
            Text = "8180", Size = new Size(60, 26), Location = new Point(370, 9),
            Font = new Font("Segoe UI", 9.5f), TextAlign = HorizontalAlignment.Center, MaxLength = 5
        };
        _portTextBox.KeyPress += (_, e) => { if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar)) e.Handled = true; };

        // Collegati dopo _portTextBox per evitare warning CS8602
        _radioIT.CheckedChanged += (s, _) => { if (s is RadioButton rb && rb.Checked && _webApp == null) _portTextBox.Text = "8180"; };
        _radioDE.CheckedChanged += (s, _) => { if (s is RadioButton rb && rb.Checked && _webApp == null) _portTextBox.Text = "5618"; };
        _radioAT.CheckedChanged += (s, _) => { if (s is RadioButton rb && rb.Checked && _webApp == null) _portTextBox.Text = "5618"; };

        _sendMessageCheckBox = new CheckBox
        {
            Text = "Invia messaggio nella risposta", Checked = true, AutoSize = true,
            Location = new Point(460, 11), ForeColor = Color.FromArgb(80, 80, 80), Font = new Font("Segoe UI", 8.5f)
        };

        // Row 2: Buttons + Status
        _startButton = new Button
        {
            Text = "Start", Size = new Size(80, 32), Location = new Point(8, 48),
            BackColor = Color.FromArgb(46, 139, 87), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
        };
        _startButton.Click += OnStartClick;

        _stopButton = new Button
        {
            Text = "Stop", Size = new Size(80, 32), Location = new Point(96, 48),
            BackColor = Color.FromArgb(178, 34, 34), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Enabled = false, Cursor = Cursors.Hand
        };
        _stopButton.Click += OnStopClick;

        _clearButton = new Button
        {
            Text = "Clear Log", Size = new Size(80, 32), Location = new Point(184, 48),
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
        };
        _clearButton.Click += (_, _) => _logBox?.Clear();

        _statusLabel = new Label
        {
            Text = "  Stopped", ForeColor = Color.Gray, AutoSize = true,
            Location = new Point(280, 55), Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        _counterLabel = new Label
        {
            Text = "", ForeColor = Color.DimGray, AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(650, 55), Font = new Font("Segoe UI", 9)
        };

        topPanel.Controls.AddRange(new Control[]
        {
            langLabel, itFlag, _radioIT, deFlag, _radioDE, atFlag, _radioAT, portLabel, _portTextBox, _sendMessageCheckBox,
            _startButton, _stopButton, _clearButton, _statusLabel, _counterLabel
        });

        // === Split container ===
        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Horizontal,
            SplitterDistance = 180, Panel1MinSize = 80, Panel2MinSize = 150
        };

        // Transaction list (top)
        _txListView = new ListView
        {
            Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true,
            Font = new Font("Segoe UI", 8.5f), BackColor = Color.FromArgb(245, 245, 250)
        };
        _txListView.Columns.Add("DocNum", 65, HorizontalAlignment.Center);
        _txListView.Columns.Add("Tipo", 100, HorizontalAlignment.Left);
        _txListView.Columns.Add("Totale", 80, HorizontalAlignment.Right);
        _txListView.Columns.Add("ADE/SQ", 160, HorizontalAlignment.Left);
        _txListView.Columns.Add("ReceiptRecordId", 280, HorizontalAlignment.Left);
        _txListView.Columns.Add("Data", 80, HorizontalAlignment.Center);
        _txListView.Columns.Add("Ora", 70, HorizontalAlignment.Center);
        _txListView.Columns.Add("Ref. Originale", 80, HorizontalAlignment.Center);

        var txLabel = new Label
        {
            Text = " Transazioni in memoria:", Dock = DockStyle.Top, Height = 20,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.DimGray,
            BackColor = Color.FromArgb(235, 235, 240), TextAlign = ContentAlignment.MiddleLeft
        };

        _splitContainer.Panel1.Controls.Add(_txListView);
        _splitContainer.Panel1.Controls.Add(txLabel);

        // Log area (bottom)
        _logBox = new RichTextBox
        {
            Dock = DockStyle.Fill, ReadOnly = false,
            BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Cascadia Code", 9.5f, FontStyle.Regular, GraphicsUnit.Point, 0, false),
            BorderStyle = BorderStyle.None, WordWrap = false, ShortcutsEnabled = true
        };
        if (!_logBox.Font.Name.Equals("Cascadia Code", StringComparison.OrdinalIgnoreCase))
            _logBox.Font = new Font("Consolas", 9.5f);
        _logBox.KeyPress += (_, e) => e.Handled = true;
        _logBox.KeyDown += (_, e) =>
        {
            if (e.Control && (e.KeyCode == Keys.C || e.KeyCode == Keys.A)) return;
            e.SuppressKeyPress = true;
        };

        _splitContainer.Panel2.Controls.Add(_logBox);

        Controls.Add(_splitContainer);
        Controls.Add(topPanel);

        FormClosing += OnFormClosing;
    }

    // ================================================================
    // Server lifecycle
    // ================================================================

    private void OnStartClick(object? sender, EventArgs e)
    {
        if (!int.TryParse(_portTextBox.Text.Trim(), out var port) || port < 1 || port > 65535)
        {
            Log("ERRORE: Porta non valida. Inserisci un numero tra 1 e 65535.", Color.Red);
            return;
        }

        _startButton.Enabled = false;
        _radioIT.Enabled = false;
        _radioDE.Enabled = false;
        _radioAT.Enabled = false;
        _portTextBox.Enabled = false;
        _store = new TransactionStore();
        _txListView.Items.Clear();
        _efrSequenceNumber = 0;

        try
        {
            _cts = new CancellationTokenSource();
            _webApp = MockServerBuilder.Build(port, SelectedLanguage, _store, this);
            _ = _webApp.StartAsync(_cts.Token);

            _stopButton.Enabled = true;
            var lang = LanguageDescription(SelectedLanguage);
            _statusLabel.Text = $"  Listening on :{port} [{lang}]";
            _statusLabel.ForeColor = Color.FromArgb(46, 139, 87);
            Log($"SERVER AVVIATO sulla porta {port} - Modalita: {lang}", Color.FromArgb(80, 200, 120));
            Log($"Contatori caricati da file: prossimo docNum={_store.PeekNextDocumentNumber()}", Color.FromArgb(180, 180, 100));
            _counterLabel.Text = $"Transazioni: 0 | Prossimo docNum: {_store.PeekNextDocumentNumber()}";
            Log("In attesa di richieste da GianoITA...\n", Color.Gray);
        }
        catch (Exception ex)
        {
            _startButton.Enabled = true;
            _radioIT.Enabled = true;
            _radioDE.Enabled = true;
            _radioAT.Enabled = true;
            _portTextBox.Enabled = true;
            Log($"ERRORE AVVIO: {ex.Message}", Color.Red);
        }
    }

    private async void OnStopClick(object? sender, EventArgs e)
    {
        _stopButton.Enabled = false;
        await StopServer();
        _startButton.Enabled = true;
        _radioIT.Enabled = true;
        _radioDE.Enabled = true;
        _radioAT.Enabled = true;
        _portTextBox.Enabled = true;
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

    // ================================================================
    // IMockCallbacks implementation
    // ================================================================

    public void Log(string message, Color color)
    {
        if (InvokeRequired) { BeginInvoke(() => Log(message, color)); return; }

        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionColor = Color.DimGray;
        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] ");
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionColor = color;
        _logBox.AppendText(message + "\n");
        _logBox.ScrollToCaret();
    }

    public void AddTransactionToList(StoredTransaction tx)
    {
        if (InvokeRequired) { BeginInvoke(() => AddTransactionToList(tx)); return; }

        var item = new ListViewItem(tx.DocumentNumber);
        item.ForeColor = tx.Type switch
        {
            "RECEIPT" => Color.DarkGreen,
            "CANCELLATION" or "VOID" => Color.DarkMagenta,
            "CORRECTION" => Color.DarkBlue,
            _ => Color.Black
        };

        item.SubItems.Add(tx.Type);
        item.SubItems.Add($"{tx.TotalAmount:F2}");
        item.SubItems.Add(tx.AdeProgressiveNumber ?? "");
        item.SubItems.Add(tx.ReceiptRecordId ?? "");
        var now = DateTime.Now;
        item.SubItems.Add(now.ToString("dd/MM/yyyy"));
        item.SubItems.Add(now.ToString("HH:mm:ss"));

        string refText = "";
        if (tx.OriginalReceiptRecordId != null)
        {
            var original = _store.FindByReceiptRecordId(tx.OriginalReceiptRecordId);
            refText = "doc " + (original?.DocumentNumber ?? "?");
        }
        item.SubItems.Add(refText);

        _txListView.Items.Insert(0, item);
        _counterLabel.Text = $"Transazioni: {_store.Count} | Prossimo docNum: {_store.PeekNextDocumentNumber()}";
    }
}
