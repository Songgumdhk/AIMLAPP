namespace AIMLAPP.Learning.ProductionAiDemo.Guardrails;

// Rule names which check fired, so a block can be logged and explained, not just denied.
public sealed record GuardDecision(bool Allowed, string Rule, string Detail);
