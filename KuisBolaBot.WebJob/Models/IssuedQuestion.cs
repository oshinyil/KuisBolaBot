using MongoDB.Bson;
using System;

namespace KuisBolaBot.WebJob.Models
{
    public class IssuedQuestion
    {
        public int MessageId { get; set; }
        public Quiz Quiz { get; set; }
        public string Winner { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? AnsweredDate { get; set; }
    }
}
