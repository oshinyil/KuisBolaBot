using KuisBolaBot.WebJob.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KuisBolaBot.WebJob
{
    public class GameManager
    {
        private static string dbConnection = Configuration.Instance["DatabaseConnection"];
        private static string dbName = Configuration.Instance["DatabaseName"];

        private static List<Game> games = new List<Game>();
        private static MongoClient client = new MongoClient(dbConnection);
        private static IMongoDatabase db = client.GetDatabase(dbName);

        public static string Start(long chatId)
        {
            try
            {
                if (!HasGameStarted(chatId))
                {
                    games.Add(new Game
                    {
                        ChatId = chatId,
                        StartDate = DateTime.Now
                    });

                    return "SUCCESS";
                }

                return "EXIST";
            }
            catch (Exception ex)
            {
                return "FAILED";
            }
        }

        public static bool HasGameStarted(long chatId)
        {
            return games.Any(g => g.ChatId == chatId);
        }

        public static QuestionMessage GenerateQuestion(long chatId)
        {
            var question = new QuestionMessage();

            var game = GetCurrentGame(chatId);
            var quiz = GetRandomQuiz(game);

            if (quiz == null)
            {
                EndGame(chatId);
            }

            game.CurrentQuizId = quiz.Id;
            game.QuestionAndWinners.Add(new QuestionAndWinner
            {
                QuizId = quiz.Id
            });

            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(quiz.Question);
            if (quiz.Type == QuestionType.MultipleChoice)
            {
                foreach (var choice in quiz.AnswerChoices.OrderBy(a => a.No))
                {
                    stringBuilder.AppendLine(string.Format("{0}. {1}", choice.No, choice.Answer));
                }
            }

            question.Message = stringBuilder.ToString();
            question.ImageUrl = quiz.ImageUrl;

            return question;
        }

        private static Quiz GetRandomQuiz(Game game)
        {
            var collection = db.GetCollection<Quiz>("Quiz");

            var filter = Builders<Quiz>.Filter.Where(q => !game.GetPlayedQuizIds().Contains(q.Id));
            var quizzes = collection.Find(filter).ToList();

            if (quizzes == null || quizzes.Count == 0)
            {
                return null;
            }

            var random = new Random();
            var quiz = quizzes[random.Next(quizzes.Count)];

            return quiz;
        }

        private static Game GetCurrentGame(long chatId)
        {
            return games.Where(g => g.ChatId == chatId).FirstOrDefault();
        }

        public static void EndGame(long chatId)
        {
            var game = GetCurrentGame(chatId);
            games.Remove(game);
        }

        public static string Join(long chatId, string userName)
        {
            var game = GetCurrentGame(chatId);
            if (game.Players.Contains(userName))
            {
                return "EXISTS";
            }

            game.Players.Add(userName);
            return "SUCCESS";
        }
    }
}
