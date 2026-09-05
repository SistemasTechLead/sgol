namespace Sgol.Cv02Demo;

internal sealed class WeeklyPlanModel(ScenarioCoordinator coordinator, DemoState state)
    : DemoPageModel("plan-semanal", "Elegibilidad, asignación y plan semanal", coordinator, state);
