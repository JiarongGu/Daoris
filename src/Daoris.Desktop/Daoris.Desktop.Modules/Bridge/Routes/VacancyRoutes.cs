using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The `vacancyHours` `daoris settings vacancy <hours>` edits: whole hours in the setting's bounds; out of them is the driver's refusal.
/// </summary>
public sealed class VacancyRoutes(DriverContext context)
{
    [DriverRoute("SET_VACANCY", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_VACANCY", None = OwnerNone.MachineSetting)]
    [GroundRead("SET_VACANCY", nameof(PipeReads.Vacancy))]
    [Publishes("SET_VACANCY", StateScopes.Driver)]
    private object? SetVacancy(SetVacancy press)
    {
        var hours = press.Hours is { } value && VacancySetting.Takes(value) ? (int)value : throw new DriverException(VacancySetting.Refusal);
        context.Change(config => config.WithVacancy(hours));
        return DriverRoutes.State(context.Loop);
    }
}
