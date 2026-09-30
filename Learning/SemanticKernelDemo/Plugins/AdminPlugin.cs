using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace AIMLAPP.Learning.SemanticKernelDemo.Plugins;

// Gated by ApprovalFilter — destructive tools never run without a human OK.
// Note there is no approval code in this class: the filter owns that concern,
// so the plugin stays plain business logic (Exercise #3).
public class AdminPlugin
{
    [KernelFunction("delete_record")]
    [Description("Permanently delete a record from a database table. This is " +
                 "DESTRUCTIVE and cannot be undone. Use only when the user " +
                 "explicitly requests deletion.")]
    public string DeleteRecord(
        [Description("Table name, e.g. 'customers'.")] string table,
        // An int parameter becomes "type": "integer" in the schema, and SK converts
        // the model's argument for you. No GetInt32() like Chapter 1.
        [Description("Primary key ID of the record to delete.")] int id)
    {
        Console.WriteLine($"  [SIMULATED DELETE] table={table} id={id}");
        return $"Deleted record id={id} from table '{table}'. This cannot be undone.";
    }
}
