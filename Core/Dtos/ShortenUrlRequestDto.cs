using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Core.Dtos;

public record ShortenUrlRequestDto(
    [Required(ErrorMessage = "URL adresi boş olamaz.")]
    [Url(ErrorMessage = "Geçerli bir URL adresi giriniz.")]
    [StringLength(2048, MinimumLength = 10, ErrorMessage = "URL uzunluğu 10 ile 2048 karakter arasında olmalıdır.")]
    string Url
);
