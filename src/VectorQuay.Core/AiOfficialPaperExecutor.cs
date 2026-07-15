using System.Diagnostics;
using VectorQuay.Core.Configuration;

namespace VectorQuay.Core;

/// <summary>
/// Handles AI-driven paper trading execution with budget guardrails.
/// In "Guide Mode", the AI receives its current budget state as context rather than being hard-blocked.
/// </summary>
public sealed class AiOfficialPaperExecutor
{
    private readonly AppSettings _settings;

    public AiOfficialPaperExecutor(AppSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Executes a trade decision based on AI output.
    /// Returns a result indicating whether the trade was executed, blocked by risk, or passed with budget warnings.
    /// </summary>
    public async Task<ExecutionResult> ExecuteAsync(TradeDecision decision)
    {
        // Check Risk thresholds first (hard guardrails)
        var riskCheck = CheckRiskLimits();
        if (!riskCheck.IsOk)
        {
            return ExecutionResult.RiskBlocked(riskCheck.Message ?? "Unknown risk limit.");
        }

        // Check AI Budget (soft guardrail / guide)
        var budgetState = GetBudgetState();
        
        if (!_settings.AI.EnableGuideMode)
        {
            // Legacy hard-block mode
            if (budgetState.NeedsAlert)
            {
                return ExecutionResult.BudgetBlocked(budgetState.Message);
            }
        }

        // In Guide Mode, we proceed but log the budget state for the AI to see in the next cycle
        LogBudgetContext(budgetState);
        
        // Simulate paper execution
        await Task.Delay(10); 
        return ExecutionResult.Success(decision, budgetState);
    }

    private AiBudgetState GetBudgetState()
    {
        // In a real implementation, this would query token usage metrics
        return new AiBudgetState
        {
            Rolling24hUsage = 1500000, // Example usage
            Limit = _settings.AI.Rolling24hTokenLimit,
            NeedsAlert = false,
            Message = "Within safe limits."
        };
    }

    private (bool IsOk, string? Message) CheckRiskLimits()
    {
        // Hard risk checks from AppSettings.Risk go here
        return (true, null);
    }

    private void LogBudgetContext(AiBudgetState state)
    {
        // Log the context that would be passed to the AI for its next decision
        Debug.WriteLine($"[AI-Guide] Budget State: {state.Message}");
    }
}

public sealed class AiBudgetState
{
    public int Rolling24hUsage { get; set; }
    public int Limit { get; set; }
    public bool NeedsAlert { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class ExecutionResult
{
    public bool IsSuccess => Type == ResultType.Success;
    public bool IsBlocked => Type == ResultType.Blocked;
    public ResultType Type { get; init; }
    public string? Message { get; init; }
    public TradeDecision? Decision { get; init; }
    public AiBudgetState? BudgetState { get; init; }

    // Allow object initialization by providing a default constructor or making properties init-only with defaults
    public ExecutionResult() { Type = ResultType.Success; }

    private ExecutionResult(ResultType type) { Type = type; }

    public static ExecutionResult Success(TradeDecision decision, AiBudgetState budget) 
        => new() { Type = ResultType.Success, Decision = decision, BudgetState = budget };

    public static ExecutionResult RiskBlocked(string message) 
        => new() { Type = ResultType.Blocked, Message = $"Risk: {message}" };

    public static ExecutionResult BudgetBlocked(string message) 
        => new() { Type = ResultType.Blocked, Message = $"Budget: {message}" };
}

public enum ResultType { Success, Blocked }

public class TradeDecision
{
    public string Asset { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // Buy/Sell
    public decimal Amount { get; set; }
}
