using MongoDB.Bson;
using System;

namespace KuisBolaBot.WebJob.Models
{
    public class Table
    {
        public ObjectId Id { get; set; }
        public string UserName { get; set; }
        public int GamesPlayed { get; set; }
        public int Points { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
