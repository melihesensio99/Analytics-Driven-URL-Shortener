using System.Text;
using UrlShortener.Core.Interfaces;

namespace UrlShortener.Infrastructure.Services;

public class Base62Encoder : IBase62Encoder
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public string Encode(long number)
    {
        if (number == 0) return Alphabet[0].ToString();

        var result = new StringBuilder();
        while (number > 0)
        {
            result.Insert(0, Alphabet[(int)(number % 62)]);
            number /= 62;
        }

        return result.ToString();
    }

    public long Decode(string str)
    {
        long result = 0;
        foreach (char c in str)
        {
            int index = Alphabet.IndexOf(c);
            if (index == -1) throw new ArgumentException($"Invalid Base62 character: {c}", nameof(str));
            result = result * 62 + index;
        }
        return result;
    }
}
