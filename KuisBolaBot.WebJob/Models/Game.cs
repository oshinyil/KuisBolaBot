using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KuisBolaBot.WebJob.Models
{
    public class Game
    {
        public Game()
        {
            QuestionAndWinners = new List<QuestionAndWinner>();
        }

        public ObjectId Id { get; set; }

        public long ChatId { get; set; }

        public List<QuestionAndWinner> QuestionAndWinners { get; set; }

        [BsonIgnore]
        public ObjectId CurrentQuizId { get; set; }

        public string WinnerUserName { get; set; }

        public int WinnerScore { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public IEnumerable<ObjectId> GetPlayedQuizIds()
        {
            return QuestionAndWinners.Select(q => q.QuizId).ToList();
        }
    }

    

}
