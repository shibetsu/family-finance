using System.Globalization;
using System.Text.RegularExpressions;
using FinTool.Models;

namespace FinTool.Services;

public static partial class DesjardinsParser
{
    // Format detection:
    //   "Solde"        → debit account
    //   "BONIDOLLARS"  → credit card with rewards (multi-line blocks + some single-line)
    //   otherwise      → original credit card (all single-line)
    public static (List<Transaction> Transactions, string AccountType) Parse(string paste)
    {
        if (paste.Contains("Solde", StringComparison.OrdinalIgnoreCase))
            return (ParseDebit(paste), "debit");

        if (paste.Contains("BONIDOLLARS", StringComparison.OrdinalIgnoreCase))
            return (ParseBonidollarsCredit(paste), "credit");

        return (ParseCreditCard(paste), "credit");
    }

    // -----------------------------------------------------------------------
    // Original credit card  (Date | Description | Montant | Lien)
    // Each transaction = one tab-separated line + one readable duplicate line.
    // -----------------------------------------------------------------------
    private static List<Transaction> ParseCreditCard(string paste)
    {
        var result = new List<Transaction>();

        foreach (var line in paste.ReplaceLineEndings("\n").Split('\n'))
        {
            if (!line.Contains('\t')) continue;

            var parts = line.Split('\t');
            if (parts.Length < 3) continue;

            var dateField = parts[0].Trim();
            if (dateField.Equals("Date", StringComparison.OrdinalIgnoreCase)) continue;
            if (!TryParseDate(dateField, out var date)) continue;

            var description = parts[1].Trim();
            if (string.IsNullOrWhiteSpace(description)) continue;

            if (!TryParseAmount(parts[2].Trim(), "credit", out var amount)) continue;

            result.Add(new Transaction
            {
                Date = date,
                Description = description,
                Amount = amount,
                AccountType = "credit"
            });
        }

        return result;
    }

    // -----------------------------------------------------------------------
    // BONIDOLLARS credit card  (Date | Description | BONIDOLLARS | Montant | Lien)
    //
    // Two transaction shapes appear in the same paste:
    //
    // Shape A — multi-line purchase (most transactions):
    //   {date}\t\t\t\t          ← date line, other fields empty
    //   {DesjardinsCategory}       ← e.g. "Restaurants"
    //   {MerchantName}             ← e.g. "Domino'S Pizza"
    //   {bonidollars%}\t{amount}\t ← e.g. "3 %\t40,22 $\t"
    //   {readable duplicate}       ← skipped
    //
    // Shape B — single-line (payments, refunds, bonidollars credits, simple):
    //   {date}\t{description}\t{bonidollars%}\t{amount}\t[{readable dup}]
    //   e.g. "28 MAI28 Mai\tPaiement\t\t+5 054,53 $\t..."
    //
    // Detection: tab line where parts[0] is a date AND parts[1] is non-empty
    //            AND parts[3] contains '$' → Shape B.
    //            Otherwise → start of a Shape A block.
    // -----------------------------------------------------------------------
    private static List<Transaction> ParseBonidollarsCredit(string paste)
    {
        var result = new List<Transaction>();
        DateOnly? date = null;
        string? category = null;
        string? merchant = null;

        foreach (var rawLine in paste.ReplaceLineEndings("\n").Split('\n'))
        {
            var trimmed = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            // Readable duplicate: no tab, starts with "DD Mon " and ends with an amount
            if (!rawLine.Contains('\t') && ReadableDuplicateRx().IsMatch(trimmed)) continue;

            if (rawLine.Contains('\t'))
            {
                var parts = rawLine.Split('\t');
                var first = parts[0].Trim();

                // Skip header and summary lines
                if (first.Equals("Date", StringComparison.OrdinalIgnoreCase)) continue;
                if (first.Equals("Total", StringComparison.OrdinalIgnoreCase)) continue;

                if (TryParseDate(first, out var parsedDate))
                {
                    // Distinguish Shape B (single-line) from Shape A date-opener.
                    // Shape B: parts[1] is non-empty AND parts[3] contains '$'
                    var desc = parts.Length > 1 ? parts[1].Trim() : "";
                    var amountField = parts.Length > 3 ? parts[3].Trim() : "";

                    if (!string.IsNullOrEmpty(desc) && amountField.Contains('$'))
                    {
                        // Shape B — emit immediately
                        if (TryParseAmount(amountField, "credit", out var amount))
                            result.Add(new Transaction
                            {
                                Date = parsedDate,
                                Description = desc,
                                Amount = amount,
                                AccountType = "credit"
                            });
                    }
                    else
                    {
                        // Shape A — start a new multi-line block
                        date = parsedDate;
                        category = null;
                        merchant = null;
                    }
                }
                else if (date.HasValue && category != null)
                {
                    // Shape A amount line: parts[0] = "3 %" (bonidollars%), parts[1] = "40,22 $"
                    var amountField = parts.Length > 1 ? parts[1].Trim() : "";
                    if (amountField.Contains('$') && TryParseAmount(amountField, "credit", out var amount))
                    {
                        var desc = merchant != null ? $"{category} – {merchant}" : category;
                        result.Add(new Transaction
                        {
                            Date = date.Value,
                            Description = desc!,
                            Amount = amount,
                            AccountType = "credit"
                        });
                    }

                    date = null; category = null; merchant = null;
                }
            }
            else if (date.HasValue)
            {
                // Non-tab line inside a Shape A block: first non-tab = category, second = merchant
                if (category == null)
                    category = trimmed;
                else if (merchant == null)
                    merchant = trimmed;
            }
        }

        return result;
    }

    // -----------------------------------------------------------------------
    // Debit account  (Date | Description | Montant | Solde | lien)
    //
    //   {date}\t\t\t\t          ← date line
    //   {category}                 ← e.g. "Virements"
    //   {sub-description}          ← optional, e.g. "Virement automatique /à …"
    //   (blank line)
    //   {amount}\t{balance}\t      ← amount has Unicode minus '−' for withdrawals
    //   {readable duplicate}       ← skipped
    //
    // Description = category alone, or "category – sub-description".
    // -----------------------------------------------------------------------
    private static List<Transaction> ParseDebit(string paste)
    {
        var result = new List<Transaction>();
        DateOnly? date = null;
        string? category = null;
        string? subDesc = null;

        foreach (var rawLine in paste.ReplaceLineEndings("\n").Split('\n'))
        {
            var trimmed = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (!rawLine.Contains('\t') && ReadableDuplicateRx().IsMatch(trimmed)) continue;

            if (rawLine.Contains('\t'))
            {
                var parts = rawLine.Split('\t');
                var first = parts[0].Trim();

                if (first.Equals("Date", StringComparison.OrdinalIgnoreCase)) continue;

                // Amount line: first field contains '$'
                if (first.Contains('$') && date.HasValue && category != null)
                {
                    if (TryParseAmount(first, "debit", out var amount))
                    {
                        var desc = subDesc != null ? $"{category} – {subDesc}" : category;
                        result.Add(new Transaction
                        {
                            Date = date.Value,
                            Description = desc!,
                            Amount = amount,
                            AccountType = "debit"
                        });
                    }

                    date = null; category = null; subDesc = null;
                    continue;
                }

                // Date line
                if (TryParseDate(first, out var parsedDate))
                {
                    date = parsedDate;
                    category = null;
                    subDesc = null;
                }
            }
            else if (date.HasValue)
            {
                if (category == null)
                    category = trimmed;
                else if (subDesc == null)
                    subDesc = trimmed;
            }
        }

        return result;
    }

    // -----------------------------------------------------------------------
    // Shared helpers
    // -----------------------------------------------------------------------

    // No-tab line that starts with "DD Mon " and ends with an amount (readable duplicate)
    [GeneratedRegex(@"^\d{1,2}\s+\p{L}+\s+.+\d+,\d{2}\s*\$\s*$")]
    private static partial Regex ReadableDuplicateRx();

    // Extracts trailing "DD Mon" from date fields like "31 MAI31 Mai"
    [GeneratedRegex(@"(\d{1,2})\s+(\p{L}+)\s*$")]
    private static partial Regex DateRx();

    private static bool TryParseDate(string input, out DateOnly date)
    {
        date = default;
        var m = DateRx().Match(input);
        if (!m.Success) return false;
        if (!int.TryParse(m.Groups[1].Value, out var day)) return false;
        if (!MonthMap.TryGetValue(m.Groups[2].Value, out var month)) return false;

        var now = DateOnly.FromDateTime(DateTime.Now);
        var year = month <= now.Month ? now.Year : now.Year - 1;

        try { date = new DateOnly(year, month, day); return true; }
        catch { return false; }
    }

    // Sign convention: positive = expense (money out), negative = income/refund.
    // Credit: no prefix → charge (+); '+' prefix → refund/credit (−).
    // Debit:  '−' (Unicode minus) → withdrawal; negate to get positive expense.
    private static bool TryParseAmount(string raw, string accountType, out decimal amount)
    {
        amount = 0;
        var s = raw
            .Replace("−", "-")       // U+2212 MINUS SIGN → hyphen-minus
            .Replace("$", "")
            .Replace(" ", "")   // non-breaking space
            .Replace(" ", "")
            .Trim();

        if (string.IsNullOrEmpty(s)) return false;

        var hasPlus = s.StartsWith('+');
        s = s.TrimStart('+').Replace(",", ".");

        if (!decimal.TryParse(s, NumberStyles.Number | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var parsed))
            return false;

        amount = accountType == "debit"
            ? -parsed
            : (hasPlus ? -parsed : parsed);

        return true;
    }

    private static readonly Dictionary<string, int> MonthMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jan"] = 1, ["janv"] = 1, ["janvier"] = 1,
        ["fev"] = 2, ["févr"] = 2, ["fév"] = 2, ["fevr"] = 2, ["février"] = 2,
        ["mar"] = 3, ["mars"] = 3,
        ["avr"] = 4, ["avril"] = 4,
        ["mai"] = 5,
        ["jun"] = 6, ["juin"] = 6,
        ["jul"] = 7, ["juil"] = 7, ["juillet"] = 7,
        ["aou"] = 8, ["août"] = 8, ["aout"] = 8,
        ["sep"] = 9, ["sept"] = 9, ["septembre"] = 9,
        ["oct"] = 10, ["octobre"] = 10,
        ["nov"] = 11, ["novembre"] = 11,
        ["dec"] = 12, ["déc"] = 12, ["décembre"] = 12,
    };
}
