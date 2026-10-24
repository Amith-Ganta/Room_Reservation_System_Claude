using System.ComponentModel.DataAnnotations;
using RoomReservation.Api.Configuration;
using RoomReservation.Api.Domain;

namespace RoomReservation.Api.Dtos;

/// <summary>Paging part of every list query string. Invalid values are rejected by model validation (HTTP 400).</summary>
public class PageQuery
{
    /// <summary>1-based page number. Defaults to 1.</summary>
    [Range(1, 100_000, ErrorMessage = "page must be between 1 and 100000.")]
    public int? Page { get; set; }

    /// <summary>Rows per page. Defaults to the configured size; a value above the configured maximum is clamped.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "pageSize must be 1 or greater.")]
    public int? PageSize { get; set; }

    /// <summary>Turns the optional query values into a checked page, using the configured default and maximum.</summary>
    public PageRequest ToRequest(PagingOptions options)
    {
        var size = Math.Min(PageSize ?? options.DefaultPageSize, options.MaxPageSize);
        return new PageRequest(Page ?? 1, size);
    }
}
