using MongoDB.Bson;
using System;

namespace KuisBolaBot.WebJob.Models
{
    public class Quiz
    {
        public ObjectId Id { get; set; }

        public QuestionType Type { get; set; }

        public string Question { get; set; }

        public string ImageUrl { get; set; }

        public AnswerChoice[] AnswerChoices { get; set; }

        public string Answer { get; set; }

        public bool IsActive { get; set; }

        public bool IsDeleted { get; set; }

        public string CreatedBy { get; set; }

        public DateTime CreatedDate { get; set; }

        public string UpdatedBy { get; set; }

        public string UpdatedDate { get; set; }
    }
}
