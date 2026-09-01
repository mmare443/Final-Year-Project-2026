using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class Hostel
{
    public int HostelId { get; set; }

    public string HostelName { get; set; } = null!;

    public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
}
