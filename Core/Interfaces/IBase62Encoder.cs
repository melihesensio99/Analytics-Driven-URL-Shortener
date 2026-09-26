namespace UrlShortener.Core.Interfaces;

public interface IBase62Encoder
{
    string Encode(long number);
    long Decode(string str);
}
