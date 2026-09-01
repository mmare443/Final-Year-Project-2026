using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class Room
{
    public int RoomId { get; set; }

    public int HostelId { get; set; }

    public string RoomNumber { get; set; } = null!;

    public int Capacity { get; set; }

    public virtual ICollection<AccommodationRecord> AccommodationRecords { get; set; } = new List<AccommodationRecord>();

    public virtual Hostel Hostel { get; set; } = null!;
}
