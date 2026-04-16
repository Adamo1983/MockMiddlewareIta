using FiskalyMock.Models;

namespace FiskalyMock;

/// <summary>
/// Interfaccia per i callback dal server HTTP verso la UI.
/// Implementata da MainForm.
/// </summary>
public interface IMockCallbacks
{
    void Log(string message, Color color);
    void AddTransactionToList(StoredTransaction tx);
    int NextSequenceNumber();
    bool SendMessageEnabled { get; }
}
