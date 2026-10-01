namespace Hakufu.Services;

/// <summary>
/// Orden "natural": los números se comparan por su valor ("Tomo 2" antes que
/// "Tomo 10"); el resto, como texto sin distinguir mayúsculas.
/// </summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            int c;
            if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
            {
                // Sin ceros a la izquierda, más cifras = número mayor.
                var a = Chunk(x, ref i, digits: true).TrimStart('0');
                var b = Chunk(y, ref j, digits: true).TrimStart('0');
                c = a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
            }
            else
            {
                c = string.Compare(Chunk(x, ref i, digits: false), Chunk(y, ref j, digits: false),
                                   StringComparison.CurrentCultureIgnoreCase);
            }
            if (c != 0) return c;
        }
        var rest = (x.Length - i).CompareTo(y.Length - j);
        // Iguales salvo ceros o mayúsculas: desempate estable.
        return rest != 0 ? rest : string.CompareOrdinal(x, y);
    }

    private static string Chunk(string s, ref int i, bool digits)
    {
        var start = i;
        while (i < s.Length && char.IsDigit(s[i]) == digits) i++;
        return s[start..i];
    }
}
