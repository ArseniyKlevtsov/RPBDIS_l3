using System;
using System.Collections.Generic;

namespace RPBDIS_l3.Model;

public partial class Stop
{
    public int StopId { get; set; }

    public string? StopName { get; set; }

    public bool? IsRailwayStation { get; set; }

    public bool? HasWaitingRoom { get; set; }

    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();

    public virtual ICollection<Train> TrainArrivalStops { get; set; } = new List<Train>();

    public virtual ICollection<Train> TrainDepartureStops { get; set; } = new List<Train>();
}
