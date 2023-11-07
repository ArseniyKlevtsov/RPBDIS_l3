using System;
using System.Collections.Generic;

namespace RPBDIS_l3.Model;

public partial class Position
{
    public int PositionId { get; set; }

    public string? PositionName { get; set; }

    public float? SalaryUsd { get; set; }

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
