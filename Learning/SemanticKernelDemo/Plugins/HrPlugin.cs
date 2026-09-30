using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace AIMLAPP.Learning.SemanticKernelDemo.Plugins;

// One plugin can expose several related tools. In Chapter 1 these were three
// separate tool classes; here they are three [KernelFunction] methods.
public class HrPlugin
{
    private static readonly Dictionary<string, string[]> Employees =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Engineering"] = new[] { "Alice (Senior)", "Bob (Junior)", "Charlie (Lead)" },
            ["Sales"] = new[] { "Diana (Manager)", "Eve (Rep)" },
            ["HR"] = new[] { "Frank (Director)" }
        };

    private static readonly Dictionary<string, string> Emails =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Alice"] = "alice@company.com",
            ["Bob"] = "bob@company.com",
            ["Charlie"] = "charlie@company.com",
            ["Diana"] = "diana@company.com",
            ["Eve"] = "eve@company.com",
            ["Frank"] = "frank@company.com",
        };

    [KernelFunction("search_database")]
    [Description("Look up employees by department. Use this when the user asks " +
                 "who works in a given team, department, or group.")]
    // Chapter 1 used a JSON "enum" to constrain department. A plain string has no
    // enum, so the valid values go in the description as the model's only hint.
    public string SearchDatabase(
        [Description("Department name. Valid values: Engineering, Sales, HR.")] string department)
    {
        if (!Employees.TryGetValue(department, out var list))
            return $"No employees found in department '{department}'.";

        return JsonSerializer.Serialize(new { department, employees = list });
    }

    [KernelFunction("get_employee_email")]
    [Description("Look up an employee's email address by name. Usually called " +
                 "AFTER search_database has identified the employee.")]
    public string GetEmail(
        [Description("Employee first or full name, e.g. 'Frank'.")] string name)
    {
        // Tolerate "Frank (Director)" exactly as search_database returned it.
        var firstName = name.Split('(')[0].Trim();
        return Emails.TryGetValue(firstName, out var email)
            ? email
            : $"No email on file for '{firstName}'.";
    }

    // Gated by ApprovalFilter. The filter matches on function NAME, so this
    // plugin has no awareness of approval logic.
    [KernelFunction("send_email")]
    [Description("Send an email on behalf of the user. This is a real-world " +
                 "action that cannot be undone; use only when the user explicitly " +
                 "asks to send an email.")]
    public string SendEmail(
        [Description("Recipient email address.")] string to,
        [Description("Email subject line.")] string subject,
        [Description("Email body text.")] string body)
    {
        Console.WriteLine($"  [SIMULATED SEND] to={to} subject={subject} body={body}");
        return $"Email successfully sent to {to}.";
    }
}
