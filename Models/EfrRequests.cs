using System.Globalization;
using System.Xml;

namespace FiskalyMock.Models;

/// <summary>
/// Request ricevuta da Giano per il Transaction Start (Germany step 1).
/// XML: <TraS><ESR TL="0000" TT="01"/></TraS>
/// </summary>
public class EfrTransactionStartRequest
{
    public string LocationId { get; set; } = "0000";
    public string TerminalId { get; set; } = "01";

    public static EfrTransactionStartRequest Parse(XmlElement root)
    {
        var esrNode = root.SelectSingleNode("ESR");
        return new EfrTransactionStartRequest
        {
            LocationId = esrNode?.Attributes?["TL"]?.InnerText ?? "0000",
            TerminalId = esrNode?.Attributes?["TT"]?.InnerText ?? "01"
        };
    }
}

/// <summary>
/// Request ricevuta da Giano per una transazione completa (Germany step 2) o void.
/// XML: <Tra><ESR TID="1" T="123.45" Opr="01" NFS="TRAINING" Void="1" RFN="ref">
///        <PosA>...</PosA><PayA>...</PayA><TaxA>...</TaxA>
///      </ESR></Tra>
/// </summary>
public class EfrTransactionRequest
{
    private static readonly NumberFormatInfo Nfi = new() { NumberDecimalSeparator = ".", NumberGroupSeparator = "" };

    public int? TransactionId { get; set; }
    public double Total { get; set; }
    public string? OperatorId { get; set; }
    public string? OperatorName { get; set; }
    public bool IsTraining { get; set; }
    public bool IsVoid { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? LocationId { get; set; }
    public string? TerminalId { get; set; }

    public List<EfrPositionEntry> Positions { get; set; } = new();
    public List<EfrPaymentEntry> Payments { get; set; } = new();
    public List<EfrTaxEntry> Taxes { get; set; } = new();

    public static EfrTransactionRequest Parse(XmlElement root)
    {
        var esrNode = root.SelectSingleNode("ESR");
        if (esrNode == null)
            throw new InvalidOperationException("Missing ESR element in transaction XML");

        var req = new EfrTransactionRequest();

        // ESR attributes
        var tidStr = esrNode.Attributes?["TID"]?.InnerText;
        if (tidStr != null && int.TryParse(tidStr, out var tid))
            req.TransactionId = tid;

        var totalStr = esrNode.Attributes?["T"]?.InnerText;
        if (totalStr != null)
            double.TryParse(totalStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var total);
        req.Total = totalStr != null && double.TryParse(totalStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var t) ? t : 0;

        req.OperatorId = esrNode.Attributes?["Opr"]?.InnerText;
        req.OperatorName = esrNode.Attributes?["OprN"]?.InnerText;
        req.IsTraining = esrNode.Attributes?["NFS"]?.InnerText == "TRAINING";
        req.IsVoid = esrNode.Attributes?["Void"]?.InnerText == "1";
        req.ReferenceNumber = esrNode.Attributes?["RFN"]?.InnerText;
        req.LocationId = esrNode.Attributes?["TL"]?.InnerText;
        req.TerminalId = esrNode.Attributes?["TT"]?.InnerText;

        // Position entries <PosA><Pos .../><Mod .../></PosA>
        var posANode = esrNode.SelectSingleNode("PosA");
        if (posANode != null)
        {
            foreach (XmlNode child in posANode.ChildNodes)
            {
                if (child.Name is "Pos" or "Mod")
                {
                    var entry = new EfrPositionEntry
                    {
                        IsModification = child.Name == "Mod",
                        PositionNumber = child.Attributes?["PN"]?.InnerText,
                        Description = child.Attributes?["Dsc"]?.InnerText,
                        TaxGroup = child.Attributes?["TaxG"]?.InnerText
                    };

                    var qtyStr = child.Attributes?["Qty"]?.InnerText;
                    if (qtyStr != null && double.TryParse(qtyStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty))
                        entry.Quantity = qty;

                    var amtStr = child.Attributes?["Amt"]?.InnerText;
                    if (amtStr != null && double.TryParse(amtStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amt))
                        entry.Amount = amt;

                    req.Positions.Add(entry);
                }
            }
        }

        // Payment entries <PayA><Pay .../></PayA>
        var payANode = esrNode.SelectSingleNode("PayA");
        if (payANode != null)
        {
            foreach (XmlNode child in payANode.ChildNodes)
            {
                if (child.Name == "Pay")
                {
                    var entry = new EfrPaymentEntry
                    {
                        Description = child.Attributes?["Dsc"]?.InnerText,
                        PaymentGroup = child.Attributes?["PayG"]?.InnerText
                    };

                    var amtStr = child.Attributes?["Amt"]?.InnerText;
                    if (amtStr != null && double.TryParse(amtStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amt))
                        entry.Amount = amt;

                    req.Payments.Add(entry);
                }
            }
        }

        // Tax entries <TaxA><Tax .../></TaxA>
        var taxANode = esrNode.SelectSingleNode("TaxA");
        if (taxANode != null)
        {
            foreach (XmlNode child in taxANode.ChildNodes)
            {
                if (child.Name == "Tax")
                {
                    var entry = new EfrTaxEntry
                    {
                        TaxGroup = child.Attributes?["TaxG"]?.InnerText
                    };

                    var prcStr = child.Attributes?["Prc"]?.InnerText;
                    if (prcStr != null && double.TryParse(prcStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var prc))
                        entry.Percentage = prc;

                    var amtStr = child.Attributes?["Amt"]?.InnerText;
                    if (amtStr != null && double.TryParse(amtStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amt))
                        entry.Amount = amt;

                    req.Taxes.Add(entry);
                }
            }
        }

        return req;
    }
}

public class EfrPositionEntry
{
    public bool IsModification { get; set; }
    public string? PositionNumber { get; set; }
    public string? Description { get; set; }
    public double? Quantity { get; set; }
    public string? TaxGroup { get; set; }
    public double? Amount { get; set; }
}

public class EfrPaymentEntry
{
    public string? Description { get; set; }
    public string? PaymentGroup { get; set; } // "0" = cash, "1" = card
    public double? Amount { get; set; }
}

public class EfrTaxEntry
{
    public string? TaxGroup { get; set; }
    public double? Percentage { get; set; }
    public double? Amount { get; set; }
}
