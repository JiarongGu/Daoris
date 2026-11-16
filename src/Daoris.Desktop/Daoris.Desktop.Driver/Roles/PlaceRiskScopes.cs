namespace Daoris.Driver;

/// <summary>The place-risk record (<see cref="PlaceRiskBook"/>, kept with the settings) read by a role's scope and place.</summary>
public static class PlaceRiskScopes
{
    extension(PlaceRiskBook book)
    {
        /// <summary>The key of the place a scope names.</summary>
        public static string KeyOf(RoleScope scope) => scope.Kind switch
        {
            RoleScope.Repository => $"repository/{scope.Id}",
            RoleScope.Workspace => $"workspace/{scope.Id}",
            _ => PlaceRiskBook.MachineKey,
        };

        /// <summary>The record set on exactly the place <paramref name="scope"/> names; none where nothing is.</summary>
        public PlaceRisk? Own(RoleScope scope) => book.Places.GetValueOrDefault(PlaceRiskBook.KeyOf(scope));

        /// <summary>What stands at <paramref name="place"/>: the narrowest risk set, and every destination mark from the machine down.</summary>
        public PlaceRiskNow At(RolePlace place) => book.At(place.Workspace, place.Repository);

        /// <summary>Whether the owner holds the level of the reporting line at <paramref name="scope"/> for <paramref name="kind"/>, by a line's mark.</summary>
        public bool OwnerHoldsLine(RoleScope scope, string kind) =>
            book.Own(scope) is { } own && (own.Marked(PlaceMarks.Line) || own.Marked(PlaceMarks.LineOf(kind)));
    }
}
