using MongoDB.Bson;

namespace KuisBolaBot.WebJob.Models
{
    public class QuestionAndWinner
    {
        public ObjectId QuizId { get; set; }
        public string UserName { get; set; }
    }
}
