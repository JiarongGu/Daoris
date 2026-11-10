using Microsoft.Extensions.DependencyInjection;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// Each answered call that named a place, noted after its tool ran (<see cref="CallPlaceFile"/>): what passes Daoris's own door is
/// what Ask Daoris's receipt may claim, so a call its tool refused, or a filter before it, claims nothing. A search's line keeps how
/// many its answer found where its tool said so (<see cref="CallFound"/>).
/// </summary>
public static class CallPlaceFilter
{
    public static IMcpServerBuilder NoteCallPlaces(this IMcpServerBuilder builder, CallPlaceFile file) =>
        file.Path is null
            ? builder
            : builder.WithRequestFilters(filters => filters.AddCallToolFilter(next => async (request, cancellationToken) =>
            {
                var found = CallFound.Listen();
                var result = await next(request, cancellationToken).ConfigureAwait(false);
                if (result.IsError != true && CallPlace.Of(request.Params?.Name, request.Params?.Arguments) is { } place)
                {
                    file.Note(place with { Found = found.Count }, DateTimeOffset.UtcNow);
                }

                return result;
            }));
}
