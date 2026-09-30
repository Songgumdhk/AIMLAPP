using Microsoft.Data.SqlTypes;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIMLAPP.Models;

[Table("Experiences")]
public class Experience
{
    [Key]
    public int Id { get; set; }

    [Column("Experience")]
    public string ExperienceText { get; set; } = null!;

    [Column("ExpVector")]
    public SqlVector<float>? ExpVector { get; set; }

    [Column("Questions")]
    public string? Questions { get; set; }
}
