using System;

namespace KuisBolaBot.WebJob.Models
{
    public class Player
    {
        public string UserName { get; set; }
        public int Points { get; set; }
        public DateTime JoinedDate { get; set; }
    }
}
