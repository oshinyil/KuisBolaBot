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
            Players = new List<Player>();
        }

        public ObjectId Id { get; set; }

        public long ChatId { get; set; }

        public List<Player> Players { get; set; }

        public List<QuestionAndWinner> QuestionAndWinners { get; set; }

        [BsonIgnore]
        public ObjectId CurrentQuizId { get; set; }

        [BsonIgnore]
        public DateTime CurrentQuizStartedDate { get; set; }

        public string Starter { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public IEnumerable<ObjectId> GetPlayedQuizIds()
        {
            return QuestionAndWinners.Select(q => q.QuizId).ToList();
        }
    }
}
