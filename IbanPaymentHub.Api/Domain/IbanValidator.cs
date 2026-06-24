using System.Text;

namespace IbanPaymentHub.Api.Domain;

public static class IbanValidator
{
    public static bool IsValid(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
            return false;

        var normalized = Normalize(iban);
        if (normalized.Length < 15 || normalized.Length > 34)
            return false;

        if (!normalized.All(char.IsLetterOrDigit))
            return false;

        if (!char.IsLetter(normalized[0]) || !char.IsLetter(normalized[1]))
            return false;

        var rearranged = normalized[4..] + normalized[..4];
        var numeric = new StringBuilder(rearranged.Length * 2);
        foreach (var ch in rearranged)
        {
            if (char.IsDigit(ch))
                numeric.Append(ch);
            else
                numeric.Append(ch - 'A' + 10);
        }

        return Mod97(numeric.ToString()) == 1;
    }

    public static string Normalize(string iban) =>
        new string(iban.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    private static int Mod97(string numeric)
    {
        var checksum = 0;
        foreach (var ch in numeric)
        {
            checksum = (checksum * 10 + (ch - '0')) % 97;
        }
        return checksum;
    }
}
