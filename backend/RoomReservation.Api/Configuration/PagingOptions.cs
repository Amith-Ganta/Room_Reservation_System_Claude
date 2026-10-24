using System.ComponentModel.DataAnnotations;

namespace RoomReservation.Api.Configuration;

/// <summary>Page sizes of the list endpoints. A client may ask for a smaller or larger page, never above the maximum.</summary>
public sealed class PagingOptions : IValidatableObject
{
    public const string SectionName = "Paging";

    /// <summary>Rows per page when the request does not send <c>pageSize</c>.</summary>
    [Range(1, 1000)]
    public int DefaultPageSize { get; set; }

    /// <summary>Largest page a client can get. A bigger <c>pageSize</c> is clamped to this value.</summary>
    [Range(1, 1000)]
    public int MaxPageSize { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DefaultPageSize > MaxPageSize)
        {
            yield return new ValidationResult(
                "DefaultPageSize must not be larger than MaxPageSize.", [nameof(DefaultPageSize)]);
        }
    }
}
