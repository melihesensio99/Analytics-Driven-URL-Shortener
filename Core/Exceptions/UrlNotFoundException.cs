namespace UrlShortener.Core.Exceptions;

public class UrlNotFoundException : Exception
{
    public string ShortCode { get; }

    public UrlNotFoundException(string shortCode) 
        : base($"Kısa URL kodu '{shortCode}' sistemde bulunamadı.")
    {
        ShortCode = shortCode;
    }
}
